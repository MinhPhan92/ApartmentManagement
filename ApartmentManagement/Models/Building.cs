using System.ComponentModel.DataAnnotations;

namespace ApartmentManagement.Models
{
    public class Building
    {
        public int BuildingId { get; set; }

        [Required(ErrorMessage = "Mã tòa nhà là bắt buộc.")]
        [StringLength(50, ErrorMessage = "Mã tòa nhà không quá 50 ký tự.")]
        [Display(Name = "Mã tòa nhà")]
        public string BuildingCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên tòa nhà là bắt buộc.")]
        [StringLength(100, ErrorMessage = "Tên tòa nhà không quá 100 ký tự.")]
        [Display(Name = "Tên tòa nhà")]
        public string BuildingName { get; set; } = string.Empty;

        [StringLength(255, ErrorMessage = "Địa chỉ không quá 255 ký tự.")]
        [Display(Name = "Địa chỉ")]
        public string? Address { get; set; }

        [Range(1, 200, ErrorMessage = "Số tầng phải từ 1 đến 200.")]
        [Display(Name = "Số tầng")]
        public int NumberOfFloors { get; set; } = 1;

        [Display(Name = "Ngày tạo")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Apartment> Apartments { get; set; }
            = new List<Apartment>();
    }
}