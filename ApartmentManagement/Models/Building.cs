namespace ApartmentManagement.Models
{
    public class Building
    {
        public int BuildingId { get; set; }

        public string BuildingCode { get; set; } = string.Empty;

        public string BuildingName { get; set; } = string.Empty;

        public string? Address { get; set; }

        public int NumberOfFloors { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Apartment> Apartments { get; set; }
            = new List<Apartment>();
    }
}