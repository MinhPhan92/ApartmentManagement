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

            var pendingTickets = await _context.IncidentTickets.CountAsync(x => x.Status == IncidentTicketStatus.Submitted || x.Status == IncidentTicketStatus.InProgress);
            var pendingComplaints = await _context.Complaints.CountAsync(x => x.Status == "Chờ tiếp nhận" || x.Status == "Đang xử lý");
            var pendingVehicles = await _context.Vehicles.CountAsync(x => x.Status == "Chờ duyệt");
            var activeAmenities = await _context.Amenities.CountAsync(x => x.IsActive);
            var unpaidInvoices = await _context.ApartmentInvoices.CountAsync(x => x.Status == ApartmentInvoiceStatus.Issued && !x.PaymentTransactions.Any(pt => pt.Status == PaymentTransactionStatus.Confirmed));

            ViewBag.UserFullName = user?.FullName ?? user?.UserName;
            ViewBag.UserRole = isSystemAdmin ? "System Administrator" : "Building Manager";
            ViewBag.TotalBuildings = totalBuildings;
            ViewBag.TotalApartments = totalApartments;
            ViewBag.TotalResidents = totalResidents;
            ViewBag.OccupiedApartments = occupiedApartments;
            ViewBag.VacantApartments = Math.Max(0, totalApartments - occupiedApartments);
            ViewBag.PendingTickets = pendingTickets;
            ViewBag.PendingComplaints = pendingComplaints;
            ViewBag.PendingVehicles = pendingVehicles;
            ViewBag.ActiveAmenities = activeAmenities;
            ViewBag.UnpaidInvoices = unpaidInvoices;

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
                .Include(r => r.IncidentTickets)
                .FirstOrDefaultAsync(r => r.UserId == user.Id);

            ViewBag.UserFullName = user.FullName;
            ViewBag.UserEmail = user.Email;

            return View(residentProfile);
        }
    }
}
