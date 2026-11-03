using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ApartmentManagement.Tests.SqlServer;

public sealed class SqlServerParkingCardConcurrencyTests
{
    [SqlServerFact]
    public async Task ConcurrentCardIssuance_WithIdenticalCardCode_EnforcesUniqueIndexAndAllowsOnlyOne()
    {
        await using var database = SqlServerTestDatabase.Create();
        int vehicle1Id;
        int vehicle2Id;

        await using (var setup = database.CreateContext())
        {
            await setup.Database.MigrateAsync();

            var building = new Building
            {
                BuildingCode = $"BLD-PK-{Guid.NewGuid():N}"[..10],
                BuildingName = "Tòa thử nghiệm thẻ xe",
                Address = "123 Đường Test",
                NumberOfFloors = 10
            };
            setup.Buildings.Add(building);
            await setup.SaveChangesAsync();

            var apartment = new Apartment
            {
                BuildingId = building.BuildingId,
                ApartmentCode = $"A-{Guid.NewGuid():N}"[..8],
                Floor = 2,
                Area = 60,
                Status = "Đang sử dụng"
            };
            setup.Apartments.Add(apartment);
            await setup.SaveChangesAsync();

            var user = new ApplicationUser
            {
                Id = $"user-pk-{Guid.NewGuid():N}",
                UserName = "pkresident@test.local",
                NormalizedUserName = "PKRESIDENT@TEST.LOCAL",
                Email = "pkresident@test.local",
                NormalizedEmail = "PKRESIDENT@TEST.LOCAL",
                FullName = "Cư dân thử nghiệm thẻ xe",
                EmailConfirmed = true,
                IsActive = true
            };
            var resident = new Resident
            {
                User = user,
                CitizenId = $"001{Guid.NewGuid():N}"[..12]
            };
            setup.AddRange(user, resident);
            await setup.SaveChangesAsync();

            var v1 = new Vehicle
            {
                ApartmentId = apartment.ApartmentId,
                ResidentId = resident.ResidentId,
                VehicleType = "Ô tô",
                LicensePlate = $"30A-{Guid.NewGuid():N}"[..10],
                Status = "Đã duyệt",
                CreatedAt = DateTime.UtcNow
            };
            var v2 = new Vehicle
            {
                ApartmentId = apartment.ApartmentId,
                ResidentId = resident.ResidentId,
                VehicleType = "Xe máy",
                LicensePlate = $"29B-{Guid.NewGuid():N}"[..10],
                Status = "Đã duyệt",
                CreatedAt = DateTime.UtcNow
            };
            setup.Vehicles.AddRange(v1, v2);
            await setup.SaveChangesAsync();

            vehicle1Id = v1.VehicleId;
            vehicle2Id = v2.VehicleId;
        }

        var sharedCardCode = $"CARD-CONCURRENT-{Guid.NewGuid():N}"[..20];

        // Hai nhân viên tại 2 quầy đồng thời cấp cùng mã thẻ cho 2 xe khác nhau
        var task1 = Task.Run(async () =>
        {
            await using var context = database.CreateContext();
            var service = new VehicleService(context);
            return await service.IssueOrReplaceCardAsync(vehicle1Id, sharedCardCode, "manager-1");
        });

        var task2 = Task.Run(async () =>
        {
            await using var context = database.CreateContext();
            var service = new VehicleService(context);
            return await service.IssueOrReplaceCardAsync(vehicle2Id, sharedCardCode, "manager-2");
        });

        var results = await Task.WhenAll(task1, task2);

        // Kết quả kỳ vọng:
        // 1. Đúng 1 yêu cầu thành công, 1 yêu cầu thất bại
        var successCount = results.Count(r => r.Success);
        var failureCount = results.Count(r => !r.Success);
        Assert.Equal(1, successCount);
        Assert.Equal(1, failureCount);

        // 2. Yêu cầu thất bại phải trả về thông báo lỗi nghiệp vụ rõ ràng (không văng 500)
        var failedResult = results.Single(r => !r.Success);
        Assert.Contains("đang được sử dụng cho một xe khác", failedResult.ErrorMessage);

        // 3. Database chỉ tồn tại đúng 1 bản ghi mang mã thẻ đó
        await using var verifyContext = database.CreateContext();
        var cards = await verifyContext.ParkingCards
            .Where(c => c.CardCode == sharedCardCode)
            .ToListAsync();

        Assert.Single(cards);
        Assert.Equal("Active", cards[0].Status);
    }
}

