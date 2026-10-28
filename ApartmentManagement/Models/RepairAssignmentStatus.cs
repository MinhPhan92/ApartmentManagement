namespace ApartmentManagement.Models;

public static class RepairAssignmentStatus
{
    public const string Assigned = "Assigned";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Reassigned = "Reassigned";

    public static readonly IReadOnlyList<string> AllStatuses =
    [
        Assigned,
        InProgress,
        Completed,
        Reassigned
    ];

    public static string ToDisplayName(string status) => status switch
    {
        Assigned => "Đã phân công",
        InProgress => "Đang xử lý",
        Completed => "Hoàn thành",
        Reassigned => "Đã chuyển giao",
        _ => status
    };
}
