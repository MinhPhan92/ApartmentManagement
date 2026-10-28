using System.ComponentModel.DataAnnotations;

namespace ApartmentManagement.Models;

public class RepairRequest
{
    public int RepairRequestId { get; set; }

    [Required]
    public int ResidentId { get; set; }

    [Required]
    public int ApartmentId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề yêu cầu.")]
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
    public string Priority { get; set; } = RepairPriority.Normal;

    [Required]
    [StringLength(30)]
    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = RepairRequestStatus.Pending;

    [StringLength(4000)]
    [Display(Name = "Kết quả xử lý")]
    public string? Result { get; set; }

    [Display(Name = "Ngày tạo")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Display(Name = "Ngày kỹ thuật viên hoàn thành")]
    public DateTime? CompletedAt { get; set; }

    [Display(Name = "Ngày cư dân nghiệm thu")]
    public DateTime? ConfirmedAt { get; set; }

    [Range(1, 5, ErrorMessage = "Đánh giá từ 1 đến 5 sao.")]
    [Display(Name = "Đánh giá")]
    public byte? Rating { get; set; }

    [StringLength(1000, ErrorMessage = "Nhận xét không quá 1000 ký tự.")]
    [Display(Name = "Nhận xét của cư dân")]
    public string? ResidentFeedback { get; set; }

    public byte[] RowVersion { get; set; } = [];

    // Navigations
    public Resident Resident { get; set; } = null!;
    public Apartment Apartment { get; set; } = null!;
    public ICollection<RepairAssignment> Assignments { get; set; } = new List<RepairAssignment>();
    public ICollection<RepairStatusHistory> StatusHistory { get; set; } = new List<RepairStatusHistory>();
}
