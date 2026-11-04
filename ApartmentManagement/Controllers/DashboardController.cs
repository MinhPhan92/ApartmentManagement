using ApartmentManagement.Common.Security;
using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using ApartmentManagement.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationService? _notificationService;

    public DashboardController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        INotificationService? notificationService = null)
    {
        _context = context;
        _userManager = userManager;
        _notificationService = notificationService;
    }

    // Management Dashboard
    [Authorize(Policy = AppPolicies.RequireManagement)]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        var isSuperAdmin = User.IsInRole(AppRoles.SuperAdmin);

        // 2. Thống kê Căn hộ & Cư trú
        var totalBuildings = await _context.Buildings.CountAsync();
        var totalApartments = await _context.Apartments.CountAsync();
        var totalResidents = await _context.Residents.CountAsync();

        var occupiedApartments = await _context.ApartmentResidents
            .AsNoTracking()
            .Where(ar => ar.MoveOutDate == null)
            .Select(ar => ar.ApartmentId)
            .Distinct()
            .CountAsync();

        var vacantApartments = Math.Max(0, totalApartments - occupiedApartments);

        // 3. Thống kê Tài chính & Thu phí (Tuân thủ nghiêm ngặt quy tắc tiền tệ)
        var issuedInvoices = await _context.ApartmentInvoices
            .AsNoTracking()
            .Where(x => x.Status == ApartmentInvoiceStatus.Issued)
            .Select(x => new
            {
                x.ApartmentInvoiceId,
                x.TotalAmount,
                x.BillingYear,
                x.BillingMonth
            })
            .ToListAsync();

        var totalIssuedInvoices = issuedInvoices.Count;
        var totalBilledAmount = issuedInvoices.Sum(x => x.TotalAmount);

        // Tính doanh thu ĐÃ THU: Chỉ tính giao dịch Confirmed trên các hóa đơn Issued
        var invoiceIds = issuedInvoices.Select(x => x.ApartmentInvoiceId).ToList();

        var confirmedPaymentsByInvoice = await _context.PaymentTransactions
            .AsNoTracking()
            .Where(pt => pt.Status == PaymentTransactionStatus.Confirmed && invoiceIds.Contains(pt.ApartmentInvoiceId))
            .GroupBy(pt => pt.ApartmentInvoiceId)
            .Select(g => new { InvoiceId = g.Key, Paid = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.InvoiceId, x => x.Paid);

        decimal totalCollectedAmount = 0m;
        int fullyPaidInvoices = 0;
        int partialPaidInvoices = 0;
        int unpaidInvoices = 0;

        foreach (var inv in issuedInvoices)
        {
            confirmedPaymentsByInvoice.TryGetValue(inv.ApartmentInvoiceId, out var paid);
            totalCollectedAmount += paid;

            if (paid >= inv.TotalAmount && inv.TotalAmount > 0)
            {
                fullyPaidInvoices++;
            }
            else if (paid > 0)
            {
                partialPaidInvoices++;
            }
            else
            {
                unpaidInvoices++;
            }
        }

        var totalOutstandingAmount = Math.Max(0m, totalBilledAmount - totalCollectedAmount);

        // Xu hướng doanh thu 6 tháng gần nhất
        var now = DateTime.UtcNow;
        var trendMonths = Enumerable.Range(0, 6)
            .Select(i => now.AddMonths(-5 + i))
            .Select(d => new { Year = d.Year, Month = d.Month, PeriodText = $"T{d.Month}/{d.Year}" })
            .ToList();

        var revenueTrends = new List<MonthlyRevenueTrendDto>();
        foreach (var tm in trendMonths)
        {
            var monthInvoices = issuedInvoices.Where(x => x.BillingYear == tm.Year && x.BillingMonth == tm.Month).ToList();
            var billed = monthInvoices.Sum(x => x.TotalAmount);
            var collected = monthInvoices.Sum(x => confirmedPaymentsByInvoice.TryGetValue(x.ApartmentInvoiceId, out var p) ? Math.Min(p, x.TotalAmount) : 0m);
            var outstanding = Math.Max(0m, billed - collected);

            revenueTrends.Add(new MonthlyRevenueTrendDto
            {
                Year = tm.Year,
                Month = tm.Month,
                PeriodText = tm.PeriodText,
                BilledAmount = billed,
                CollectedAmount = collected,
                OutstandingAmount = outstanding
            });
        }

        // 4. Thống kê Sửa chữa & Kỹ thuật
        var repairRequests = await _context.RepairRequests
            .AsNoTracking()
            .Select(x => new { x.Status, x.Priority, x.RepairRequestId, x.Title, x.CreatedAt })
            .ToListAsync();

        var totalRepairs = repairRequests.Count;
        var pendingRepairs = repairRequests.Count(x => x.Status == RepairRequestStatus.Pending);
        var inProgressRepairs = repairRequests.Count(x => x.Status == RepairRequestStatus.Assigned || x.Status == RepairRequestStatus.InProgress);
        var pendingConfirmationRepairs = repairRequests.Count(x => x.Status == RepairRequestStatus.PendingConfirmation);
        var completedRepairs = repairRequests.Count(x => x.Status == RepairRequestStatus.Completed);
        var reopenedRepairs = repairRequests.Count(x => x.Status == RepairRequestStatus.Reopened);

        var recentCriticalRepairs = await _context.RepairRequests
            .AsNoTracking()
            .Include(r => r.Apartment).ThenInclude(a => a.Building)
            .Include(r => r.Resident).ThenInclude(res => res.User)
            .Where(r => r.Status != RepairRequestStatus.Completed && r.Status != RepairRequestStatus.Cancelled)
            .OrderByDescending(r => r.Priority == RepairPriority.Urgent)
            .ThenByDescending(r => r.Priority == RepairPriority.High)
            .ThenByDescending(r => r.CreatedAt)
            .Take(5)
            .ToListAsync();

        // 5. Thống kê Phương tiện & Khách thăm
        var totalActiveVehicles = await _context.Vehicles.CountAsync(x => x.Status == "Đã duyệt");
        var pendingVehicles = await _context.Vehicles.CountAsync(x => x.Status == "Chờ duyệt");

        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var todayVisitorsCount = await _context.VisitorPasses.CountAsync(x =>
            (x.ExpectedArrivalDate >= today && x.ExpectedArrivalDate < tomorrow) ||
            (x.ActualCheckInUtc >= today && x.ActualCheckInUtc < tomorrow));

        var activeVisitorsCount = await _context.VisitorPasses.CountAsync(x => x.Status == "Đã vào");

        // 6. Thống kê Phản ánh
        var pendingComplaints = await _context.Complaints.CountAsync(x => x.Status == "Chờ tiếp nhận");
        var inProgressComplaints = await _context.Complaints.CountAsync(x => x.Status == "Đang xử lý");
        var resolvedComplaints = await _context.Complaints.CountAsync(x => x.Status == "Đã xử lý");

        // 7. Thống kê Tiện ích
        var activeAmenities = await _context.Amenities.CountAsync(x => x.IsActive);

        // 8. Danh sách Tòa nhà
        var recentBuildings = await _context.Buildings
            .AsNoTracking()
            .Include(b => b.Apartments)
            .OrderByDescending(b => b.CreatedAt)
            .Take(5)
            .ToListAsync();

        var viewModel = new ManagementDashboardViewModel
        {
            UserFullName = user?.FullName ?? user?.UserName,
            UserRole = isSuperAdmin ? "System Administrator" : (User.IsInRole(AppRoles.Accountant) ? "Kế toán viên" : "Building Manager"),
            TotalBuildings = totalBuildings,
            TotalApartments = totalApartments,
            OccupiedApartments = occupiedApartments,
            VacantApartments = vacantApartments,
            TotalResidents = totalResidents,

            TotalIssuedInvoices = totalIssuedInvoices,
            FullyPaidInvoices = fullyPaidInvoices,
            PartialPaidInvoices = partialPaidInvoices,
            UnpaidInvoices = unpaidInvoices,
            TotalBilledAmount = totalBilledAmount,
            TotalCollectedAmount = totalCollectedAmount,
            TotalOutstandingAmount = totalOutstandingAmount,
            RevenueTrends = revenueTrends,

            TotalRepairs = totalRepairs,
            PendingRepairs = pendingRepairs,
            InProgressRepairs = inProgressRepairs,
            PendingConfirmationRepairs = pendingConfirmationRepairs,
            CompletedRepairs = completedRepairs,
            ReopenedRepairs = reopenedRepairs,
            RecentCriticalRepairs = recentCriticalRepairs,

            ActiveAmenities = activeAmenities,

            TotalActiveVehicles = totalActiveVehicles,
            PendingVehicles = pendingVehicles,
            TodayVisitorsCount = todayVisitorsCount,
            ActiveVisitorsCount = activeVisitorsCount,

            PendingComplaints = pendingComplaints,
            InProgressComplaints = inProgressComplaints,
            ResolvedComplaints = resolvedComplaints,

            RecentBuildings = recentBuildings
        };

        // Giữ tương thích ViewBag cho các partial/views hiện có
        ViewBag.UserFullName = viewModel.UserFullName;
        ViewBag.UserRole = viewModel.UserRole;
        ViewBag.TotalBuildings = viewModel.TotalBuildings;
        ViewBag.TotalApartments = viewModel.TotalApartments;
        ViewBag.TotalResidents = viewModel.TotalResidents;
        ViewBag.OccupiedApartments = viewModel.OccupiedApartments;
        ViewBag.VacantApartments = viewModel.VacantApartments;
        ViewBag.ActiveAmenities = activeAmenities;
        ViewBag.PendingComplaints = pendingComplaints + inProgressComplaints;
        ViewBag.PendingVehicles = viewModel.PendingVehicles;
        ViewBag.UnpaidInvoices = viewModel.UnpaidInvoices;
        ViewBag.PendingRepairs = viewModel.PendingRepairs;
        ViewBag.PendingTickets = viewModel.PendingRepairs + viewModel.InProgressRepairs;

        return View(viewModel);
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
            .AsNoTracking()
            .Include(r => r.ApartmentResidents)
                .ThenInclude(ar => ar.Apartment)
                    .ThenInclude(a => a.Building)
            .Include(r => r.Vehicles)
            .Include(r => r.Complaints)
            .FirstOrDefaultAsync(r => r.UserId == user.Id);

        var activeOccupancy = residentProfile?.ApartmentResidents
            .FirstOrDefault(ar => ar.MoveOutDate == null);

        List<ApartmentResident> householdMembers = new();
        Apartment? activeApartment = activeOccupancy?.Apartment;

        if (activeOccupancy != null)
        {
            householdMembers = await _context.ApartmentResidents
                .AsNoTracking()
                .Include(ar => ar.Resident)
                    .ThenInclude(r => r.User)
                .Where(ar => ar.ApartmentId == activeOccupancy.ApartmentId && ar.MoveOutDate == null)
                .OrderByDescending(ar => ar.IsOwner)
                .ThenBy(ar => ar.MoveInDate)
                .ToListAsync();
        }

        // Hóa đơn gần nhất & Công nợ
        ApartmentInvoice? latestInvoice = null;
        decimal latestInvoiceConfirmedPaid = 0m;
        decimal latestInvoiceRemainingDebt = 0m;
        decimal totalOutstandingDebt = 0m;

        if (activeApartment != null)
        {
            var issuedInvoices = await _context.ApartmentInvoices
                .AsNoTracking()
                .Include(i => i.PaymentTransactions)
                .Where(i => i.ApartmentId == activeApartment.ApartmentId && i.Status == ApartmentInvoiceStatus.Issued)
                .OrderByDescending(i => i.BillingYear)
                .ThenByDescending(i => i.BillingMonth)
                .ToListAsync();

            if (issuedInvoices.Any())
            {
                latestInvoice = issuedInvoices.First();
                latestInvoiceConfirmedPaid = latestInvoice.PaymentTransactions
                    .Where(pt => pt.Status == PaymentTransactionStatus.Confirmed)
                    .Sum(pt => pt.Amount);
                latestInvoiceRemainingDebt = Math.Max(0m, latestInvoice.TotalAmount - latestInvoiceConfirmedPaid);

                foreach (var inv in issuedInvoices)
                {
                    var paid = inv.PaymentTransactions
                        .Where(pt => pt.Status == PaymentTransactionStatus.Confirmed)
                        .Sum(pt => pt.Amount);
                    totalOutstandingDebt += Math.Max(0m, inv.TotalAmount - paid);
                }
            }
        }

        // Dịch vụ của cư dân: Sửa chữa, Xe, Khách, Thông báo
        List<RepairRequest> recentRepairs = new();
        List<Vehicle> registeredVehicles = new();
        List<VisitorPass> recentVisitors = new();

        if (residentProfile != null)
        {
            recentRepairs = await _context.RepairRequests
                .AsNoTracking()
                .Where(r => r.ResidentId == residentProfile.ResidentId)
                .OrderByDescending(r => r.CreatedAt)
                .Take(5)
                .ToListAsync();

            registeredVehicles = await _context.Vehicles
                .AsNoTracking()
                .Include(v => v.ParkingCard)
                .Where(v => v.ResidentId == residentProfile.ResidentId)
                .OrderByDescending(v => v.CreatedAt)
                .ToListAsync();

            recentVisitors = await _context.VisitorPasses
                .AsNoTracking()
                .Where(v => v.ResidentId == residentProfile.ResidentId)
                .OrderByDescending(v => v.CreatedAtUtc)
                .Take(5)
                .ToListAsync();
        }

        int unreadNotifsCount = 0;
        List<Notification> recentNotifications = new();
        if (_notificationService != null)
        {
            unreadNotifsCount = await _notificationService.GetUnreadCountAsync(user.Id);
            recentNotifications = await _notificationService.GetUserNotificationsAsync(user.Id, page: 1, pageSize: 5);
        }

        var viewModel = new ResidentDashboardViewModel
        {
            Resident = residentProfile,
            UserFullName = user.FullName,
            UserEmail = user.Email,
            ActiveApartment = activeApartment,
            HouseholdMembers = householdMembers,
            LatestInvoice = latestInvoice,
            LatestInvoiceConfirmedPaid = latestInvoiceConfirmedPaid,
            LatestInvoiceRemainingDebt = latestInvoiceRemainingDebt,
            TotalOutstandingDebt = totalOutstandingDebt,
            RecentRepairRequests = recentRepairs,
            RegisteredVehicles = registeredVehicles,
            RecentVisitorPasses = recentVisitors,
            UnreadNotificationsCount = unreadNotifsCount,
            RecentNotifications = recentNotifications
        };

        ViewBag.UserFullName = user.FullName;
        ViewBag.UserEmail = user.Email;
        ViewBag.ActiveApartment = activeApartment;
        ViewBag.HouseholdMembers = householdMembers;

        return View(viewModel);
    }

    // Technician Dashboard
    [Authorize(Policy = AppPolicies.RequireTechnician)]
    public async Task<IActionResult> Technician()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var techUserId = user.Id;

        // Thống kê số lượng theo trạng thái công việc được phân công cho KTV này
        var assignments = await _context.RepairAssignments
            .AsNoTracking()
            .Include(a => a.RepairRequest)
                .ThenInclude(r => r.Apartment)
                    .ThenInclude(apt => apt.Building)
            .Include(a => a.RepairRequest)
                .ThenInclude(r => r.Resident)
                    .ThenInclude(res => res.User)
            .Where(a => a.TechnicianUserId == techUserId)
            .OrderByDescending(a => a.AssignedAt)
            .ToListAsync();

        var assignedCount = assignments.Count(a => a.RepairRequest.Status == RepairRequestStatus.Assigned);
        var inProgressCount = assignments.Count(a => a.RepairRequest.Status == RepairRequestStatus.InProgress);
        var pendingConfirmationCount = assignments.Count(a => a.RepairRequest.Status == RepairRequestStatus.PendingConfirmation);
        var completedCount = assignments.Count(a => a.RepairRequest.Status == RepairRequestStatus.Completed);

        // Công việc đang hoạt động (Assigned hoặc InProgress)
        var activeTasks = assignments
            .Where(a => a.RepairRequest.Status == RepairRequestStatus.Assigned ||
                        a.RepairRequest.Status == RepairRequestStatus.InProgress ||
                        a.RepairRequest.Status == RepairRequestStatus.Reopened)
            .OrderByDescending(a => a.RepairRequest.Priority == RepairPriority.Urgent)
            .ThenByDescending(a => a.RepairRequest.Priority == RepairPriority.High)
            .ThenByDescending(a => a.AssignedAt)
            .ToList();

        // Công việc gần đây đã hoàn thành
        var recentCompleted = assignments
            .Where(a => a.RepairRequest.Status == RepairRequestStatus.Completed ||
                        a.RepairRequest.Status == RepairRequestStatus.PendingConfirmation)
            .Take(5)
            .ToList();

        var viewModel = new TechnicianDashboardViewModel
        {
            UserFullName = user.FullName ?? user.UserName,
            UserEmail = user.Email,
            TechnicianUserId = techUserId,
            AssignedCount = assignedCount,
            InProgressCount = inProgressCount,
            PendingConfirmationCount = pendingConfirmationCount,
            CompletedCount = completedCount,
            ActiveTasks = activeTasks,
            RecentCompletedTasks = recentCompleted
        };

        return View(viewModel);
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
