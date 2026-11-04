using ApartmentManagement.Data;
using ApartmentManagement.Hubs;
using ApartmentManagement.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ApartmentManagement.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ApplicationDbContext context,
        IHubContext<NotificationHub> hubContext,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<Notification> CreateNotificationAsync(
        string userId,
        string title,
        string content,
        string type,
        string? targetUrl = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId không được để trống.", nameof(userId));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Tiêu đề không được để trống.", nameof(title));

        var cleanTitle = title.Trim();
        var cleanTarget = targetUrl?.Trim();

        // Anti-Duplication: Bỏ qua nếu có thông báo trùng lặp (cùng UserId, Title, TargetUrl) trong vòng 30 giây
        var deduplicationCutoff = DateTime.UtcNow.AddSeconds(-30);
        var isDuplicate = await _context.Notifications
            .AnyAsync(n => n.UserId == userId &&
                           n.Title == cleanTitle &&
                           n.TargetUrl == cleanTarget &&
                           n.CreatedAtUtc >= deduplicationCutoff);

        if (isDuplicate)
        {
            _logger.LogInformation("Phát hiện thông báo trùng lặp cho User {UserId}: {Title}. Bỏ qua phát lại.", userId, cleanTitle);
            var existing = await _context.Notifications
                .Where(n => n.UserId == userId && n.Title == cleanTitle && n.TargetUrl == cleanTarget)
                .OrderByDescending(n => n.CreatedAtUtc)
                .FirstOrDefaultAsync();

            return existing ?? new Notification
            {
                UserId = userId,
                Title = cleanTitle,
                Content = content?.Trim() ?? string.Empty,
                Type = string.IsNullOrWhiteSpace(type) ? "Hệ thống" : type.Trim(),
                TargetUrl = cleanTarget
            };
        }

        var notification = new Notification
        {
            UserId = userId,
            Title = cleanTitle,
            Content = content?.Trim() ?? string.Empty,
            Type = string.IsNullOrWhiteSpace(type) ? "Hệ thống" : type.Trim(),
            TargetUrl = cleanTarget,
            IsRead = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        // Gửi realtime qua SignalR (không làm hỏng giao dịch nghiệp vụ nếu lỗi mạng/socket)
        try
        {
            var payload = new
            {
                notificationId = notification.NotificationId,
                title = notification.Title,
                content = notification.Content,
                type = notification.Type,
                targetUrl = notification.TargetUrl,
                createdAtUtc = notification.CreatedAtUtc.ToString("o"),
                displayDate = notification.CreatedAtUtc.ToString("dd/MM/yyyy HH:mm")
            };

            await _hubContext.Clients.User(userId).SendAsync("ReceiveNotification", payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi gửi SignalR notification cho UserId {UserId}: {Message}", userId, ex.Message);
        }

        return notification;
    }

    public async Task<List<Notification>> GetUserNotificationsAsync(
        string userId,
        int page = 1,
        int pageSize = 20,
        bool? unreadOnly = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return new List<Notification>();

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = _context.Notifications.AsNoTracking()
            .Where(x => x.UserId == userId);

        if (unreadOnly == true)
        {
            query = query.Where(x => !x.IsRead);
        }

        return await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return 0;

        return await _context.Notifications
            .CountAsync(x => x.UserId == userId && !x.IsRead);
    }

    public async Task<Notification?> GetNotificationByIdAsync(int notificationId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        // Anti-IDOR: Cư dân chỉ xem được thông báo của chính mình
        return await _context.Notifications
            .FirstOrDefaultAsync(x => x.NotificationId == notificationId && x.UserId == userId);
    }

    public async Task<(bool Success, string? ErrorMessage)> MarkAsReadAsync(int notificationId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return (false, "Người dùng chưa được xác thực.");

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(x => x.NotificationId == notificationId);

        if (notification == null)
            return (false, "Không tìm thấy thông báo.");

        // Anti-IDOR: Chặn người dùng khác đánh dấu thông báo không thuộc về mình
        if (notification.UserId != userId)
            return (false, "Bạn không có quyền thao tác trên thông báo này.");

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> MarkAllAsReadAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return (false, "Người dùng chưa được xác thực.");

        var unreadNotifications = await _context.Notifications
            .Where(x => x.UserId == userId && !x.IsRead)
            .ToListAsync();

        if (unreadNotifications.Any())
        {
            var now = DateTime.UtcNow;
            foreach (var n in unreadNotifications)
            {
                n.IsRead = true;
                n.ReadAtUtc = now;
            }
            await _context.SaveChangesAsync();
        }

        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteNotificationAsync(int notificationId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return (false, "Người dùng chưa được xác thực.");

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(x => x.NotificationId == notificationId);

        if (notification == null)
            return (false, "Không tìm thấy thông báo.");

        // Anti-IDOR: Chặn người dùng khác xóa thông báo không thuộc về mình
        if (notification.UserId != userId)
            return (false, "Bạn không có quyền xóa thông báo này.");

        _context.Notifications.Remove(notification);
        await _context.SaveChangesAsync();

        return (true, null);
    }
}

