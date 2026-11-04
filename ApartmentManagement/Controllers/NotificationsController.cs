using System.Security.Claims;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApartmentManagement.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    // GET: /Notifications
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, bool? unreadOnly = null)
    {
        const int pageSize = 15;
        var notifications = await _notificationService.GetUserNotificationsAsync(CurrentUserId, page, pageSize, unreadOnly);
        var unreadCount = await _notificationService.GetUnreadCountAsync(CurrentUserId);

        ViewBag.CurrentPage = page;
        ViewBag.UnreadOnly = unreadOnly;
        ViewBag.UnreadCount = unreadCount;

        return View(notifications);
    }

    // GET: /Notifications/Details/5
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var notification = await _notificationService.GetNotificationByIdAsync(id, CurrentUserId);
        if (notification == null)
        {
            return NotFound();
        }

        if (!notification.IsRead)
        {
            await _notificationService.MarkAsReadAsync(id, CurrentUserId);
        }

        if (!string.IsNullOrWhiteSpace(notification.TargetUrl) && Url.IsLocalUrl(notification.TargetUrl))
        {
            return LocalRedirect(notification.TargetUrl);
        }

        return View(notification);
    }

    // POST: /Notifications/MarkAsRead/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(int id, string? returnUrl = null)
    {
        var result = await _notificationService.MarkAsReadAsync(id, CurrentUserId);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: /Notifications/MarkAllAsRead
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead(string? returnUrl = null)
    {
        await _notificationService.MarkAllAsReadAsync(CurrentUserId);
        TempData["SuccessMessage"] = "Đã đánh dấu tất cả thông báo là đã đọc.";

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: /Notifications/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _notificationService.DeleteNotificationAsync(id, CurrentUserId);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Đã xóa thông báo.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ==========================================
    // JSON APIs FOR HEADER DROPDOWN & REALTIME
    // ==========================================

    // GET: /api/notifications/recent
    [HttpGet("api/notifications/recent")]
    public async Task<IActionResult> GetRecentNotifications()
    {
        var notifications = await _notificationService.GetUserNotificationsAsync(CurrentUserId, page: 1, pageSize: 6);
        var unreadCount = await _notificationService.GetUnreadCountAsync(CurrentUserId);

        var items = notifications.Select(n => new
        {
            id = n.NotificationId,
            title = n.Title,
            content = n.Content,
            type = n.Type,
            isRead = n.IsRead,
            targetUrl = n.TargetUrl ?? Url.Action(nameof(Details), new { id = n.NotificationId }),
            createdAtUtc = n.CreatedAtUtc.ToString("o"),
            displayDate = n.CreatedAtUtc.ToString("dd/MM/yyyy HH:mm")
        });

        return Json(new
        {
            unreadCount,
            items
        });
    }

    // GET: /api/notifications/unread-count
    [HttpGet("api/notifications/unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var count = await _notificationService.GetUnreadCountAsync(CurrentUserId);
        return Json(new { unreadCount = count });
    }

    // POST: /api/notifications/mark-read/{id}
    [HttpPost("api/notifications/mark-read/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApiMarkAsRead(int id)
    {
        var result = await _notificationService.MarkAsReadAsync(id, CurrentUserId);
        if (!result.Success)
        {
            return BadRequest(new { message = result.ErrorMessage });
        }
        var unreadCount = await _notificationService.GetUnreadCountAsync(CurrentUserId);
        return Json(new { success = true, unreadCount });
    }

    // POST: /api/notifications/mark-all-read
    [HttpPost("api/notifications/mark-all-read")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApiMarkAllAsRead()
    {
        await _notificationService.MarkAllAsReadAsync(CurrentUserId);
        return Json(new { success = true, unreadCount = 0 });
    }
}

