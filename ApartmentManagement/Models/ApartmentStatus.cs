namespace ApartmentManagement.Models;

public static class ApartmentStatus
{
    public const string Vacant = "Trống";
    public const string Occupied = "Đang sử dụng";
    public const string Maintenance = "Bảo trì";
    public const string Inactive = "Không hoạt động";

    // Legacy labels for backwards compatibility
    public const string LegacyVacant = "Đang trống";
    public const string LegacyMaintenance = "Đang bảo trì";

    public static readonly IReadOnlyList<string> All =
    [
        Vacant,
        Occupied,
        Maintenance,
        Inactive
    ];

    public static bool IsValid(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        var s = status.Trim();
        return s == Vacant || s == Occupied || s == Maintenance || s == Inactive
               || s == LegacyVacant || s == LegacyMaintenance;
    }

    public static string Normalize(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return Vacant;
        var trimmed = status.Trim();
        if (trimmed == LegacyVacant) return Vacant;
        if (trimmed == LegacyMaintenance) return Maintenance;
        return trimmed;
    }
}
