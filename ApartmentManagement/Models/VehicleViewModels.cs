using System.ComponentModel.DataAnnotations;

namespace ApartmentManagement.Models;

public class VehicleReviewViewModel
{
    public int VehicleId { get; set; }
    public string LicensePlate { get; set; } = string.Empty;
    public string VehicleType { get; set; } = string.Empty;
    public string ApartmentCode { get; set; } = string.Empty;
    public string ResidentName { get; set; } = string.Empty;
    public string ResidentPhone { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    [Display(Name = "Mã thẻ từ bãi đỗ (RFID)")]
    [StringLength(50, ErrorMessage = "Mã thẻ không được vượt quá 50 ký tự.")]
    public string? CardCode { get; set; }

    [Display(Name = "Ghi chú thẩm định / Lý do từ chối")]
    [StringLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự.")]
    public string? ReviewNotes { get; set; }
}

public sealed record VehicleReviewResult(
    bool Success,
    string? ErrorMessage,
    int? ParkingCardId = null)
{
    public void Deconstruct(out bool success, out string? errorMessage)
    {
        success = Success;
        errorMessage = ErrorMessage;
    }
}

