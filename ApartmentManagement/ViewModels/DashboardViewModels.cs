using System.Collections;
using ApartmentManagement.Models;

namespace ApartmentManagement.ViewModels;

public sealed class MonthlyRevenueTrendDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string PeriodText { get; set; } = string.Empty;
    public decimal BilledAmount { get; set; }
    public decimal CollectedAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
}

public sealed class CategoryDistributionDto
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public string BadgeClass { get; set; } = "bg-secondary";
}

public sealed class ManagementDashboardViewModel : IEnumerable<Building>
{
    public string? UserFullName { get; set; }
    public string UserRole { get; set; } = "Building Manager";

    // Căn hộ & Cư trú
    public int TotalBuildings { get; set; }
    public int TotalApartments { get; set; }
    public int OccupiedApartments { get; set; }
    public int VacantApartments { get; set; }
    public double OccupancyRate => TotalApartments > 0 ? (OccupiedApartments * 100.0 / TotalApartments) : 0.0;
    public int TotalResidents { get; set; }

    // Tài chính & Thu phí
    public int TotalIssuedInvoices { get; set; }
    public int FullyPaidInvoices { get; set; }
    public int PartialPaidInvoices { get; set; }
    public int UnpaidInvoices { get; set; }
    public decimal TotalBilledAmount { get; set; }
    public decimal TotalCollectedAmount { get; set; }
    public decimal TotalOutstandingAmount { get; set; }
    public double CollectionRate => TotalBilledAmount > 0 ? (double)(TotalCollectedAmount * 100m / TotalBilledAmount) : 0.0;
    public List<MonthlyRevenueTrendDto> RevenueTrends { get; set; } = new();

    // Sửa chữa & Kỹ thuật
    public int TotalRepairs { get; set; }
    public int PendingRepairs { get; set; }
    public int InProgressRepairs { get; set; }
    public int PendingConfirmationRepairs { get; set; }
    public int CompletedRepairs { get; set; }
    public int ReopenedRepairs { get; set; }

    // Tiện ích
    public int ActiveAmenities { get; set; }

    // Phương tiện & Khách
    public int TotalActiveVehicles { get; set; }
    public int PendingVehicles { get; set; }
    public int TodayVisitorsCount { get; set; }
    public int ActiveVisitorsCount { get; set; }

    // Phản ánh
    public int PendingComplaints { get; set; }
    public int InProgressComplaints { get; set; }
    public int ResolvedComplaints { get; set; }

    // Danh sách hiển thị
    public List<Building> RecentBuildings { get; set; } = new();
    public List<RepairRequest> RecentCriticalRepairs { get; set; } = new();

    public IEnumerator<Building> GetEnumerator() => RecentBuildings.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => RecentBuildings.GetEnumerator();
}

public sealed class ResidentDashboardViewModel
{
    public Resident? Resident { get; set; }
    public string? UserFullName { get; set; }
    public string? UserEmail { get; set; }
    public Apartment? ActiveApartment { get; set; }
    public List<ApartmentResident> HouseholdMembers { get; set; } = new();

    // Hóa đơn & Công nợ
    public ApartmentInvoice? LatestInvoice { get; set; }
    public decimal LatestInvoiceConfirmedPaid { get; set; }
    public decimal LatestInvoiceRemainingDebt { get; set; }
    public decimal TotalOutstandingDebt { get; set; }

    // Dịch vụ của cư dân
    public List<RepairRequest> RecentRepairRequests { get; set; } = new();
    public List<Vehicle> RegisteredVehicles { get; set; } = new();
    public List<VisitorPass> RecentVisitorPasses { get; set; } = new();
    public int UnreadNotificationsCount { get; set; }
    public List<Notification> RecentNotifications { get; set; } = new();
}

public sealed class TechnicianDashboardViewModel
{
    public string? UserFullName { get; set; }
    public string? UserEmail { get; set; }
    public string TechnicianUserId { get; set; } = string.Empty;

    // Thống kê phân công
    public int AssignedCount { get; set; }
    public int InProgressCount { get; set; }
    public int PendingConfirmationCount { get; set; }
    public int CompletedCount { get; set; }
    public int TotalAssignedCount => AssignedCount + InProgressCount + PendingConfirmationCount + CompletedCount;

    // Danh sách nhiệm vụ
    public List<RepairAssignment> ActiveTasks { get; set; } = new();
    public List<RepairAssignment> RecentCompletedTasks { get; set; } = new();
}

public sealed class ReportsViewModel
{
    // Bộ lọc
    public int? SelectedBuildingId { get; set; }
    public int? SelectedYear { get; set; }
    public int? SelectedMonth { get; set; }
    public string? SearchTerm { get; set; }
    public List<Building> Buildings { get; set; } = new();

    // Báo cáo Tài chính & Thu phí
    public MonthlyStatementReportDto FinancialReport { get; set; } = new();

    // Thống kê Sửa chữa
    public int TotalRepairs { get; set; }
    public List<CategoryDistributionDto> RepairStatusDistribution { get; set; } = new();
    public List<CategoryDistributionDto> RepairPriorityDistribution { get; set; } = new();

    // Thống kê Phương tiện
    public int TotalVehicles { get; set; }
    public List<CategoryDistributionDto> VehicleTypeDistribution { get; set; } = new();
    public List<CategoryDistributionDto> VehicleStatusDistribution { get; set; } = new();

    // Thống kê Khách thăm
    public int TotalVisitors { get; set; }
    public int TodayVisitors { get; set; }
    public List<CategoryDistributionDto> VisitorStatusDistribution { get; set; } = new();

    // Thống kê Phản ánh
    public int TotalComplaints { get; set; }
    public List<CategoryDistributionDto> ComplaintStatusDistribution { get; set; } = new();
    public List<CategoryDistributionDto> ComplaintCategoryDistribution { get; set; } = new();
}

