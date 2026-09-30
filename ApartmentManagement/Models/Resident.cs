namespace ApartmentManagement.Models
{
    public class Resident
    {
        public int ResidentId { get; set; }

        public string UserId { get; set; } = string.Empty;

        public string CitizenId { get; set; } = string.Empty;

        public DateTime? DateOfBirth { get; set; }

        public string? Gender { get; set; }

        public string? Address { get; set; }

        public string? EmergencyContact { get; set; }

        public ApplicationUser User { get; set; } = null!;

        public ICollection<ApartmentResident> ApartmentResidents { get; set; }
            = new List<ApartmentResident>();
    }
}