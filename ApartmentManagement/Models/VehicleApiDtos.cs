using System.ComponentModel.DataAnnotations;

namespace ApartmentManagement.Models;

public sealed class RegisterVehicleRequest
{
    [Range(1, int.MaxValue)]
    public int ApartmentId { get; set; }

    [Required, StringLength(50)]
    public string VehicleType { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string LicensePlate { get; set; } = string.Empty;

    [StringLength(100)]
    public string? BrandModel { get; set; }

    [StringLength(50)]
    public string? Color { get; set; }
}

public sealed record VehicleResponse(
    int Id,
    int ApartmentId,
    string ApartmentCode,
    int ResidentId,
    string ResidentName,
    string VehicleType,
    string LicensePlate,
    string? BrandModel,
    string? Color,
    string Status,
    DateTime CreatedAt,
    ParkingCardSummary? ParkingCard);

public sealed record ParkingCardSummary(int Id, string CardCode, string Status, DateTime IssuedAtUtc);

public sealed class IssueParkingCardRequest
{
    [Range(1, int.MaxValue)]
    public int VehicleId { get; set; }

    [Required, StringLength(32)]
    public string CardCode { get; set; } = string.Empty;
}

public sealed record ParkingCardResponse(
    int Id,
    string CardCode,
    string Status,
    DateTime IssuedAtUtc,
    int VehicleId,
    string LicensePlate,
    int ApartmentId,
    string ApartmentCode);
