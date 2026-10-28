using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DashboardController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Admin & Manager Dashboard
        [Authorize(Policy = AppPolicies.RequireManagement)]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var isSystemAdmin = User.IsInRole(AppRoles.SuperAdmin);

            var totalBuildings = await _context.Buildings.CountAsync();
            var totalApartments = await _context.Apartments.CountAsync();
            var totalResidents = await _context.Residents.CountAsync();
            var occupiedApartments = await _context.ApartmentResidents
                .Where(ar => ar.MoveOutDate == null)
                .Select(ar => ar.ApartmentId)
                .Distinct()
                .CountAsync();

            var recentBuildings = await _context.Buildings
                .Include(b => b.Apartments)
                .OrderByDescending(b => b.CreatedAt)
                .Take(5)
                .ToListAsync();

            var pendingComplaints = await _context.Complaints.CountAsync(x => x.Status == "Chờ tiếp nhận" || x.Status == "Đang xử lý");
            var pendingVehicles = await _context.Vehicles.CountAsync(x => x.Status == "Chờ duyệt");
            var activeAmenities = await _context.Amenities.CountAsync(x => x.IsActive);
            var unpaidInvoices = await _context.ApartmentInvoices.CountAsync(x => x.Status == ApartmentInvoiceStatus.Issued && !x.PaymentTransactions.Any(pt => pt.Status == PaymentTransactionStatus.Confirmed));
            var pendingRepairs = await _context.RepairRequests.CountAsync(x =>
                x.Status == RepairRequestStatus.Pending ||
                x.Status == RepairRequestStatus.Assigned ||
                x.Status == RepairRequestStatus.InProgress ||
                x.Status == RepairRequestStatus.Reopened);

            ViewBag.UserFullName = user?.FullName ?? user?.UserName;
            ViewBag.UserRole = isSystemAdmin ? "System Administrator" : "Building Manager";
            ViewBag.TotalBuildings = totalBuildings;
            ViewBag.TotalApartments = totalApartments;
            ViewBag.TotalResidents = totalResidents;
            ViewBag.OccupiedApartments = occupiedApartments;
            ViewBag.VacantApartments = Math.Max(0, totalApartments - occupiedApartments);
            ViewBag.PendingComplaints = pendingComplaints;
            ViewBag.PendingVehicles = pendingVehicles;
            ViewBag.ActiveAmenities = activeAmenities;
            ViewBag.UnpaidInvoices = unpaidInvoices;
            ViewBag.PendingRepairs = pendingRepairs;
            ViewBag.PendingTickets = pendingRepairs;

            return View(recentBuildings);
        }

        // Resident Dashboard
        [Authorize(Policy = AppPolicies.RequireResident)]
        public async Task<IActionResult> Resident()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var residentProfile = await _context.Residents
                .Include(r => r.ApartmentResidents)
                    .ThenInclude(ar => ar.Apartment)
                        .ThenInclude(a => a.Building)
                .Include(r => r.Vehicles)
                .Include(r => r.Complaints)
                .FirstOrDefaultAsync(r => r.UserId == user.Id);

            var activeOccupancy = residentProfile?.ApartmentResidents
                .FirstOrDefault(ar => ar.MoveOutDate == null);

            List<ApartmentResident> householdMembers = new();
            if (activeOccupancy != null)
            {
                householdMembers = await _context.ApartmentResidents
                    .Include(ar => ar.Resident)
                        .ThenInclude(r => r.User)
                    .Where(ar => ar.ApartmentId == activeOccupancy.ApartmentId && ar.MoveOutDate == null)
                    .OrderByDescending(ar => ar.IsOwner)
                    .ThenBy(ar => ar.MoveInDate)
                    .ToListAsync();
            }

            ViewBag.UserFullName = user.FullName;
            ViewBag.UserEmail = user.Email;
            ViewBag.ActiveApartment = activeOccupancy?.Apartment;
            ViewBag.HouseholdMembers = householdMembers;

            return View(residentProfile);
        }

        [HttpGet]
        [Authorize(Policy = AppPolicies.RequireResident)]
        public async Task<IActionResult> EditProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var resident = await _context.Residents
                .FirstOrDefaultAsync(r => r.UserId == user.Id);
            if (resident == null) return NotFound();

            var model = new ResidentProfileEditViewModel
            {
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                CitizenId = resident.CitizenId,
                DateOfBirth = resident.DateOfBirth,
                Gender = resident.Gender,
                Address = resident.Address,
                EmergencyContact = resident.EmergencyContact
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AppPolicies.RequireResident)]
        public async Task<IActionResult> EditProfile(ResidentProfileEditViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var resident = await _context.Residents.FirstOrDefaultAsync(r => r.UserId == user.Id);
            if (resident == null) return NotFound();

            model.FullName = user.FullName;
            model.Email = user.Email ?? string.Empty;
            model.CitizenId = resident.CitizenId;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            resident.DateOfBirth = model.DateOfBirth;
            resident.Gender = string.IsNullOrWhiteSpace(model.Gender) ? null : model.Gender.Trim();
            resident.Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim();
            resident.EmergencyContact = string.IsNullOrWhiteSpace(model.EmergencyContact) ? null : model.EmergencyContact.Trim();

            try
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cập nhật thông tin thành công.";
                return RedirectToAction(nameof(Resident));
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty, "Không thể cập nhật thông tin do dữ liệu không hợp lệ.");
                return View(model);
            }
        }
    }
}
