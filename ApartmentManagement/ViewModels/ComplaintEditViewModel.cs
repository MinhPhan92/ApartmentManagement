using System.ComponentModel.DataAnnotations;

namespace ApartmentManagement.ViewModels
{
    public class ComplaintEditViewModel
    {
        [Required]
        public int ComplaintId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề phản ánh.")]
        [StringLength(200, ErrorMessage = "Tiêu đề không quá 200 ký tự.")]
        [Display(Name = "Tiêu đề phản ánh")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn danh mục phản ánh.")]
        [StringLength(50)]
        [Display(Name = "Danh mục phản ánh")]
        public string Category { get; set; } = "Vệ sinh";

        [Required(ErrorMessage = "Vui lòng chọn mức độ ưu tiên.")]
        [StringLength(30)]
        [Display(Name = "Mức độ ưu tiên")]
        public string Priority { get; set; } = "Bình thường";

        [Display(Name = "Căn hộ liên quan (Tùy chọn)")]
        public int? ApartmentId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập nội dung chi tiết phản ánh.")]
        [StringLength(2000, ErrorMessage = "Nội dung không quá 2000 ký tự.")]
        [Display(Name = "Nội dung phản ánh chi tiết")]
        public string Content { get; set; } = string.Empty;
    }
}

