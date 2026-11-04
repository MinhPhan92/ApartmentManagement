using ApartmentManagement.Common.Security;
using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using ApartmentManagement.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Controllers;

[Authorize(Policy = AppPolicies.RequireAccountantOrManager)]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IFinancialReportService _financialReportService;

    public ReportsController(
        ApplicationDbContext context,
        IFinancialReportService financialReportService)
    {
        _context = context;
        _financialReportService = financialReportService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        int? buildingId,
        int? year,
        int? month,
        string? searchTerm,
        CancellationToken cancellationToken = default)
    {
        // 1. Chuẩn hóa bộ lọc thời gian
        var now = DateTime.UtcNow;
        if (year.HasValue && (year.Value < 2000 || year.Value > 2100))
        {
            year = now.Year;
        }

        if (month.HasValue && (month.Value < 1 || month.Value > 12))
        {
            month = now.Month;
        }

        var buildings = await _context.Buildings
            .AsNoTracking()
            .OrderBy(b => b.BuildingName)
            .ToListAsync(cancellationToken);

        // 2. Tạo báo cáo tài chính qua FinancialReportService (tuân thủ nghiêm ngặt quy tắc tiền tệ)
        var financialFilter = new MonthlyStatementFilterInput
        {
            BuildingId = buildingId,
            BillingYear = year,
            BillingMonth = month,
            Status = ApartmentInvoiceStatus.Issued,
            SearchTerm = searchTerm
        };

        var financialReport = await _financialReportService.GenerateMonthlyStatementAsync(financialFilter, cancellationToken);

        // 3. Thống kê Sửa chữa & Kỹ thuật
        var repairQuery = _context.RepairRequests.AsNoTracking().AsQueryable();
        if (buildingId.HasValue)
        {
            repairQuery = repairQuery.Where(r => r.Apartment != null && r.Apartment.BuildingId == buildingId.Value);
        }
        if (year.HasValue)
        {
            repairQuery = repairQuery.Where(r => r.CreatedAt.Year == year.Value);
            if (month.HasValue)
            {
                repairQuery = repairQuery.Where(r => r.CreatedAt.Month == month.Value);
            }
        }

        var repairs = await repairQuery
            .Select(r => new { r.Status, r.Priority })
            .ToListAsync(cancellationToken);

        var totalRepairs = repairs.Count;

        var repairStatusDist = new List<CategoryDistributionDto>
        {
            new() { Label = "Chờ tiếp nhận", Count = repairs.Count(r => r.Status == RepairRequestStatus.Pending), BadgeClass = "bg-warning text-dark" },
            new() { Label = "Đã phân công", Count = repairs.Count(r => r.Status == RepairRequestStatus.Assigned), BadgeClass = "bg-info text-dark" },
            new() { Label = "Đang xử lý", Count = repairs.Count(r => r.Status == RepairRequestStatus.InProgress), BadgeClass = "bg-primary" },
            new() { Label = "Chờ nghiệm thu", Count = repairs.Count(r => r.Status == RepairRequestStatus.PendingConfirmation), BadgeClass = "bg-secondary" },
            new() { Label = "Hoàn thành", Count = repairs.Count(r => r.Status == RepairRequestStatus.Completed), BadgeClass = "bg-success" },
            new() { Label = "Yêu cầu làm lại", Count = repairs.Count(r => r.Status == RepairRequestStatus.Reopened), BadgeClass = "bg-danger" }
        };

        var repairPriorityDist = new List<CategoryDistributionDto>
        {
            new() { Label = "Khẩn cấp", Count = repairs.Count(r => r.Priority == RepairPriority.Urgent), BadgeClass = "bg-danger" },
            new() { Label = "Cao", Count = repairs.Count(r => r.Priority == RepairPriority.High), BadgeClass = "bg-warning text-dark" },
            new() { Label = "Bình thường", Count = repairs.Count(r => r.Priority == RepairPriority.Normal), BadgeClass = "bg-primary" },
            new() { Label = "Thấp", Count = repairs.Count(r => r.Priority == RepairPriority.Low), BadgeClass = "bg-secondary" }
        };

        // 4. Thống kê Phương tiện
        var vehicleQuery = _context.Vehicles.AsNoTracking().AsQueryable();
        if (buildingId.HasValue)
        {
            vehicleQuery = vehicleQuery.Where(v => v.Apartment != null && v.Apartment.BuildingId == buildingId.Value);
        }

        var vehicles = await vehicleQuery
            .Select(v => new { v.VehicleType, v.Status })
            .ToListAsync(cancellationToken);

        var totalVehicles = vehicles.Count;

        var vehicleTypeDist = vehicles
            .GroupBy(v => v.VehicleType)
            .Select(g => new CategoryDistributionDto
            {
                Label = g.Key,
                Count = g.Count(),
                BadgeClass = g.Key == "Ô tô" ? "bg-primary" : (g.Key == "Xe máy" ? "bg-success" : "bg-info text-dark")
            })
            .ToList();

        var vehicleStatusDist = vehicles
            .GroupBy(v => v.Status)
            .Select(g => new CategoryDistributionDto
            {
                Label = g.Key,
                Count = g.Count(),
                BadgeClass = g.Key == "Đã duyệt" ? "bg-success" : (g.Key == "Chờ duyệt" ? "bg-warning text-dark" : "bg-secondary")
            })
            .ToList();

        // 5. Thống kê Khách thăm
        var visitorQuery = _context.VisitorPasses.AsNoTracking().AsQueryable();
        if (buildingId.HasValue)
        {
            visitorQuery = visitorQuery.Where(v => v.Apartment != null && v.Apartment.BuildingId == buildingId.Value);
        }
        if (year.HasValue)
        {
            visitorQuery = visitorQuery.Where(v => v.ExpectedArrivalDate.Year == year.Value);
            if (month.HasValue)
            {
                visitorQuery = visitorQuery.Where(v => v.ExpectedArrivalDate.Month == month.Value);
            }
        }

        var visitors = await visitorQuery
            .Select(v => new { v.Status, v.ExpectedArrivalDate, v.ActualCheckInUtc })
            .ToListAsync(cancellationToken);

        var totalVisitors = visitors.Count;
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var todayVisitors = visitors.Count(v =>
            (v.ExpectedArrivalDate >= today && v.ExpectedArrivalDate < tomorrow) ||
            (v.ActualCheckInUtc >= today && v.ActualCheckInUtc < tomorrow));

        var visitorStatusDist = visitors
            .GroupBy(v => v.Status)
            .Select(g => new CategoryDistributionDto
            {
                Label = g.Key,
                Count = g.Count(),
                BadgeClass = g.Key == "Đã vào" ? "bg-success" : (g.Key == "Đã đăng ký" ? "bg-primary" : (g.Key == "Đã ra" ? "bg-secondary" : "bg-danger"))
            })
            .ToList();

        // 6. Thống kê Phản ánh
        var complaintQuery = _context.Complaints.AsNoTracking().AsQueryable();
        if (buildingId.HasValue)
        {
            complaintQuery = complaintQuery.Where(c => c.Apartment != null && c.Apartment.BuildingId == buildingId.Value);
        }
        if (year.HasValue)
        {
            complaintQuery = complaintQuery.Where(c => c.CreatedAt.Year == year.Value);
            if (month.HasValue)
            {
                complaintQuery = complaintQuery.Where(c => c.CreatedAt.Month == month.Value);
            }
        }

        var complaints = await complaintQuery
            .Select(c => new { c.Status, c.Category })
            .ToListAsync(cancellationToken);

        var totalComplaints = complaints.Count;

        var complaintStatusDist = complaints
            .GroupBy(c => c.Status)
            .Select(g => new CategoryDistributionDto
            {
                Label = g.Key,
                Count = g.Count(),
                BadgeClass = g.Key == "Đã xử lý" ? "bg-success" : (g.Key == "Đang xử lý" ? "bg-primary" : "bg-warning text-dark")
            })
            .ToList();

        var complaintCategoryDist = complaints
            .GroupBy(c => c.Category)
            .Select(g => new CategoryDistributionDto
            {
                Label = g.Key,
                Count = g.Count(),
                BadgeClass = "bg-light text-dark border"
            })
            .ToList();

        var viewModel = new ReportsViewModel
        {
            SelectedBuildingId = buildingId,
            SelectedYear = year,
            SelectedMonth = month,
            SearchTerm = searchTerm,
            Buildings = buildings,

            FinancialReport = financialReport,

            TotalRepairs = totalRepairs,
            RepairStatusDistribution = repairStatusDist,
            RepairPriorityDistribution = repairPriorityDist,

            TotalVehicles = totalVehicles,
            VehicleTypeDistribution = vehicleTypeDist,
            VehicleStatusDistribution = vehicleStatusDist,

            TotalVisitors = totalVisitors,
            TodayVisitors = todayVisitors,
            VisitorStatusDistribution = visitorStatusDist,

            TotalComplaints = totalComplaints,
            ComplaintStatusDistribution = complaintStatusDist,
            ComplaintCategoryDistribution = complaintCategoryDist
        };

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv(
        int? buildingId,
        int? year,
        int? month,
        string? searchTerm,
        CancellationToken cancellationToken = default)
    {
        var filter = new MonthlyStatementFilterInput
        {
            BuildingId = buildingId,
            BillingYear = year,
            BillingMonth = month,
            Status = ApartmentInvoiceStatus.Issued,
            SearchTerm = searchTerm
        };

        var report = await _financialReportService.GenerateMonthlyStatementAsync(filter, cancellationToken);
        var csvBytes = _financialReportService.ExportMonthlyStatementToCsv(report);
        var fileName = _financialReportService.GetCsvFileName(report);

        return File(csvBytes, "text/csv; charset=utf-8", fileName);
    }
}

