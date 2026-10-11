using System.ComponentModel.DataAnnotations;

namespace ApartmentManagement.Models
{
    public class Vehicle
    {
        public int VehicleId { get; set; }

        [Required]
        public int ResidentId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn loại phương tiện.")]
        [StringLength(50)]
        [Display(Name = "Loại phương tiện")]
        public string VehicleType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập biển số xe.")]
        [StringLength(20, ErrorMessage = "Biển số xe không quá 20 ký tự.")]
        [Display(Name = "Biển số xe")]
        public string LicensePlate { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "Hãng / Dòng xe không quá 100 ký tự.")]
        [Display(Name = "Hãng / Dòng xe")]
        public string? BrandModel { get; set; }

        [StringLength(50, ErrorMessage = "Màu xe không quá 50 ký tự.")]
        [Display(Name = "Màu xe")]
        public string? Color { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Trạng thái")]
        public string Status { get; set; } = "Chờ duyệt";

        [Display(Name = "Ngày đăng ký")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Display(Name = "Ngày duyệt / xử lý")]
        public DateTime? ReviewedAt { get; set; }

        [StringLength(500, ErrorMessage = "Ghi chú không quá 500 ký tự.")]
        [Display(Name = "Ghi chú duyệt/từ chối")]
        public string? ReviewNotes { get; set; }

        // Navigation
        public Resident? Resident { get; set; }
    }
}
