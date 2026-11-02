using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ApartmentManagement.Tests;

public class AmenityApprovalWorkflowTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private async Task<(Resident resident1, Resident resident2, Amenity amenity)> SeedDataAsync(ApplicationDbContext context, int capacity = 10)
    {
        var building = new Building
        {
            BuildingCode = "BLD-A",
            BuildingName = "Tòa A",
            NumberOfFloors = 15
        };
        context.Buildings.Add(building);
        await context.SaveChangesAsync();

        var apt = new Apartment
        {
            BuildingId = building.BuildingId,
            ApartmentCode = "A101",
            Floor = 1,
            Area = 70m
        };
        context.Apartments.Add(apt);
        await context.SaveChangesAsync();

        var user1 = new ApplicationUser
        {
            Id = "user-resident-1",
            UserName = "res1@test.com",
            Email = "res1@test.com",
            FullName = "Nguyễn Văn Cư Dân 1"
        };
        var user2 = new ApplicationUser
        {
            Id = "user-resident-2",
            UserName = "res2@test.com",
            Email = "res2@test.com",
            FullName = "Trần Thị Cư Dân 2"
        };
        context.Users.AddRange(user1, user2);
        await context.SaveChangesAsync();

        var res1 = new Resident
        {
            UserId = user1.Id,
            CitizenId = "111222333444"
        };
        var res2 = new Resident
        {
            UserId = user2.Id,
            CitizenId = "555666777888"
        };
        context.Residents.AddRange(res1, res2);
        await context.SaveChangesAsync();

        context.ApartmentResidents.AddRange(
            new ApartmentResident
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res1.ResidentId,
                Relationship = "Chủ hộ",
                MoveInDate = DateTime.Today.AddDays(-30)
            },
            new ApartmentResident
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res2.ResidentId,
                Relationship = "Thành viên",
                MoveInDate = DateTime.Today.AddDays(-30)
            }
        );
        await context.SaveChangesAsync();

        var amenity = new Amenity
        {
            Name = "Phòng Sinh Hoạt Cộng Đồng",
            Capacity = capacity,
            OpenTime = new TimeSpan(8, 0, 0),
            CloseTime = new TimeSpan(22, 0, 0),
            IsActive = true,
            PricePerHour = 0
        };
        context.Amenities.Add(amenity);
        await context.SaveChangesAsync();

        return (res1, res2, amenity);
    }

    // 1. Booking mới tạo phải ở trạng thái "Chờ duyệt"
    [Fact]
    public async Task BookAmenity_CreatesBookingWithPendingStatus()
    {
        using var context = CreateInMemoryDbContext();
        var (res1, _, amenity) = await SeedDataAsync(context);
        var service = new AmenityService(context);

        var (success, error) = await service.BookAmenityAsync(
            res1.UserId,
            amenity.AmenityId,
            DateTime.Today.AddDays(1),
            new TimeSpan(9, 0, 0),
            new TimeSpan(10, 0, 0),
            2,
            "Họp gia đình");

        Assert.True(success, error);

        var bookings = await service.GetResidentBookingsAsync(res1.UserId);
        Assert.Single(bookings);
        Assert.Equal("Chờ duyệt", bookings[0].Status);
    }

    // 2. BQL phê duyệt đơn Chờ duyệt -> chuyển sang "Đã xác nhận"
    [Fact]
    public async Task ApproveBooking_WhenPending_TransitionsToConfirmed()
    {
        using var context = CreateInMemoryDbContext();
        var (res1, _, amenity) = await SeedDataAsync(context);
        var service = new AmenityService(context);

        await service.BookAmenityAsync(
            res1.UserId,
            amenity.AmenityId,
            DateTime.Today.AddDays(1),
            new TimeSpan(9, 0, 0),
            new TimeSpan(10, 0, 0),
            2,
            "Ghi chú");

        var booking = await context.AmenityBookings.FirstAsync();
        Assert.Equal("Chờ duyệt", booking.Status);

        var (success, error) = await service.ApproveBookingAsync("manager-user-id", booking.AmenityBookingId);
        Assert.True(success, error);

        var updated = await context.AmenityBookings.FindAsync(booking.AmenityBookingId);
        Assert.Equal("Đã xác nhận", updated!.Status);
    }

    // 3. BQL từ chối đơn Chờ duyệt -> chuyển sang "Từ chối"
    [Fact]
    public async Task RejectBooking_WhenPending_TransitionsToRejected()
    {
        using var context = CreateInMemoryDbContext();
        var (res1, _, amenity) = await SeedDataAsync(context);
        var service = new AmenityService(context);

        await service.BookAmenityAsync(
            res1.UserId,
            amenity.AmenityId,
            DateTime.Today.AddDays(1),
            new TimeSpan(9, 0, 0),
            new TimeSpan(10, 0, 0),
            2,
            "Ghi chú");

        var booking = await context.AmenityBookings.FirstAsync();
        Assert.Equal("Chờ duyệt", booking.Status);

        var (success, error) = await service.RejectBookingAsync("manager-user-id", booking.AmenityBookingId, "Bảo trì đột xuất");
        Assert.True(success, error);

        var updated = await context.AmenityBookings.FindAsync(booking.AmenityBookingId);
        Assert.Equal("Từ chối", updated!.Status);
    }

    // 4. Không cho phép phê duyệt lại đơn đã xác nhận
    [Fact]
    public async Task ApproveBooking_WhenAlreadyConfirmed_ReturnsError()
    {
        using var context = CreateInMemoryDbContext();
        var (res1, _, amenity) = await SeedDataAsync(context);
        var service = new AmenityService(context);

        await service.BookAmenityAsync(
            res1.UserId,
            amenity.AmenityId,
            DateTime.Today.AddDays(1),
            new TimeSpan(9, 0, 0),
            new TimeSpan(10, 0, 0),
            2,
            null);

        var booking = await context.AmenityBookings.FirstAsync();
        await service.ApproveBookingAsync("manager-id", booking.AmenityBookingId);

        // Thử phê duyệt lại
        var (success, error) = await service.ApproveBookingAsync("manager-id", booking.AmenityBookingId);
        Assert.False(success);
        Assert.Contains("đã được phê duyệt", error);
    }

    // 5. Không cho phép phê duyệt đơn đã bị hủy
    [Fact]
    public async Task ApproveBooking_WhenCancelled_ReturnsError()
    {
        using var context = CreateInMemoryDbContext();
        var (res1, _, amenity) = await SeedDataAsync(context);
        var service = new AmenityService(context);

        await service.BookAmenityAsync(
            res1.UserId,
            amenity.AmenityId,
            DateTime.Today.AddDays(1),
            new TimeSpan(9, 0, 0),
            new TimeSpan(10, 0, 0),
            2,
            null);

        var booking = await context.AmenityBookings.FirstAsync();
        await service.CancelBookingAsync(res1.UserId, booking.AmenityBookingId);

        // Thử phê duyệt đơn đã hủy
        var (success, error) = await service.ApproveBookingAsync("manager-id", booking.AmenityBookingId);
        Assert.False(success);
        Assert.Contains("đã bị hủy", error);
    }

    // 6. Không cho phép từ chối đơn đã xác nhận
    [Fact]
    public async Task RejectBooking_WhenAlreadyConfirmed_ReturnsError()
    {
        using var context = CreateInMemoryDbContext();
        var (res1, _, amenity) = await SeedDataAsync(context);
        var service = new AmenityService(context);

        await service.BookAmenityAsync(
            res1.UserId,
            amenity.AmenityId,
            DateTime.Today.AddDays(1),
            new TimeSpan(9, 0, 0),
            new TimeSpan(10, 0, 0),
            2,
            null);

        var booking = await context.AmenityBookings.FirstAsync();
        await service.ApproveBookingAsync("manager-id", booking.AmenityBookingId);

        // Thử từ chối đơn đã duyệt
        var (success, error) = await service.RejectBookingAsync("manager-id", booking.AmenityBookingId);
        Assert.False(success);
        Assert.Contains("Không thể từ chối lịch đặt chỗ đã được phê duyệt", error);
    }

    // 7. Booking bị từ chối sẽ giải phóng sức chứa
    [Fact]
    public async Task RejectedBooking_FreesUpCapacityForOtherResidents()
    {
        using var context = CreateInMemoryDbContext();
        // Sức chứa = 5 người
        var (res1, res2, amenity) = await SeedDataAsync(context, capacity: 5);
        var service = new AmenityService(context);

        var date = DateTime.Today.AddDays(1);
        var startTime = new TimeSpan(10, 0, 0);
        var endTime = new TimeSpan(11, 0, 0);

        // Cư dân 1 đăng ký 5 người -> hết chỗ
        await service.BookAmenityAsync(res1.UserId, amenity.AmenityId, date, startTime, endTime, 5, null);

        // Cư dân 2 đăng ký 2 người cùng khung giờ -> Bị từ chối do quá sức chứa
        var (failResult, failError) = await service.BookAmenityAsync(res2.UserId, amenity.AmenityId, date, startTime, endTime, 2, null);
        Assert.False(failResult);
        Assert.Contains("Không đủ sức chứa", failError);

        // BQL từ chối đơn của Cư dân 1
        var booking1 = await context.AmenityBookings.FirstAsync();
        var (rejectSuccess, _) = await service.RejectBookingAsync("manager-id", booking1.AmenityBookingId);
        Assert.True(rejectSuccess);

        // Cư dân 2 đăng ký lại -> Thành công vì đơn của Cư dân 1 đã bị từ chối và giải phóng chỗ
        var (retrySuccess, retryError) = await service.BookAmenityAsync(res2.UserId, amenity.AmenityId, date, startTime, endTime, 2, null);
        Assert.True(retrySuccess, retryError);
    }

    // 8. Booking bị hủy sẽ giải phóng sức chứa
    [Fact]
    public async Task CancelledBooking_FreesUpCapacityForOtherResidents()
    {
        using var context = CreateInMemoryDbContext();
        var (res1, res2, amenity) = await SeedDataAsync(context, capacity: 4);
        var service = new AmenityService(context);

        var date = DateTime.Today.AddDays(1);
        var startTime = new TimeSpan(14, 0, 0);
        var endTime = new TimeSpan(15, 0, 0);

        // Cư dân 1 đặt 4 người và đã được duyệt
        await service.BookAmenityAsync(res1.UserId, amenity.AmenityId, date, startTime, endTime, 4, null);
        var booking1 = await context.AmenityBookings.FirstAsync();
        await service.ApproveBookingAsync("manager-id", booking1.AmenityBookingId);

        // Cư dân 2 thử đặt 2 người -> Quá sức chứa
        var (failResult, _) = await service.BookAmenityAsync(res2.UserId, amenity.AmenityId, date, startTime, endTime, 2, null);
        Assert.False(failResult);

        // Cư dân 1 hủy lịch
        var (cancelSuccess, _) = await service.CancelBookingAsync(res1.UserId, booking1.AmenityBookingId);
        Assert.True(cancelSuccess);

        // Cư dân 2 đăng ký lại -> Thành công
        var (retrySuccess, retryError) = await service.BookAmenityAsync(res2.UserId, amenity.AmenityId, date, startTime, endTime, 2, null);
        Assert.True(retrySuccess, retryError);
    }

    // 9. Booking "Chờ duyệt" vẫn giữ chỗ tạm thời (không cho phép overbooking trước khi BQL duyệt)
    [Fact]
    public async Task PendingBooking_HoldsCapacityAndPreventsOverbooking()
    {
        using var context = CreateInMemoryDbContext();
        var (res1, res2, amenity) = await SeedDataAsync(context, capacity: 5);
        var service = new AmenityService(context);

        var date = DateTime.Today.AddDays(2);
        var startTime = new TimeSpan(10, 0, 0);
        var endTime = new TimeSpan(11, 0, 0);

        // Cư dân 1 gửi đăng ký 4 người (trạng thái: Chờ duyệt, chưa được duyệt)
        var (b1Success, _) = await service.BookAmenityAsync(res1.UserId, amenity.AmenityId, date, startTime, endTime, 4, null);
        Assert.True(b1Success);

        // Cư dân 2 đăng ký 2 người vào cùng khung giờ (4 + 2 = 6 > 5)
        var (b2Success, b2Error) = await service.BookAmenityAsync(res2.UserId, amenity.AmenityId, date, startTime, endTime, 2, null);
        Assert.False(b2Success);
        Assert.Contains("Không đủ sức chứa", b2Error);
    }

    // 10. Cư dân không thể hủy booking của cư dân khác
    [Fact]
    public async Task CancelBooking_WhenUserIsNotOwner_ReturnsError()
    {
        using var context = CreateInMemoryDbContext();
        var (res1, res2, amenity) = await SeedDataAsync(context);
        var service = new AmenityService(context);

        await service.BookAmenityAsync(
            res1.UserId,
            amenity.AmenityId,
            DateTime.Today.AddDays(1),
            new TimeSpan(9, 0, 0),
            new TimeSpan(10, 0, 0),
            2,
            null);

        var booking = await context.AmenityBookings.FirstAsync();

        // Cư dân 2 cố gắng hủy booking của Cư dân 1
        var (success, error) = await service.CancelBookingAsync(res2.UserId, booking.AmenityBookingId, isManager: false);
        Assert.False(success);
        Assert.Contains("Bạn không có quyền hủy lịch đặt chỗ của cư dân khác", error);

        // Trạng thái đơn vẫn là Chờ duyệt
        var untouched = await context.AmenityBookings.FindAsync(booking.AmenityBookingId);
        Assert.Equal("Chờ duyệt", untouched!.Status);
    }

    // 11. Ban quản lý có quyền hủy booking (isManager = true)
    [Fact]
    public async Task CancelBooking_WhenManager_SucceedsEvenIfDifferentUserId()
    {
        using var context = CreateInMemoryDbContext();
        var (res1, _, amenity) = await SeedDataAsync(context);
        var service = new AmenityService(context);

        await service.BookAmenityAsync(
            res1.UserId,
            amenity.AmenityId,
            DateTime.Today.AddDays(1),
            new TimeSpan(9, 0, 0),
            new TimeSpan(10, 0, 0),
            2,
            null);

        var booking = await context.AmenityBookings.FirstAsync();

        // BQL hủy đơn của Cư dân 1
        var (success, error) = await service.CancelBookingAsync("manager-admin-id", booking.AmenityBookingId, isManager: true);
        Assert.True(success, error);

        var updated = await context.AmenityBookings.FindAsync(booking.AmenityBookingId);
        Assert.Equal("Đã hủy", updated!.Status);
    }

    // 12. Quy tắc BR_03: Cùng một cư dân không thể tạo nhiều đăng ký trùng thời gian cho cùng một tiện ích
    [Fact]
    public async Task BookAmenity_WhenSameResidentRegistersOverlappingTime_IsRejected()
    {
        using var context = CreateInMemoryDbContext();
        var (res1, _, amenity) = await SeedDataAsync(context, capacity: 50);
        var service = new AmenityService(context);

        var date = DateTime.Today.AddDays(1);

        // Đăng ký 1: 09:00 - 11:00 (1 người)
        var (firstSuccess, _) = await service.BookAmenityAsync(
            res1.UserId,
            amenity.AmenityId,
            date,
            new TimeSpan(9, 0, 0),
            new TimeSpan(11, 0, 0),
            1,
            "Lần 1");
        Assert.True(firstSuccess);

        // Đăng ký 2: cùng cư dân đó đăng ký 10:00 - 12:00 (giao nhau từ 10:00 - 11:00) dù sức chứa còn thừa rất nhiều
        var (secondSuccess, secondError) = await service.BookAmenityAsync(
            res1.UserId,
            amenity.AmenityId,
            date,
            new TimeSpan(10, 0, 0),
            new TimeSpan(12, 0, 0),
            1,
            "Lần 2");

        Assert.False(secondSuccess);
        Assert.Contains("Bạn đã có đơn đăng ký cho tiện ích này trong cùng khung giờ", secondError);
    }

    // 13. Lọc danh sách booking theo trạng thái
    [Fact]
    public async Task GetBookings_FiltersByStatusCorrectly()
    {
        using var context = CreateInMemoryDbContext();
        var (res1, res2, amenity) = await SeedDataAsync(context);
        var service = new AmenityService(context);

        var date = DateTime.Today.AddDays(1);

        // Đơn 1: 09:00 - 10:00 (Chờ duyệt)
        await service.BookAmenityAsync(res1.UserId, amenity.AmenityId, date, new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0), 1, null);

        // Đơn 2: 10:00 - 11:00 (Phê duyệt -> Đã xác nhận)
        await service.BookAmenityAsync(res2.UserId, amenity.AmenityId, date, new TimeSpan(10, 0, 0), new TimeSpan(11, 0, 0), 1, null);
        var all = await context.AmenityBookings.ToListAsync();
        var booking2 = all.First(b => b.ResidentId == res2.ResidentId);
        await service.ApproveBookingAsync("mgr", booking2.AmenityBookingId);

        // Lọc Chờ duyệt
        var pendingOnly = await service.GetBookingsAsync(status: "Chờ duyệt");
        Assert.Single(pendingOnly);
        Assert.Equal("Chờ duyệt", pendingOnly[0].Status);

        // Lọc Đã xác nhận
        var confirmedOnly = await service.GetBookingsAsync(status: "Đã xác nhận");
        Assert.Single(confirmedOnly);
        Assert.Equal("Đã xác nhận", confirmedOnly[0].Status);
    }

    // 14. AMENITIES-01C: Cư dân hủy trước giờ bắt đầu -> Thành công
    [Fact]
    public async Task CancelBooking_BeforeStartTime_WhenResident_Succeeds()
    {
        using var context = CreateInMemoryDbContext();
        var (res1, _, amenity) = await SeedDataAsync(context);
        var service = new AmenityService(context);

        var bookingDate = DateTime.Today.AddDays(1);
        var startTime = new TimeSpan(14, 0, 0); // 14:00
        var endTime = new TimeSpan(16, 0, 0);   // 16:00

        await service.BookAmenityAsync(res1.UserId, amenity.AmenityId, bookingDate, startTime, endTime, 2, "Đặt trước");
        var booking = await context.AmenityBookings.FirstAsync();

        // Thời điểm hủy: 13:00 cùng ngày (trước giờ bắt đầu 1 tiếng)
        var simulatedNow = bookingDate.Date.AddHours(13);
        var (success, error) = await service.CancelBookingAsync(res1.UserId, booking.AmenityBookingId, isManager: false, currentDateTime: simulatedNow);

        Assert.True(success, error);
        var updated = await context.AmenityBookings.FindAsync(booking.AmenityBookingId);
        Assert.Equal("Đã hủy", updated!.Status);
    }

    // 15. AMENITIES-01C: Cư dân hủy đúng giờ bắt đầu -> Bị từ chối
    [Fact]
    public async Task CancelBooking_AtExactStartTime_WhenResident_IsRejected()
    {
        using var context = CreateInMemoryDbContext();
        var (res1, _, amenity) = await SeedDataAsync(context);
        var service = new AmenityService(context);

        var bookingDate = DateTime.Today.AddDays(1);
        var startTime = new TimeSpan(10, 0, 0); // 10:00:00
        var endTime = new TimeSpan(11, 0, 0);

        await service.BookAmenityAsync(res1.UserId, amenity.AmenityId, bookingDate, startTime, endTime, 2, null);
        var booking = await context.AmenityBookings.FirstAsync();

        // Thời điểm hủy: đúng 10:00:00 (giờ bắt đầu)
        var exactStartTime = bookingDate.Date.Add(startTime);
        var (success, error) = await service.CancelBookingAsync(res1.UserId, booking.AmenityBookingId, isManager: false, currentDateTime: exactStartTime);

        Assert.False(success);
        Assert.Contains("Không thể hủy lịch đặt chỗ khi thời gian sử dụng đã bắt đầu hoặc đã qua", error);
        var untouched = await context.AmenityBookings.FindAsync(booking.AmenityBookingId);
        Assert.NotEqual("Đã hủy", untouched!.Status);
    }

    // 16. AMENITIES-01C: Cư dân hủy sau giờ bắt đầu trong cùng ngày -> Bị từ chối (Fix lỗi REV-AM-01)
    [Fact]
    public async Task CancelBooking_AfterStartTime_SameDay_WhenResident_IsRejected()
    {
        using var context = CreateInMemoryDbContext();
        var (res1, _, amenity) = await SeedDataAsync(context);
        var service = new AmenityService(context);

        var bookingDate = DateTime.Today.AddDays(1);
        var startTime = new TimeSpan(8, 0, 0); // 08:00 sáng
        var endTime = new TimeSpan(9, 0, 0);   // 09:00 sáng

        await service.BookAmenityAsync(res1.UserId, amenity.AmenityId, bookingDate, startTime, endTime, 2, null);
        var booking = await context.AmenityBookings.FirstAsync();

        // Kịch bản REV-AM-01: Đến 15:00 chiều cùng ngày mới bấm Hủy
        var afternoonSameDay = bookingDate.Date.AddHours(15);
        var (success, error) = await service.CancelBookingAsync(res1.UserId, booking.AmenityBookingId, isManager: false, currentDateTime: afternoonSameDay);

        Assert.False(success);
        Assert.Contains("Không thể hủy lịch đặt chỗ khi thời gian sử dụng đã bắt đầu hoặc đã qua", error);
        var untouched = await context.AmenityBookings.FindAsync(booking.AmenityBookingId);
        Assert.NotEqual("Đã hủy", untouched!.Status);
    }

    // 17. AMENITIES-01C: Ban quản lý vẫn có quyền hủy sau giờ bắt đầu nếu cần thiết
    [Fact]
    public async Task CancelBooking_AfterStartTime_WhenManager_Succeeds()
    {
        using var context = CreateInMemoryDbContext();
        var (res1, _, amenity) = await SeedDataAsync(context);
        var service = new AmenityService(context);

        var bookingDate = DateTime.Today.AddDays(1);
        var startTime = new TimeSpan(8, 0, 0);
        var endTime = new TimeSpan(9, 0, 0);

        await service.BookAmenityAsync(res1.UserId, amenity.AmenityId, bookingDate, startTime, endTime, 2, null);
        var booking = await context.AmenityBookings.FirstAsync();

        // BQL can thiệp hủy lịch lúc 15:00 cùng ngày (ví dụ xử lý sự cố thiết bị)
        var afternoonSameDay = bookingDate.Date.AddHours(15);
        var (success, error) = await service.CancelBookingAsync("manager-id", booking.AmenityBookingId, isManager: true, currentDateTime: afternoonSameDay);

        Assert.True(success, error);
        var updated = await context.AmenityBookings.FindAsync(booking.AmenityBookingId);
        Assert.Equal("Đã hủy", updated!.Status);
    }

    // 18. AMENITIES-01C: Cư dân hủy lịch ngày hôm trước (quá khứ) -> Bị từ chối
    [Fact]
    public async Task CancelBooking_PastDate_WhenResident_IsRejected()
    {
        using var context = CreateInMemoryDbContext();
        var (res1, _, amenity) = await SeedDataAsync(context);
        var service = new AmenityService(context);

        // Tạo thủ công booking của ngày hôm qua
        var pastBooking = new AmenityBooking
        {
            AmenityId = amenity.AmenityId,
            ResidentId = res1.ResidentId,
            BookingDate = DateTime.Today.AddDays(-1),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            NumberOfGuests = 1,
            Status = "Đã xác nhận",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-2)
        };
        context.AmenityBookings.Add(pastBooking);
        await context.SaveChangesAsync();

        var (success, error) = await service.CancelBookingAsync(res1.UserId, pastBooking.AmenityBookingId, isManager: false);
        Assert.False(success);
        Assert.Contains("Không thể hủy lịch đặt chỗ khi thời gian sử dụng đã bắt đầu hoặc đã qua", error);
    }
}

