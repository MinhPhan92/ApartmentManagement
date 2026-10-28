namespace ApartmentManagement.Models;

public static class RepairPriority
{
    public const string Low = "Low";
    public const string Normal = "Normal";
    public const string High = "High";
    public const string Urgent = "Urgent";

    public static readonly IReadOnlyList<string> AllPriorities =
    [
        Low,
        Normal,
        High,
        Urgent
    ];

    public static string ToDisplayName(string priority) => priority switch
    {
        Low => "Thấp",
        Normal => "Bình thường",
        High => "Cao",
        Urgent => "Khẩn cấp",
        _ => priority
    };

    public static string ToBadgeClass(string priority) => priority switch
    {
        Low => "bg-secondary text-white",
        Normal => "bg-info text-dark",
        High => "bg-warning text-dark",
        Urgent => "bg-danger text-white",
        _ => "bg-secondary text-white"
    };
}
