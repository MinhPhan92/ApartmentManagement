using System.ComponentModel.DataAnnotations;

namespace ApartmentManagement.ViewModels
{
    public class VehicleReviewViewModel
    {
        public int VehicleId { get; set; }

        public string LicensePlate { get; set; } = string.Empty;

        public string VehicleType { get; set; } = string.Empty;

        public string? ResidentName { get; set; }

        [StringLength(500, ErrorMessage = "Ghi chú không quá 500 ký tự.")]
        [Display(Name = "Ghi chú / Lý do")]
        public string? ReviewNotes { get; set; }
    }
}
