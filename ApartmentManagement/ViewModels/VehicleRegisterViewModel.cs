using System.ComponentModel.DataAnnotations;

namespace ApartmentManagement.ViewModels
{
    public class VehicleRegisterViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn loại phương tiện.")]
        [Display(Name = "Loại phương tiện")]
        public string VehicleType { get; set; } = "Xe máy";

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
    }
}
