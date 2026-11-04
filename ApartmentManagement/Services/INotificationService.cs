using ApartmentManagement.Models;

namespace ApartmentManagement.Services;

public interface INotificationService
{
    Task<Notification> CreateNotificationAsync(
        string userId,
        string title,
        string content,
        string type,
        string? targetUrl = null);

    Task<List<Notification>> GetUserNotificationsAsync(
        string userId,
        int page = 1,
        int pageSize = 20,
        bool? unreadOnly = null);

    Task<int> GetUnreadCountAsync(string userId);

    Task<Notification?> GetNotificationByIdAsync(int notificationId, string userId);

    Task<(bool Success, string? ErrorMessage)> MarkAsReadAsync(int notificationId, string userId);

    Task<(bool Success, string? ErrorMessage)> MarkAllAsReadAsync(string userId);

    Task<(bool Success, string? ErrorMessage)> DeleteNotificationAsync(int notificationId, string userId);
}

