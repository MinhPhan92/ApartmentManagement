namespace ApartmentManagement.Models;

public static class RepairRequestStatus
{
    public const string Pending = "Pending";                       // Chờ tiếp nhận
    public const string Assigned = "Assigned";                     // Đã phân công
    public const string InProgress = "InProgress";                 // Đang xử lý
    public const string PendingConfirmation = "PendingConfirmation"; // Chờ nghiệm thu
    public const string Completed = "Completed";                   // Đã hoàn thành
    public const string Reopened = "Reopened";                     // Cần xử lý lại
    public const string Cancelled = "Cancelled";                   // Đã hủy

    public static readonly IReadOnlyList<string> AllStatuses =
    [
        Pending,
        Assigned,
        InProgress,
        PendingConfirmation,
        Completed,
        Reopened,
        Cancelled
    ];

    public static string ToDisplayName(string status) => status switch
    {
        Pending => "Chờ tiếp nhận",
        Assigned => "Đã phân công",
        InProgress => "Đang xử lý",
        PendingConfirmation => "Chờ nghiệm thu",
        Completed => "Đã hoàn thành",
        Reopened => "Cần xử lý lại",
        Cancelled => "Đã hủy",
        _ => status
    };

    public static string ToBadgeClass(string status) => status switch
    {
        Pending => "bg-warning text-dark",
        Assigned => "bg-info text-dark",
        InProgress => "bg-primary text-white",
        PendingConfirmation => "bg-purple text-white",
        Completed => "bg-success text-white",
        Reopened => "bg-danger text-white",
        Cancelled => "bg-secondary text-white",
        _ => "bg-secondary text-white"
    };
}
