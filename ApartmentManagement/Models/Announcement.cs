using System.ComponentModel.DataAnnotations;

namespace ApartmentManagement.Models;

public class Announcement
{
    public int AnnouncementId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề thông báo.")]
    [StringLength(200, ErrorMessage = "Tiêu đề không quá 200 ký tự.")]
    [Display(Name = "Tiêu đề")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập nội dung thông báo.")]
    [Display(Name = "Nội dung")]
    public string Content { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    [Display(Name = "Chuyên mục")]
    public string Category { get; set; } = "Thông báo chung"; // "Bảo trì", "Thu phí", "An ninh", "Thông báo chung"

    [Display(Name = "Tòa nhà áp dụng")]
    public int? BuildingId { get; set; } // null = áp dụng cho toàn khu

    [Display(Name = "Ghim lên đầu")]
    public bool IsPinned { get; set; } = false;

    [Display(Name = "Ngày tạo")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public string? CreatedByUserId { get; set; }

    public Building? Building { get; set; }
    public ApplicationUser? CreatedByUser { get; set; }
}
