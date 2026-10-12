using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ApartmentManagement.Tests
{
    public class OccupancyServiceTests
    {
        private ApplicationDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private async Task<(Building building, Apartment apt, Resident res)> SeedDataAsync(ApplicationDbContext context)
        {
            var building = new Building
            {
                BuildingCode = "BLD-OCC",
                BuildingName = "Tòa Cư Trú",
                NumberOfFloors = 10
            };
            context.Buildings.Add(building);
            await context.SaveChangesAsync();

            var apt = new Apartment
            {
                BuildingId = building.BuildingId,
                ApartmentCode = "OCC-101",
                Floor = 1,
                Area = 65m,
                Status = "Trống"
            };
            context.Apartments.Add(apt);
            await context.SaveChangesAsync();

            var user = new ApplicationUser
            {
                Id = "user-occ",
                UserName = "res_occ",
                Email = "occ@apartment.local",
                FullName = "Nguyễn Văn Cư Trú"
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var res = new Resident
            {
                UserId = user.Id,
                CitizenId = "034567890123"
            };
            context.Residents.Add(res);
            await context.SaveChangesAsync();

            return (building, apt, res);
        }

        [Fact]
        public async Task MoveIn_Today_Succeeds()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt, res) = await SeedDataAsync(context);
            var service = new OccupancyService(context, NullLogger<OccupancyService>.Instance);

            var model = new MoveInViewModel
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res.ResidentId,
                Relationship = "Chủ hộ",
                MoveInDate = OccupancyService.GetApplicationToday(),
                IsOwner = true
            };

            var (success, error, occupancy) = await service.MoveInAsync(model);
            Assert.True(success, error);
            Assert.Null(error);
            Assert.NotNull(occupancy);
            Assert.Equal("Đang sử dụng", apt.Status);
        }

        [Fact]
        public async Task MoveIn_ValidPastDate_Succeeds()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt, res) = await SeedDataAsync(context);
            var service = new OccupancyService(context, NullLogger<OccupancyService>.Instance);

            var model = new MoveInViewModel
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res.ResidentId,
                Relationship = "Thành viên",
                MoveInDate = new DateTime(2025, 6, 1),
                IsOwner = false
            };

            var (success, error, _) = await service.MoveInAsync(model);
            Assert.True(success, error);
            Assert.Null(error);
        }

        [Fact]
        public async Task MoveIn_FutureDate_IsRejected()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt, res) = await SeedDataAsync(context);
            var service = new OccupancyService(context, NullLogger<OccupancyService>.Instance);

            var model = new MoveInViewModel
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res.ResidentId,
                Relationship = "Chủ hộ",
                MoveInDate = OccupancyService.GetApplicationToday().AddDays(1),
                IsOwner = true
            };

            var (success, error, _) = await service.MoveInAsync(model);
            Assert.False(success);
            Assert.Equal("Ngày chuyển vào không thể ở tương lai.", error);
        }

        [Fact]
        public async Task MoveIn_DateBefore1900_IsRejected()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt, res) = await SeedDataAsync(context);
            var service = new OccupancyService(context, NullLogger<OccupancyService>.Instance);

            var model = new MoveInViewModel
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res.ResidentId,
                Relationship = "Chủ hộ",
                MoveInDate = new DateTime(1899, 12, 31),
                IsOwner = true
            };

            var (success, error, _) = await service.MoveInAsync(model);
            Assert.False(success);
            Assert.Equal("Ngày chuyển vào phải từ 01/01/1900.", error);
        }

        [Fact]
        public async Task MoveOut_Today_SucceedsAndSetsVacantWhenNoOtherResidents()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt, res) = await SeedDataAsync(context);
            var service = new OccupancyService(context, NullLogger<OccupancyService>.Instance);

            var moveInResult = await service.MoveInAsync(new MoveInViewModel
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res.ResidentId,
                Relationship = "Chủ hộ",
                MoveInDate = OccupancyService.GetApplicationToday().AddDays(-5),
                IsOwner = true
            });
            Assert.True(moveInResult.Success);

            var (moveOutSuccess, moveOutError) = await service.MoveOutAsync(
                moveInResult.Occupancy!.ApartmentResidentId,
                OccupancyService.GetApplicationToday());

            Assert.True(moveOutSuccess, moveOutError);
            Assert.Null(moveOutError);
            Assert.Equal("Trống", apt.Status);
        }

        [Fact]
        public async Task MoveOut_FutureDate_IsRejected()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt, res) = await SeedDataAsync(context);
            var service = new OccupancyService(context, NullLogger<OccupancyService>.Instance);

            var moveInResult = await service.MoveInAsync(new MoveInViewModel
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res.ResidentId,
                Relationship = "Chủ hộ",
                MoveInDate = OccupancyService.GetApplicationToday(),
                IsOwner = true
            });
            Assert.True(moveInResult.Success);

            var (moveOutSuccess, moveOutError) = await service.MoveOutAsync(
                moveInResult.Occupancy!.ApartmentResidentId,
                OccupancyService.GetApplicationToday().AddDays(1));

            Assert.False(moveOutSuccess);
            Assert.Equal("Ngày chuyển đi không thể ở tương lai.", moveOutError);
        }

        [Fact]
        public async Task MoveOut_BeforeMoveInDate_IsRejected()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt, res) = await SeedDataAsync(context);
            var service = new OccupancyService(context, NullLogger<OccupancyService>.Instance);

            var moveInResult = await service.MoveInAsync(new MoveInViewModel
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res.ResidentId,
                Relationship = "Chủ hộ",
                MoveInDate = OccupancyService.GetApplicationToday(),
                IsOwner = true
            });
            Assert.True(moveInResult.Success);

            var (moveOutSuccess, moveOutError) = await service.MoveOutAsync(
                moveInResult.Occupancy!.ApartmentResidentId,
                OccupancyService.GetApplicationToday().AddDays(-1));

            Assert.False(moveOutSuccess);
            Assert.Equal("Ngày chuyển đi không thể trước ngày chuyển vào.", moveOutError);
        }
    }
}
