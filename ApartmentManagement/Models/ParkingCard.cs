using System.ComponentModel.DataAnnotations;

namespace ApartmentManagement.Models;

public class ParkingCard
{
    public int ParkingCardId { get; set; }
    public int VehicleId { get; set; }

    [Required, StringLength(32)]
    public string CardCode { get; set; } = string.Empty;

    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;

    [Required, StringLength(32)]
    public string Status { get; set; } = "Active";

    public Vehicle Vehicle { get; set; } = null!;
}
