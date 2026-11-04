using ApartmentManagement.Data;
using ApartmentManagement.Hubs;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ApartmentManagement.Tests;

public class NotificationModuleTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private (Mock<IHubContext<NotificationHub>> hubContextMock, Mock<IClientProxy> clientProxyMock) CreateHubContextMock()
    {
        var hubContextMock = new Mock<IHubContext<NotificationHub>>();
        var hubClientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();

        hubClientsMock.Setup(clients => clients.User(It.IsAny<string>()))
            .Returns(clientProxyMock.Object);

        hubContextMock.Setup(hub => hub.Clients)
            .Returns(hubClientsMock.Object);

        return (hubContextMock, clientProxyMock);
    }

    [Fact]
    public async Task CreateNotification_SavesToDatabase_AndSendsRealtimeSignalRToUser()
    {
        using var context = CreateInMemoryDbContext();
        var (hubContextMock, clientProxyMock) = CreateHubContextMock();
        var service = new NotificationService(context, hubContextMock.Object, NullLogger<NotificationService>.Instance);

        var notification = await service.CreateNotificationAsync(
            "user-101",
            "Hóa đơn mới kỳ 11/2026",
            "Hóa đơn căn hộ A-101 đã phát hành.",
            "Phí",
            "/MyInvoices/Details/5");

        Assert.NotNull(notification);
        Assert.True(notification.NotificationId > 0);
        Assert.Equal("user-101", notification.UserId);
        Assert.Equal("Hóa đơn mới kỳ 11/2026", notification.Title);
        Assert.False(notification.IsRead);

        // Kiểm tra lưu DB
        var inDb = await context.Notifications.FindAsync(notification.NotificationId);
        Assert.NotNull(inDb);
        Assert.Equal("user-101", inDb.UserId);

        // Kiểm tra gửi SignalR đến đúng user
        hubContextMock.Verify(h => h.Clients.User("user-101"), Times.Once);
        clientProxyMock.Verify(c => c.SendCoreAsync("ReceiveNotification", It.IsAny<object[]>(), default), Times.Once);
    }

    [Fact]
    public async Task CreateNotification_WhenSignalRThrows_DoesNotThrow_AndNotificationIsSaved()
    {
        using var context = CreateInMemoryDbContext();
        var (hubContextMock, clientProxyMock) = CreateHubContextMock();

        // Giả lập SignalR bị lỗi kết nối/socket
        clientProxyMock.Setup(c => c.SendCoreAsync("ReceiveNotification", It.IsAny<object[]>(), default))
            .ThrowsAsync(new InvalidOperationException("SignalR connection broken"));

        var service = new NotificationService(context, hubContextMock.Object, NullLogger<NotificationService>.Instance);

        // Gọi không được ném ngoại lệ làm hỏng thao tác
        var notification = await service.CreateNotificationAsync(
            "user-102",
            "Bảo trì thang máy",
            "Thang máy số 2 đang bảo trì.",
            "Sửa chữa");

        Assert.NotNull(notification);
        Assert.True(notification.NotificationId > 0);

        // Thông báo vẫn được lưu an toàn trong database
        var inDb = await context.Notifications.FindAsync(notification.NotificationId);
        Assert.NotNull(inDb);
        Assert.Equal("user-102", inDb.UserId);
    }

    [Fact]
    public async Task GetUserNotifications_ReturnsOnlyNotificationsBelongingToRequestedUser()
    {
        using var context = CreateInMemoryDbContext();
        var (hubContextMock, _) = CreateHubContextMock();
        var service = new NotificationService(context, hubContextMock.Object, NullLogger<NotificationService>.Instance);

        context.Notifications.AddRange(
            new Notification { UserId = "user-A", Title = "Tin A1", Content = "Nội dung A1", Type = "Phí" },
            new Notification { UserId = "user-A", Title = "Tin A2", Content = "Nội dung A2", Type = "Sửa chữa" },
            new Notification { UserId = "user-B", Title = "Tin B1", Content = "Nội dung B1", Type = "Phương tiện" }
        );
        await context.SaveChangesAsync();

        var listA = await service.GetUserNotificationsAsync("user-A");
        var listB = await service.GetUserNotificationsAsync("user-B");

        Assert.Equal(2, listA.Count);
        Assert.All(listA, n => Assert.Equal("user-A", n.UserId));

        Assert.Single(listB);
        Assert.Equal("user-B", listB[0].UserId);
    }

    [Fact]
    public async Task GetNotificationById_WhenForeignUser_ReturnsNull_AntiIDOR()
    {
        using var context = CreateInMemoryDbContext();
        var (hubContextMock, _) = CreateHubContextMock();
        var service = new NotificationService(context, hubContextMock.Object, NullLogger<NotificationService>.Instance);

        var notifA = new Notification
        {
            UserId = "user-A",
            Title = "Tin riêng tư A",
            Content = "Thông tin tài chính căn hộ A",
            Type = "Phí"
        };
        context.Notifications.Add(notifA);
        await context.SaveChangesAsync();

        // User A xem của chính mình -> OK
        var resultOwner = await service.GetNotificationByIdAsync(notifA.NotificationId, "user-A");
        Assert.NotNull(resultOwner);

        // User B cố tình truy cập ID của User A -> Null (Anti-IDOR)
        var resultForeign = await service.GetNotificationByIdAsync(notifA.NotificationId, "user-B");
        Assert.Null(resultForeign);
    }

    [Fact]
    public async Task MarkAsRead_WhenOwner_UpdatesIsReadAndReadAtUtc()
    {
        using var context = CreateInMemoryDbContext();
        var (hubContextMock, _) = CreateHubContextMock();
        var service = new NotificationService(context, hubContextMock.Object, NullLogger<NotificationService>.Instance);

        var notif = new Notification
        {
            UserId = "user-A",
            Title = "Thông báo chưa đọc",
            Content = "Chi tiết",
            IsRead = false
        };
        context.Notifications.Add(notif);
        await context.SaveChangesAsync();

        var (success, _) = await service.MarkAsReadAsync(notif.NotificationId, "user-A");
        Assert.True(success);

        var updated = await context.Notifications.FindAsync(notif.NotificationId);
        Assert.True(updated!.IsRead);
        Assert.NotNull(updated.ReadAtUtc);
    }

    [Fact]
    public async Task MarkAsRead_WhenForeignUser_FailsWithErrorMessage_AntiIDOR()
    {
        using var context = CreateInMemoryDbContext();
        var (hubContextMock, _) = CreateHubContextMock();
        var service = new NotificationService(context, hubContextMock.Object, NullLogger<NotificationService>.Instance);

        var notif = new Notification
        {
            UserId = "user-A",
            Title = "Thông báo A",
            Content = "Chi tiết",
            IsRead = false
        };
        context.Notifications.Add(notif);
        await context.SaveChangesAsync();

        // User B cố tình đánh dấu đã đọc thông báo của User A
        var (success, error) = await service.MarkAsReadAsync(notif.NotificationId, "user-B");
        Assert.False(success);
        Assert.Contains("Bạn không có quyền thao tác", error);

        // Trạng thái thông báo không bị thay đổi
        var unread = await context.Notifications.FindAsync(notif.NotificationId);
        Assert.False(unread!.IsRead);
    }

    [Fact]
    public async Task MarkAllAsRead_MarksOnlyUserNotificationsAsRead()
    {
        using var context = CreateInMemoryDbContext();
        var (hubContextMock, _) = CreateHubContextMock();
        var service = new NotificationService(context, hubContextMock.Object, NullLogger<NotificationService>.Instance);

        context.Notifications.AddRange(
            new Notification { UserId = "user-A", Title = "A1", Content = "C1", IsRead = false },
            new Notification { UserId = "user-A", Title = "A2", Content = "C2", IsRead = false },
            new Notification { UserId = "user-B", Title = "B1", Content = "C3", IsRead = false }
        );
        await context.SaveChangesAsync();

        var (success, _) = await service.MarkAllAsReadAsync("user-A");
        Assert.True(success);

        var userANotifs = await context.Notifications.Where(n => n.UserId == "user-A").ToListAsync();
        Assert.All(userANotifs, n => Assert.True(n.IsRead));

        // Thông báo của user-B vẫn giữ nguyên là chưa đọc
        var userBNotif = await context.Notifications.SingleAsync(n => n.UserId == "user-B");
        Assert.False(userBNotif.IsRead);
    }

    [Fact]
    public async Task DeleteNotification_WhenForeignUser_FailsWithErrorMessage_AntiIDOR()
    {
        using var context = CreateInMemoryDbContext();
        var (hubContextMock, _) = CreateHubContextMock();
        var service = new NotificationService(context, hubContextMock.Object, NullLogger<NotificationService>.Instance);

        var notif = new Notification
        {
            UserId = "user-A",
            Title = "A1",
            Content = "C1"
        };
        context.Notifications.Add(notif);
        await context.SaveChangesAsync();

        var (success, error) = await service.DeleteNotificationAsync(notif.NotificationId, "user-B");
        Assert.False(success);
        Assert.Contains("Bạn không có quyền xóa", error);

        // Bản ghi vẫn còn trong DB
        var exists = await context.Notifications.FindAsync(notif.NotificationId);
        Assert.NotNull(exists);
    }

    [Fact]
    public async Task OfflineUser_ReceivesSavedNotificationsWhenLoggingIn()
    {
        using var context = CreateInMemoryDbContext();
        var (hubContextMock, _) = CreateHubContextMock();
        var service = new NotificationService(context, hubContextMock.Object, NullLogger<NotificationService>.Instance);

        // Tạo thông báo khi user đang offline
        await service.CreateNotificationAsync("user-offline", "Hóa đơn mới", "Nội dung phí", "Phí");
        await service.CreateNotificationAsync("user-offline", "Xe duyệt", "Nội dung xe", "Phương tiện");

        // Khi user đăng nhập lại và tải danh sách thông báo:
        var unreadCount = await service.GetUnreadCountAsync("user-offline");
        var notifications = await service.GetUserNotificationsAsync("user-offline");

        Assert.Equal(2, unreadCount);
        Assert.Equal(2, notifications.Count);
        Assert.All(notifications, n => Assert.False(n.IsRead));
    }

    [Fact]
    public async Task VehicleApproval_WhenApproved_GeneratesNotificationForOwner()
    {
        using var context = CreateInMemoryDbContext();
        var (hubContextMock, _) = CreateHubContextMock();
        var notificationService = new NotificationService(context, hubContextMock.Object, NullLogger<NotificationService>.Instance);

        var user = new ApplicationUser { Id = "res-owner", UserName = "res_owner", FullName = "Chủ Xe A" };
        var resident = new Resident { UserId = user.Id, CitizenId = "001122334455" };
        var vehicle = new Vehicle
        {
            Resident = resident,
            ApartmentId = 1,
            LicensePlate = "29A-88888",
            VehicleType = "Ô tô",
            Status = "Chờ duyệt"
        };
        context.AddRange(user, resident, vehicle);
        await context.SaveChangesAsync();

        var vehicleService = new VehicleService(context, currentUserService: null, notificationService: notificationService);

        var result = await vehicleService.ApproveVehicleAsync(vehicle.VehicleId, "CARD-88", "Đạt chuẩn", "admin-id");
        Assert.True(result.Success);

        // Kiểm tra thông báo được tạo cho chủ xe
        var notifs = await context.Notifications.Where(n => n.UserId == "res-owner").ToListAsync();
        Assert.Single(notifs);
        Assert.Contains("đã được duyệt", notifs[0].Title);
        Assert.Contains("29A-88888", notifs[0].Content);
        Assert.Equal("Phương tiện", notifs[0].Type);
    }

    [Fact]
    public async Task VehicleApproval_WhenRejected_GeneratesRejectionNotificationWithReason()
    {
        using var context = CreateInMemoryDbContext();
        var (hubContextMock, _) = CreateHubContextMock();
        var notificationService = new NotificationService(context, hubContextMock.Object, NullLogger<NotificationService>.Instance);

        var user = new ApplicationUser { Id = "res-owner-2", UserName = "res_owner_2", FullName = "Chủ Xe B" };
        var resident = new Resident { UserId = user.Id, CitizenId = "001122334466" };
        var vehicle = new Vehicle
        {
            Resident = resident,
            ApartmentId = 1,
            LicensePlate = "30E-99999",
            VehicleType = "Xe máy",
            Status = "Chờ duyệt"
        };
        context.AddRange(user, resident, vehicle);
        await context.SaveChangesAsync();

        var vehicleService = new VehicleService(context, currentUserService: null, notificationService: notificationService);

        var result = await vehicleService.RejectVehicleAsync(vehicle.VehicleId, "Giấy tờ xe không hợp lệ", "admin-id");
        Assert.True(result.Success);

        var notifs = await context.Notifications.Where(n => n.UserId == "res-owner-2").ToListAsync();
        Assert.Single(notifs);
        Assert.Contains("bị từ chối", notifs[0].Title);
        Assert.Contains("Giấy tờ xe không hợp lệ", notifs[0].Content);
    }

    [Fact]
    public async Task CreateNotification_WhenDuplicateWithin30Seconds_DoesNotDuplicateInDatabase()
    {
        using var context = CreateInMemoryDbContext();
        var (hubContextMock, clientProxyMock) = CreateHubContextMock();
        var service = new NotificationService(context, hubContextMock.Object, NullLogger<NotificationService>.Instance);

        // Lần gọi 1
        var notif1 = await service.CreateNotificationAsync("user-dup", "Hóa đơn mới", "Nội dung", "Phí", "/MyInvoices/Details/10");
        // Lần gọi 2 (trùng lặp ngay sau đó)
        var notif2 = await service.CreateNotificationAsync("user-dup", "Hóa đơn mới", "Nội dung khác", "Phí", "/MyInvoices/Details/10");

        // Phải trả về thông báo đã có, không tạo dòng mới
        var allNotifs = await context.Notifications.Where(n => n.UserId == "user-dup").ToListAsync();
        Assert.Single(allNotifs);
        Assert.Equal(notif1.NotificationId, notif2.NotificationId);

        // SignalR chỉ gửi 1 lần
        clientProxyMock.Verify(c => c.SendCoreAsync("ReceiveNotification", It.IsAny<object[]>(), default), Times.Once);
    }

    [Fact]
    public async Task PaymentAttemptCreation_DoesNotSendConfirmationNotification()
    {
        using var context = CreateInMemoryDbContext();
        var (hubContextMock, clientProxyMock) = CreateHubContextMock();
        var notifService = new NotificationService(context, hubContextMock.Object, NullLogger<NotificationService>.Instance);

        var residentUser = new ApplicationUser { Id = "res-pay-1", UserName = "res_pay_1", FullName = "Cư Dân Pay" };
        var resident = new Resident { UserId = residentUser.Id, CitizenId = "001122334488" };
        var invoice = new ApartmentInvoice
        {
            ApartmentId = 1,
            Resident = resident,
            ResidentId = 1,
            InvoiceCode = "INV-202611-001",
            BillingMonth = 11,
            BillingYear = 2026,
            TotalAmount = 500000m,
            Status = ApartmentInvoiceStatus.Issued
        };
        context.AddRange(residentUser, resident, invoice);
        await context.SaveChangesAsync();

        var paymentService = new PaymentService(context, NullLogger<PaymentService>.Instance, notifService);
        var result = await paymentService.CreatePaymentAttemptForInvoiceAsync(invoice.ApartmentInvoiceId);

        Assert.True(result.Success);
        Assert.Equal(PaymentTransactionStatus.Pending, result.Payment!.Status);

        // Đảm bảo KHÔNG gửi thông báo "Xác nhận thanh toán thành công" khi mới chỉ tạo attempt quét QR
        var notifs = await context.Notifications.Where(n => n.UserId == "res-pay-1").ToListAsync();
        Assert.Empty(notifs);
    }

    [Fact]
    public async Task PaymentConfirmation_DeterminesResidentUserIdCorrectly_AndSendsNotification()
    {
        using var context = CreateInMemoryDbContext();
        var (hubContextMock, _) = CreateHubContextMock();
        var notifService = new NotificationService(context, hubContextMock.Object, NullLogger<NotificationService>.Instance);

        var residentUser = new ApplicationUser { Id = "res-pay-confirm", UserName = "res_pay_confirm", FullName = "Cư Dân Confirm" };
        var resident = new Resident { UserId = residentUser.Id, CitizenId = "001122334499" };
        var invoice = new ApartmentInvoice
        {
            ApartmentId = 1,
            Resident = resident,
            InvoiceCode = "INV-202611-002",
            BillingMonth = 11,
            BillingYear = 2026,
            TotalAmount = 1000000m,
            Status = ApartmentInvoiceStatus.Issued
        };
        var payment = new PaymentTransaction
        {
            Invoice = invoice,
            Amount = 1000000m,
            Reference = "REF-CONFIRM-1",
            Status = PaymentTransactionStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };
        context.AddRange(residentUser, resident, invoice, payment);
        await context.SaveChangesAsync();

        var paymentService = new PaymentService(context, NullLogger<PaymentService>.Instance, notifService);
        var result = await paymentService.ConfirmSimulatedPaymentAsync(payment.PaymentTransactionId, "actor-admin", payment.RowVersion);

        Assert.True(result.Success);

        // Kiểm tra thông báo được gửi cho đúng cư dân
        var notifs = await context.Notifications.Where(n => n.UserId == "res-pay-confirm").ToListAsync();
        Assert.Single(notifs);
        Assert.Contains("Xác nhận thanh toán thành công", notifs[0].Title);
        Assert.Contains("1,000,000", notifs[0].Content);
        Assert.Equal($"/MyInvoices/Details/{invoice.ApartmentInvoiceId}", notifs[0].TargetUrl);
    }

    [Fact]
    public void VisitorsController_CheckInAndCheckOut_RequireManagementPolicy()
    {
        var checkInMethod = typeof(ApartmentManagement.Controllers.VisitorsController).GetMethod("CheckIn");
        Assert.NotNull(checkInMethod);
        var authAttr = checkInMethod.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authAttr);
        Assert.Equal(ApartmentManagement.Common.Security.AppPolicies.RequireManagement, authAttr.Policy);
        Assert.Null(authAttr.Roles);
    }
}
