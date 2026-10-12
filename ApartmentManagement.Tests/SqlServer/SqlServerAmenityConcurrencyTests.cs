using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ApartmentManagement.Tests.SqlServer;

public sealed class SqlServerAmenityConcurrencyTests
{
    [SqlServerFact]
    public async Task ConcurrentBooking_WhenCapacityLimited_DoesNotOverbook()
    {
        await using var database = SqlServerTestDatabase.Create();
        int amenityId;
        string userAId;
        string userBId;

        await using (var setup = database.CreateContext())
        {
            await setup.Database.MigrateAsync();

            var userA = new ApplicationUser
            {
                Id = "user-sql-a",
                UserName = "sqla@res.local",
                Email = "sqla@res.local",
                FullName = "Resident SQL A",
                EmailConfirmed = true,
                IsActive = true
            };
            var userB = new ApplicationUser
            {
                Id = "user-sql-b",
                UserName = "sqlb@res.local",
                Email = "sqlb@res.local",
                FullName = "Resident SQL B",
                EmailConfirmed = true,
                IsActive = true
            };
            setup.Users.AddRange(userA, userB);
            await setup.SaveChangesAsync();

            var resA = new Resident
            {
                UserId = userA.Id,
                CitizenId = "098765432111"
            };
            var resB = new Resident
            {
                UserId = userB.Id,
                CitizenId = "098765432112"
            };
            setup.Residents.AddRange(resA, resB);
            await setup.SaveChangesAsync();

            // Amenity capacity = 5
            var amenity = new Amenity
            {
                Name = $"Bể bơi VIP {Guid.NewGuid():N}"[..25],
                Capacity = 5,
                OpenTime = new TimeSpan(6, 0, 0),
                CloseTime = new TimeSpan(22, 0, 0),
                IsActive = true
            };
            setup.Amenities.Add(amenity);
            await setup.SaveChangesAsync();

            amenityId = amenity.AmenityId;
            userAId = userA.Id;
            userBId = userB.Id;
        }

        var bookingDate = OccupancyService.GetApplicationToday().AddDays(1);
        var startTime = new TimeSpan(10, 0, 0);
        var endTime = new TimeSpan(11, 0, 0);

        // Chạy 2 task đặt chỗ đồng thời, mỗi yêu cầu đặt 3 người (Tổng = 6 > Sức chứa 5)
        var task1 = Task.Run(async () =>
        {
            await using var context = database.CreateContext();
            var service = new AmenityService(context);
            return await service.BookAmenityAsync(userAId, amenityId, bookingDate, startTime, endTime, 3, "Booking 1");
        });

        var task2 = Task.Run(async () =>
        {
            await using var context = database.CreateContext();
            var service = new AmenityService(context);
            return await service.BookAmenityAsync(userBId, amenityId, bookingDate, startTime, endTime, 3, "Booking 2");
        });

        var results = await Task.WhenAll(task1, task2);

        // Chỉ có thể tối đa 1 yêu cầu thành công, không được cả 2 cùng thành công gây overbooking
        var successCount = results.Count(r => r.Success);
        Assert.True(successCount <= 1, $"Kỳ vọng tối đa 1 lượt thành công, nhưng có {successCount} lượt thành công (vượt sức chứa).");

        await using var verifyContext = database.CreateContext();
        var bookings = await verifyContext.AmenityBookings
            .Where(b => b.AmenityId == amenityId && b.BookingDate.Date == bookingDate.Date && b.Status != "Đã hủy")
            .ToListAsync();

        var totalGuests = bookings.Sum(b => b.NumberOfGuests);
        Assert.True(totalGuests <= 5, $"Tổng số người ({totalGuests}) vượt quá sức chứa (5).");
    }
}
