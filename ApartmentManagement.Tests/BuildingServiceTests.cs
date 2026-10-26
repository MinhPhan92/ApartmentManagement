using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ApartmentManagement.Tests
{
    public class BuildingServiceTests
    {
        private ApplicationDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task CreateBuilding_NormalizesBuildingCode_ToUpperCaseAndTrimmed()
        {
            using var context = CreateInMemoryDbContext();
            var service = new BuildingService(context, NullLogger<BuildingService>.Instance);

            var building = new Building
            {
                BuildingCode = "  bld-alpha  ",
                BuildingName = "  Tòa Tháp Alpha  ",
                Address = "  123 Nguyễn Văn Linh  ",
                NumberOfFloors = 15
            };

            var (success, error, created) = await service.CreateBuildingAsync(building);

            Assert.True(success);
            Assert.Null(error);
            Assert.NotNull(created);
            Assert.Equal("BLD-ALPHA", created.BuildingCode);
            Assert.Equal("Tòa Tháp Alpha", created.BuildingName);
            Assert.Equal("123 Nguyễn Văn Linh", created.Address);
        }

        [Fact]
        public async Task CreateBuilding_Fails_WhenBuildingCodeAlreadyExists()
        {
            using var context = CreateInMemoryDbContext();
            var service = new BuildingService(context, NullLogger<BuildingService>.Instance);

            var existing = new Building
            {
                BuildingCode = "T01",
                BuildingName = "Tòa 1",
                NumberOfFloors = 10
            };
            context.Buildings.Add(existing);
            await context.SaveChangesAsync();

            var duplicate = new Building
            {
                BuildingCode = "t01", // lowercase should match
                BuildingName = "Tòa 1 Mới",
                NumberOfFloors = 12
            };

            var (success, error, created) = await service.CreateBuildingAsync(duplicate);

            Assert.False(success);
            Assert.NotNull(error);
            Assert.Contains("đã tồn tại", error);
            Assert.Null(created);
        }

        [Fact]
        public async Task UpdateBuilding_Fails_WhenBuildingDoesNotExist()
        {
            using var context = CreateInMemoryDbContext();
            var service = new BuildingService(context, NullLogger<BuildingService>.Instance);

            var nonExistent = new Building
            {
                BuildingId = 9999,
                BuildingCode = "T99",
                BuildingName = "Tòa 99",
                NumberOfFloors = 10
            };

            var (success, error) = await service.UpdateBuildingAsync(nonExistent);

            Assert.False(success);
            Assert.Equal("Không tìm thấy tòa nhà cần cập nhật.", error);
        }

        [Fact]
        public async Task UpdateBuilding_Fails_WhenBuildingCodeDuplicatesAnotherBuilding()
        {
            using var context = CreateInMemoryDbContext();
            var service = new BuildingService(context, NullLogger<BuildingService>.Instance);

            var bldA = new Building { BuildingCode = "BLD-A", BuildingName = "Tòa A", NumberOfFloors = 10 };
            var bldB = new Building { BuildingCode = "BLD-B", BuildingName = "Tòa B", NumberOfFloors = 10 };
            context.Buildings.AddRange(bldA, bldB);
            await context.SaveChangesAsync();

            bldB.BuildingCode = "bld-a"; // Try to rename B to A

            var (success, error) = await service.UpdateBuildingAsync(bldB);

            Assert.False(success);
            Assert.Contains("đã được sử dụng bởi tòa nhà khác", error);
        }

        [Fact]
        public async Task UpdateBuilding_Fails_WhenNewNumberOfFloorsIsLessThanHighestExistingApartmentFloor()
        {
            using var context = CreateInMemoryDbContext();
            var service = new BuildingService(context, NullLogger<BuildingService>.Instance);

            var building = new Building
            {
                BuildingCode = "BLD-TOWER",
                BuildingName = "Tòa Tower",
                NumberOfFloors = 20
            };
            context.Buildings.Add(building);
            await context.SaveChangesAsync();

            var aptOnFloor15 = new Apartment
            {
                BuildingId = building.BuildingId,
                ApartmentCode = "T-1501",
                Floor = 15,
                Area = 70m,
                Status = ApartmentStatus.Vacant
            };
            context.Apartments.Add(aptOnFloor15);
            await context.SaveChangesAsync();

            // Try to reduce number of floors to 10 (less than 15) using a separate update model (as MVC binds)
            var updateModel = new Building
            {
                BuildingId = building.BuildingId,
                BuildingCode = building.BuildingCode,
                BuildingName = building.BuildingName,
                NumberOfFloors = 10
            };

            var (success, error) = await service.UpdateBuildingAsync(updateModel);

            Assert.False(success);
            Assert.NotNull(error);
            Assert.Contains("10", error);
            Assert.Contains("15", error);
            Assert.Contains("đang có căn hộ ở tầng 15", error);

            // Verify database was NOT mutated
            var refreshed = await context.Buildings.AsNoTracking().FirstOrDefaultAsync(b => b.BuildingId == building.BuildingId);
            Assert.Equal(20, refreshed!.NumberOfFloors);
        }

        [Fact]
        public async Task UpdateBuilding_Success_WhenNewNumberOfFloorsEqualsHighestExistingApartmentFloor()
        {
            using var context = CreateInMemoryDbContext();
            var service = new BuildingService(context, NullLogger<BuildingService>.Instance);

            var building = new Building
            {
                BuildingCode = "BLD-EQ",
                BuildingName = "Tòa Equal",
                NumberOfFloors = 20
            };
            context.Buildings.Add(building);
            await context.SaveChangesAsync();

            var aptOnFloor15 = new Apartment
            {
                BuildingId = building.BuildingId,
                ApartmentCode = "EQ-1501",
                Floor = 15,
                Area = 70m,
                Status = ApartmentStatus.Vacant
            };
            context.Apartments.Add(aptOnFloor15);
            await context.SaveChangesAsync();

            // Set floors exactly to 15 (equal to highest existing apartment)
            building.NumberOfFloors = 15;

            var (success, error) = await service.UpdateBuildingAsync(building);

            Assert.True(success);
            Assert.Null(error);

            var refreshed = await context.Buildings.FindAsync(building.BuildingId);
            Assert.Equal(15, refreshed!.NumberOfFloors);
        }

        [Fact]
        public async Task UpdateBuilding_Success_WhenBuildingHasNoApartments()
        {
            using var context = CreateInMemoryDbContext();
            var service = new BuildingService(context, NullLogger<BuildingService>.Instance);

            var building = new Building
            {
                BuildingCode = "BLD-EMPTY",
                BuildingName = "Tòa Trống",
                NumberOfFloors = 20
            };
            context.Buildings.Add(building);
            await context.SaveChangesAsync();

            // Can change to any valid floor count when no apartments exist
            building.NumberOfFloors = 5;

            var (success, error) = await service.UpdateBuildingAsync(building);

            Assert.True(success);
            Assert.Null(error);

            var refreshed = await context.Buildings.FindAsync(building.BuildingId);
            Assert.Equal(5, refreshed!.NumberOfFloors);
        }

        [Fact]
        public async Task UpdateBuilding_Success_WhenNewNumberOfFloorsIsHigherThanHighestApartmentFloor()
        {
            using var context = CreateInMemoryDbContext();
            var service = new BuildingService(context, NullLogger<BuildingService>.Instance);

            var building = new Building
            {
                BuildingCode = "BLD-UP",
                BuildingName = "Tòa Up",
                NumberOfFloors = 15
            };
            context.Buildings.Add(building);
            await context.SaveChangesAsync();

            var aptOnFloor12 = new Apartment
            {
                BuildingId = building.BuildingId,
                ApartmentCode = "UP-1201",
                Floor = 12,
                Area = 70m,
                Status = ApartmentStatus.Vacant
            };
            context.Apartments.Add(aptOnFloor12);
            await context.SaveChangesAsync();

            // Increase to 25
            building.NumberOfFloors = 25;

            var (success, error) = await service.UpdateBuildingAsync(building);

            Assert.True(success);
            Assert.Null(error);

            var refreshed = await context.Buildings.FindAsync(building.BuildingId);
            Assert.Equal(25, refreshed!.NumberOfFloors);
        }

        [Fact]
        public async Task DeleteBuilding_Fails_WhenBuildingDoesNotExist()
        {
            using var context = CreateInMemoryDbContext();
            var service = new BuildingService(context, NullLogger<BuildingService>.Instance);

            var (success, error) = await service.DeleteBuildingAsync(9999);

            Assert.False(success);
            Assert.Equal("Không tìm thấy tòa nhà cần xóa.", error);
        }

        [Fact]
        public async Task DeleteBuilding_Fails_WhenApartmentsExist_AndPreservesData()
        {
            using var context = CreateInMemoryDbContext();
            var service = new BuildingService(context, NullLogger<BuildingService>.Instance);

            var building = new Building
            {
                BuildingCode = "BLD-APT",
                BuildingName = "Tòa Có Căn Hộ",
                NumberOfFloors = 10
            };
            context.Buildings.Add(building);
            await context.SaveChangesAsync();

            var apartment = new Apartment
            {
                BuildingId = building.BuildingId,
                ApartmentCode = "APT-101",
                Floor = 1,
                Area = 60m,
                Status = ApartmentStatus.Vacant
            };
            context.Apartments.Add(apartment);
            await context.SaveChangesAsync();

            var (success, error) = await service.DeleteBuildingAsync(building.BuildingId);

            Assert.False(success);
            Assert.NotNull(error);
            Assert.Contains("căn hộ trực thuộc", error);

            // Verify building and apartment still exist intact
            Assert.NotNull(await context.Buildings.FindAsync(building.BuildingId));
            Assert.NotNull(await context.Apartments.FindAsync(apartment.ApartmentId));
        }

        [Fact]
        public async Task DeleteBuilding_Fails_WhenFeeTariffsExist_AndPreservesData()
        {
            using var context = CreateInMemoryDbContext();
            var service = new BuildingService(context, NullLogger<BuildingService>.Instance);

            var building = new Building
            {
                BuildingCode = "BLD-TARIFF",
                BuildingName = "Tòa Có Biểu Phí",
                NumberOfFloors = 10
            };
            context.Buildings.Add(building);
            await context.SaveChangesAsync();

            var tariff = new FeeTariff
            {
                BuildingId = building.BuildingId,
                ChargeType = FeeChargeType.Management,
                EffectiveFrom = DateTime.UtcNow.Date,
                MonthlyRatePerSquareMeter = 12000m,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = "admin-1"
            };
            context.FeeTariffs.Add(tariff);
            await context.SaveChangesAsync();

            var (success, error) = await service.DeleteBuildingAsync(building.BuildingId);

            Assert.False(success);
            Assert.NotNull(error);
            Assert.Contains("biểu phí áp dụng", error);

            // Verify building and tariff still exist intact
            Assert.NotNull(await context.Buildings.FindAsync(building.BuildingId));
            Assert.NotNull(await context.FeeTariffs.FindAsync(tariff.FeeTariffId));
        }

        [Fact]
        public async Task DeleteBuilding_Fails_WhenAnnouncementsExist_AndPreservesData()
        {
            using var context = CreateInMemoryDbContext();
            var service = new BuildingService(context, NullLogger<BuildingService>.Instance);

            var building = new Building
            {
                BuildingCode = "BLD-ANN",
                BuildingName = "Tòa Có Thông Báo",
                NumberOfFloors = 10
            };
            context.Buildings.Add(building);
            await context.SaveChangesAsync();

            var announcement = new Announcement
            {
                BuildingId = building.BuildingId,
                Title = "Thông báo diễn tập PCCC",
                Content = "Tổ chức vào ngày mai tại sảnh",
                Category = "Thông báo chung",
                CreatedAtUtc = DateTime.UtcNow
            };
            context.Announcements.Add(announcement);
            await context.SaveChangesAsync();

            var (success, error) = await service.DeleteBuildingAsync(building.BuildingId);

            Assert.False(success);
            Assert.NotNull(error);
            Assert.Contains("thông báo liên kết trực tiếp", error);

            // Verify building and announcement still exist intact
            Assert.NotNull(await context.Buildings.FindAsync(building.BuildingId));
            Assert.NotNull(await context.Announcements.FindAsync(announcement.AnnouncementId));
        }

        [Fact]
        public async Task DeleteBuilding_Success_WhenNoApartmentsTariffsOrAnnouncements()
        {
            using var context = CreateInMemoryDbContext();
            var service = new BuildingService(context, NullLogger<BuildingService>.Instance);

            var building = new Building
            {
                BuildingCode = "BLD-CLEAN",
                BuildingName = "Tòa Sạch Rỗng",
                NumberOfFloors = 10
            };
            context.Buildings.Add(building);
            await context.SaveChangesAsync();

            var (success, error) = await service.DeleteBuildingAsync(building.BuildingId);

            Assert.True(success);
            Assert.Null(error);

            // Verify building is removed
            Assert.Null(await context.Buildings.FindAsync(building.BuildingId));
        }

        [Fact]
        public async Task GetAllBuildings_SearchTermFilters_ByCodeNameOrAddress()
        {
            using var context = CreateInMemoryDbContext();
            var service = new BuildingService(context, NullLogger<BuildingService>.Instance);

            var b1 = new Building { BuildingCode = "T-A1", BuildingName = "Tháp Ruby", Address = "Khu Đông", NumberOfFloors = 10 };
            var b2 = new Building { BuildingCode = "T-B2", BuildingName = "Tháp Diamond", Address = "Khu Tây", NumberOfFloors = 15 };
            var b3 = new Building { BuildingCode = "T-C3", BuildingName = "Tháp Emerald", Address = "Khu Nam", NumberOfFloors = 20 };
            context.Buildings.AddRange(b1, b2, b3);
            await context.SaveChangesAsync();

            var byCode = await service.GetAllBuildingsAsync("a1");
            Assert.Single(byCode);
            Assert.Equal("T-A1", byCode[0].BuildingCode);

            var byName = await service.GetAllBuildingsAsync("diamond");
            Assert.Single(byName);
            Assert.Equal("T-B2", byName[0].BuildingCode);

            var byAddress = await service.GetAllBuildingsAsync("Khu Nam");
            Assert.Single(byAddress);
            Assert.Equal("T-C3", byAddress[0].BuildingCode);
        }
    }
}
