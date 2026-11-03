using System.ComponentModel.DataAnnotations;

namespace ApartmentManagement.Models;

public static class TechnicalIncidentCategory
{
    public const string Electricity = "Điện";
    public const string Water = "Nước / Bơm";
    public const string Elevator = "Thang máy";
    public const string FireSafety = "PCCC";
    public const string CommonArea = "Khu vực chung";
    public const string HVAC = "Thông gió / ĐHKK";
    public const string Other = "Khác";

    public static readonly IReadOnlyList<string> AllCategories =
    [
        Electricity,
        Water,
        Elevator,
        FireSafety,
        CommonArea,
        HVAC,
        Other
    ];
}

public static class TechnicalIncidentPriority
{
    public const string Low = "Low";
    public const string Normal = "Normal";
    public const string High = "High";
    public const string Critical = "Critical";

    public static readonly IReadOnlyList<string> AllPriorities =
    [
        Low,
        Normal,
        High,
        Critical
    ];
}

public static class TechnicalIncidentStatus
{
    public const string Reported = "Reported";         // Kỹ thuật viên đã gửi báo cáo, chờ BQL tiếp nhận
    public const string Acknowledged = "Acknowledged"; // BQL đã tiếp nhận báo cáo
    public const string Assigned = "Assigned";         // BQL đã phân công KTV xử lý
    public const string InProgress = "InProgress";     // KTV đang tiến hành xử lý
    public const string Resolved = "Resolved";         // KTV đã xử lý xong và báo cáo kết quả
    public const string Closed = "Closed";             // BQL đã nghiệm thu và đóng sự cố
    public const string Cancelled = "Cancelled";       // Sự cố bị hủy hoặc trùng lặp

    public static readonly IReadOnlyList<string> AllStatuses =
    [
        Reported,
        Acknowledged,
        Assigned,
        InProgress,
        Resolved,
        Closed,
        Cancelled
    ];
}

public class TechnicalIncident
{
    public int TechnicalIncidentId { get; set; }

    [Required]
    [StringLength(30)]
    public string IncidentCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn tòa nhà xảy ra sự cố.")]
    public int BuildingId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập vị trí cụ thể xảy ra sự cố.")]
    [StringLength(200, ErrorMessage = "Vị trí không quá 200 ký tự.")]
    [Display(Name = "Vị trí sự cố")]
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn loại sự cố.")]
    [StringLength(50)]
    [Display(Name = "Loại sự cố")]
    public string Category { get; set; } = TechnicalIncidentCategory.Other;

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề sự cố.")]
    [StringLength(200, ErrorMessage = "Tiêu đề không quá 200 ký tự.")]
    [Display(Name = "Tiêu đề")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mô tả chi tiết sự cố.")]
    [StringLength(4000, ErrorMessage = "Mô tả không quá 4000 ký tự.")]
    [Display(Name = "Mô tả sự cố")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn mức độ ưu tiên.")]
    [StringLength(30)]
    [Display(Name = "Mức độ ưu tiên")]
    public string Priority { get; set; } = TechnicalIncidentPriority.Normal;

    [Required]
    [StringLength(30)]
    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = TechnicalIncidentStatus.Reported;

    [Required]
    [StringLength(450)]
    public string ReportedByUserId { get; set; } = string.Empty;

    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;

    [StringLength(450)]
    public string? AcknowledgedByUserId { get; set; }

    public DateTime? AcknowledgedAt { get; set; }

    [StringLength(450)]
    public string? AssignedTechnicianUserId { get; set; }

    public DateTime? AssignedAt { get; set; }

    [Display(Name = "Hạn xử lý")]
    public DateTime? ScheduledDate { get; set; }

    public DateTime? ResolvedAt { get; set; }

    [StringLength(4000)]
    [Display(Name = "Kết quả xử lý")]
    public string? ResolutionNotes { get; set; }

    [StringLength(2000)]
    [Display(Name = "Vật tư thay thế")]
    public string? SpareParts { get; set; }

    [Display(Name = "Chi phí vật tư")]
    public decimal? MaterialCost { get; set; }

    [StringLength(450)]
    public string? ClosedByUserId { get; set; }

    public DateTime? ClosedAt { get; set; }

    public byte[] RowVersion { get; set; } = [];

    // Navigations
    public Building Building { get; set; } = null!;
    public ApplicationUser ReportedByUser { get; set; } = null!;
    public ApplicationUser? AcknowledgedByUser { get; set; }
    public ApplicationUser? AssignedTechnicianUser { get; set; }
    public ApplicationUser? ClosedByUser { get; set; }
    public ICollection<TechnicalIncidentStatusHistory> StatusHistory { get; set; } = new List<TechnicalIncidentStatusHistory>();
}

