using System.ComponentModel.DataAnnotations;

namespace ApartmentManagement.ViewModels
{
    public class ComplaintProcessViewModel
    {
        public int ComplaintId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string? ResidentName { get; set; }

        public string? ApartmentCode { get; set; }

        public string CurrentStatus { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn trạng thái cập nhật.")]
        [Display(Name = "Trạng thái mới")]
        public string TargetStatus { get; set; } = "Đang xử lý";

        [StringLength(1000, ErrorMessage = "Nội dung phản hồi không quá 1000 ký tự.")]
        [Display(Name = "Nội dung phản hồi / Giải pháp xử lý")]
        public string? ResponseNotes { get; set; }
    }
}
