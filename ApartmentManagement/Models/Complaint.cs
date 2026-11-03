using System.ComponentModel.DataAnnotations;

namespace ApartmentManagement.Models
{
    public class Complaint
    {
        public int ComplaintId { get; set; }

        [Required]
        public int ResidentId { get; set; }

        public int? ApartmentId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề phản ánh.")]
        [StringLength(200, ErrorMessage = "Tiêu đề không quá 200 ký tự.")]
        [Display(Name = "Tiêu đề")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập nội dung chi tiết phản ánh.")]
        [StringLength(2000, ErrorMessage = "Nội dung không quá 2000 ký tự.")]
        [Display(Name = "Nội dung")]
        public string Content { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn danh mục phản ánh.")]
        [StringLength(50)]
        [Display(Name = "Danh mục")]
        public string Category { get; set; } = "Dịch vụ khác";

        [Required]
        [StringLength(30)]
        [Display(Name = "Mức độ ưu tiên")]
        public string Priority { get; set; } = "Bình thường";

        [Required]
        [StringLength(50)]
        [Display(Name = "Trạng thái")]
        public string Status { get; set; } = "Chờ tiếp nhận";

        [Display(Name = "Ngày gửi")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Display(Name = "Ngày hoàn tất")]
        public DateTime? ResolvedAt { get; set; }

        [StringLength(1000, ErrorMessage = "Phản hồi không quá 1000 ký tự.")]
        [Display(Name = "Phản hồi từ Ban Quản Lý")]
        public string? ResponseNotes { get; set; }

        [Range(1, 5, ErrorMessage = "Đánh giá từ 1 đến 5 sao.")]
        [Display(Name = "Đánh giá")]
        public int? Rating { get; set; }

        [StringLength(1000, ErrorMessage = "Nhận xét không quá 1000 ký tự.")]
        [Display(Name = "Ý kiến cư dân")]
        public string? ResidentFeedback { get; set; }

        [Display(Name = "Ngày đánh giá")]
        public DateTime? RatedAt { get; set; }

        [StringLength(450)]
        [Display(Name = "Người phụ trách")]
        public string? HandlerUserId { get; set; }

        // Navigation
        public Resident? Resident { get; set; }

        public Apartment? Apartment { get; set; }

        public ApplicationUser? HandlerUser { get; set; }

        public ICollection<ComplaintStatusHistory> StatusHistory { get; set; } = new List<ComplaintStatusHistory>();
    }
}
