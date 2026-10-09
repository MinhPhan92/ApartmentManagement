using System.ComponentModel.DataAnnotations;

namespace ApartmentManagement.Models
{
    public class Apartment
    {
        public int ApartmentId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn tòa nhà.")]
        public int BuildingId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã căn hộ.")]
        [StringLength(50)]
        [Display(Name = "Mã căn hộ")]
        public string ApartmentCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tầng.")]
        [Range(1, 200, ErrorMessage = "Tầng phải từ 1 đến 200.")]
        [Display(Name = "Tầng")]
        public int Floor { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập diện tích.")]
        [Range(1, 10000, ErrorMessage = "Diện tích phải lớn hơn 0.")]
        [Display(Name = "Diện tích (m²)")]
        public decimal Area { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn trạng thái.")]
        [StringLength(50)]
        [Display(Name = "Trạng thái")]
        public string Status { get; set; } = "Đang sử dụng";

        public Building? Building { get; set; }

        public ICollection<ApartmentResident> ApartmentResidents { get; set; }
            = new List<ApartmentResident>();

        public ICollection<ApartmentContract> Contracts { get; set; }
            = new List<ApartmentContract>();

        public ICollection<IncidentTicket> IncidentTickets { get; set; }
            = new List<IncidentTicket>();

        public ICollection<UtilityMeterReading> UtilityMeterReadings { get; set; }
            = new List<UtilityMeterReading>();

        public ICollection<ApartmentInvoice> Invoices { get; set; }
            = new List<ApartmentInvoice>();
    }
}