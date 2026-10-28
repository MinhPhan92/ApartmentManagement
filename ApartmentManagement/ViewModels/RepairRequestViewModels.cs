using System.ComponentModel.DataAnnotations;
using ApartmentManagement.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ApartmentManagement.ViewModels;

public class RepairRequestCreateViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn căn hộ.")]
    [Display(Name = "Căn hộ")]
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
    [Display(Name = "Mức độ ưu tiên")]
    public string Priority { get; set; } = RepairPriority.Normal;

    public IEnumerable<SelectListItem> AvailableApartments { get; set; } = Enumerable.Empty<SelectListItem>();
}

public class RepairRequestAssignViewModel
{
    public int RepairRequestId { get; set; }

    [Display(Name = "Mã yêu cầu")]
    public string RequestTitle { get; set; } = string.Empty;

    public string ApartmentCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn kỹ thuật viên.")]
    [Display(Name = "Kỹ thuật viên")]
    public string TechnicianUserId { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "Ghi chú không quá 2000 ký tự.")]
    [Display(Name = "Ghi chú giao việc")]
    public string? Note { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public IEnumerable<SelectListItem> AvailableTechnicians { get; set; } = Enumerable.Empty<SelectListItem>();
}

public class RepairRequestProgressViewModel
{
    public int RepairRequestId { get; set; }
    public int AssignmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ApartmentCode { get; set; } = string.Empty;
    public string CurrentStatus { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập kết quả xử lý.")]
    [StringLength(4000, ErrorMessage = "Kết quả không quá 4000 ký tự.")]
    [Display(Name = "Kết quả xử lý")]
    public string Result { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Ghi chú không quá 1000 ký tự.")]
    [Display(Name = "Ghi chú thêm")]
    public string? Note { get; set; }

    public byte[] RowVersion { get; set; } = [];
}

public class RepairRequestConfirmViewModel
{
    public int RepairRequestId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Result { get; set; }

    [Range(1, 5, ErrorMessage = "Vui lòng đánh giá từ 1 đến 5 sao.")]
    [Display(Name = "Đánh giá chất lượng")]
    public byte Rating { get; set; } = 5;

    [StringLength(1000, ErrorMessage = "Nhận xét không quá 1000 ký tự.")]
    [Display(Name = "Nhận xét của bạn")]
    public string? Feedback { get; set; }

    public byte[] RowVersion { get; set; } = [];
}

public class RepairRequestRedoViewModel
{
    public int RepairRequestId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Result { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập lý do yêu cầu làm lại.")]
    [StringLength(1000, ErrorMessage = "Lý do không quá 1000 ký tự.")]
    [Display(Name = "Lý do chưa đạt / Yêu cầu làm lại")]
    public string RedoReason { get; set; } = string.Empty;

    public byte[] RowVersion { get; set; } = [];
}
