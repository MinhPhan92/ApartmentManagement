namespace ApartmentManagement.Models
{
    public class Apartment
    {
        public int ApartmentId { get; set; }

        public int BuildingId { get; set; }

        public string ApartmentCode { get; set; } = string.Empty;

        public int Floor { get; set; }

        public decimal Area { get; set; }

        public string Status { get; set; } = "Đang sử dụng";

        public Building Building { get; set; } = null!;

        public ICollection<ApartmentResident> ApartmentResidents { get; set; }
            = new List<ApartmentResident>();
    }
}