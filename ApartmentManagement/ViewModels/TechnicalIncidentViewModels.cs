using System.ComponentModel.DataAnnotations;
using ApartmentManagement.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ApartmentManagement.ViewModels;

public class TechnicalIncidentCreateViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn tòa nhà xảy ra sự cố.")]
    [Display(Name = "Tòa nhà")]
    public int BuildingId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập vị trí cụ thể xảy ra sự cố.")]
    [StringLength(200, ErrorMessage = "Vị trí không quá 200 ký tự.")]
    [Display(Name = "Vị trí sự cố")]
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn loại sự cố.")]
    [Display(Name = "Loại sự cố")]
    public string Category { get; set; } = TechnicalIncidentCategory.Other;

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề sự cố.")]
    [StringLength(200, ErrorMessage = "Tiêu đề không quá 200 ký tự.")]
    [Display(Name = "Tiêu đề sự cố")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mô tả chi tiết sự cố.")]
    [StringLength(4000, ErrorMessage = "Mô tả không quá 4000 ký tự.")]
    [Display(Name = "Mô tả chi tiết")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn mức độ ưu tiên.")]
    [Display(Name = "Mức độ ưu tiên")]
    public string Priority { get; set; } = TechnicalIncidentPriority.Normal;

    public IEnumerable<SelectListItem> BuildingOptions { get; set; } = [];
    public IEnumerable<SelectListItem> CategoryOptions { get; set; } = [];
    public IEnumerable<SelectListItem> PriorityOptions { get; set; } = [];
}

public class TechnicalIncidentAssignViewModel
{
    public int TechnicalIncidentId { get; set; }
    public string IncidentCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn kỹ thuật viên phụ trách.")]
    [Display(Name = "Kỹ thuật viên phụ trách")]
    public string TechnicianUserId { get; set; } = string.Empty;

    [Display(Name = "Hạn chót xử lý")]
    [DataType(DataType.Date)]
    public DateTime? ScheduledDate { get; set; }

    [StringLength(1000, ErrorMessage = "Ghi chú không quá 1000 ký tự.")]
    [Display(Name = "Ghi chú phân công")]
    public string? AssignmentNote { get; set; }

    public string? RowVersionBase64 { get; set; }

    public IEnumerable<SelectListItem> TechnicianOptions { get; set; } = [];
}

public class TechnicalIncidentResolveViewModel
{
    public int TechnicalIncidentId { get; set; }
    public string IncidentCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập nội dung và kết quả xử lý.")]
    [StringLength(4000, ErrorMessage = "Kết quả không quá 4000 ký tự.")]
    [Display(Name = "Nội dung & Kết quả xử lý")]
    public string ResolutionNotes { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "Danh mục vật tư không quá 2000 ký tự.")]
    [Display(Name = "Vật tư / Linh kiện thay thế")]
    public string? SpareParts { get; set; }

    [Range(0, 1000000000, ErrorMessage = "Chi phí từ 0 đến 1 tỷ đồng.")]
    [Display(Name = "Chi phí vật tư phát sinh (VNĐ)")]
    public decimal? MaterialCost { get; set; }

    public string? RowVersionBase64 { get; set; }
}

