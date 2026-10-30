using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ApartmentManagement.Tests;

public sealed class ParkingFeeCalculationTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static (Building building, Apartment apartment, Resident resident) SeedBaseData(
        ApplicationDbContext context,
        int buildingId = 1,
        int apartmentId = 101)
    {
        var user = new ApplicationUser
        {
            Id = $"user-{apartmentId}",
            UserName = $"resident{apartmentId}@test.local",
            Email = $"resident{apartmentId}@test.local",
            FullName = "Nguyen Van A",
            IsActive = true
        };

        var resident = new Resident
        {
            ResidentId = apartmentId,
            UserId = user.Id,
            User = user,
            CitizenId = $"0012026{apartmentId:D5}"
        };

        var building = new Building
        {
            BuildingId = buildingId,
            BuildingCode = $"BLD-{buildingId}",
            BuildingName = $"Tòa {buildingId}",
            Address = "123 Đường Test",
            NumberOfFloors = 10
        };

        var apartment = new Apartment
        {
            ApartmentId = apartmentId,
            BuildingId = buildingId,
            Building = building,
            ApartmentCode = $"A-{apartmentId}",
            Floor = 1,
            Area = 75.0m,
            Status = "Đang sử dụng"
        };

        context.AddRange(user, resident, building, apartment);
        context.SaveChanges();

        return (building, apartment, resident);
    }

    [Fact]
    public async Task CalculateApartmentParkingFeeAsync_WhenNoVehicles_ReturnsZeroAndNoRegisteredVehiclesMessage()
    {
        using var context = CreateContext();
        var (_, apartment, _) = SeedBaseData(context);
        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var result = await service.CalculateApartmentParkingFeeAsync(
            apartment.ApartmentId,
            apartment.BuildingId,
            2026,
            10);

        Assert.Equal(0m, result.TotalAmount);
        Assert.Equal(0, result.ActiveVehicleCount);
        Assert.Equal("Không có phương tiện đăng ký", result.SummaryDescription);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task CalculateApartmentParkingFeeAsync_WhenActiveVehiclesExist_CalculatesCorrectRatesAndSummary()
    {
        using var context = CreateContext();
        var (_, apartment, resident) = SeedBaseData(context);

        var car = new Vehicle
        {
            VehicleId = 1,
            ApartmentId = apartment.ApartmentId,
            ResidentId = resident.ResidentId,
            VehicleType = "Ô tô 7 chỗ",
            LicensePlate = "30A-12345",
            Status = "Đã duyệt",
            CreatedAt = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var motorbike1 = new Vehicle
        {
            VehicleId = 2,
            ApartmentId = apartment.ApartmentId,
            ResidentId = resident.ResidentId,
            VehicleType = "Xe máy Honda",
            LicensePlate = "29B1-99999",
            Status = "Đã duyệt",
            CreatedAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var motorbike2 = new Vehicle
        {
            VehicleId = 3,
            ApartmentId = apartment.ApartmentId,
            ResidentId = resident.ResidentId,
            VehicleType = "Xe máy Honda",
            LicensePlate = "29B1-88888",
            Status = "Đã duyệt",
            CreatedAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var bike = new Vehicle
        {
            VehicleId = 4,
            ApartmentId = apartment.ApartmentId,
            ResidentId = resident.ResidentId,
            VehicleType = "Xe đạp",
            LicensePlate = "XD-01",
            Status = "Đã duyệt",
            CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        context.Vehicles.AddRange(car, motorbike1, motorbike2, bike);
        await context.SaveChangesAsync();

        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var result = await service.CalculateApartmentParkingFeeAsync(
            apartment.ApartmentId,
            apartment.BuildingId,
            2026,
            10);

        // Ô tô = 1,200,000; Xe máy Honda x2 = 200,000; Xe đạp = 50,000 => Tổng 1,450,000
        Assert.Equal(1_450_000m, result.TotalAmount);
        Assert.Equal(4, result.ActiveVehicleCount);
        Assert.Equal(4, result.Items.Count);

        Assert.Contains("Phí gửi xe tháng 10/2026:", result.SummaryDescription);
        Assert.Contains("1 Ô tô 7 chỗ", result.SummaryDescription);
        Assert.Contains("2 Xe máy Honda", result.SummaryDescription);
        Assert.Contains("1 Xe đạp", result.SummaryDescription);
    }

    [Fact]
    public async Task CalculateApartmentParkingFeeAsync_WhenVehicleStatusNotApprovedAndNoActiveCard_IgnoresVehicle()
    {
        using var context = CreateContext();
        var (_, apartment, resident) = SeedBaseData(context);

        var pendingVehicle = new Vehicle
        {
            VehicleId = 1,
            ApartmentId = apartment.ApartmentId,
            ResidentId = resident.ResidentId,
            VehicleType = "Xe máy",
            LicensePlate = "29X1-11111",
            Status = "Chờ duyệt",
            CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var rejectedVehicleWithLockedCard = new Vehicle
        {
            VehicleId = 2,
            ApartmentId = apartment.ApartmentId,
            ResidentId = resident.ResidentId,
            VehicleType = "Ô tô",
            LicensePlate = "30A-99999",
            Status = "Từ chối",
            CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            ParkingCard = new ParkingCard
            {
                ParkingCardId = 20,
                CardCode = "CARD-LOCKED",
                Status = "Locked"
            }
        };

        context.Vehicles.AddRange(pendingVehicle, rejectedVehicleWithLockedCard);
        await context.SaveChangesAsync();

        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var result = await service.CalculateApartmentParkingFeeAsync(
            apartment.ApartmentId,
            apartment.BuildingId,
            2026,
            10);

        Assert.Equal(0m, result.TotalAmount);
        Assert.Equal(0, result.ActiveVehicleCount);
        Assert.Equal("Không có phương tiện đăng ký", result.SummaryDescription);
    }

    [Fact]
    public async Task CalculateApartmentParkingFeeAsync_WhenVehicleCreatedAfterPeriodEnd_IgnoresFutureVehicle()
    {
        using var context = CreateContext();
        var (_, apartment, resident) = SeedBaseData(context);

        // Created in Nov 2026, while billing period is Oct 2026
        var futureVehicle = new Vehicle
        {
            VehicleId = 1,
            ApartmentId = apartment.ApartmentId,
            ResidentId = resident.ResidentId,
            VehicleType = "Ô tô",
            LicensePlate = "30A-FUTURE",
            Status = "Đã duyệt",
            CreatedAt = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc)
        };

        context.Vehicles.Add(futureVehicle);
        await context.SaveChangesAsync();

        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var result = await service.CalculateApartmentParkingFeeAsync(
            apartment.ApartmentId,
            apartment.BuildingId,
            2026,
            10);

        Assert.Equal(0m, result.TotalAmount);
        Assert.Equal(0, result.ActiveVehicleCount);
        Assert.Equal("Không có phương tiện đăng ký", result.SummaryDescription);
    }

    [Fact]
    public async Task CalculateApartmentParkingFeeAsync_WhenVehicleHasActiveParkingCardEvenIfStatusNotApproved_IsIncluded()
    {
        using var context = CreateContext();
        var (_, apartment, resident) = SeedBaseData(context);

        var vehicleWithActiveCard = new Vehicle
        {
            VehicleId = 1,
            ApartmentId = apartment.ApartmentId,
            ResidentId = resident.ResidentId,
            VehicleType = "Xe máy điện",
            LicensePlate = "29MD-12345",
            Status = "Chờ duyệt",
            CreatedAt = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc),
            ParkingCard = new ParkingCard
            {
                ParkingCardId = 10,
                CardCode = "CARD-ACTIVE-01",
                Status = "Active"
            }
        };

        context.Vehicles.Add(vehicleWithActiveCard);
        await context.SaveChangesAsync();

        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var result = await service.CalculateApartmentParkingFeeAsync(
            apartment.ApartmentId,
            apartment.BuildingId,
            2026,
            10);

        // "Xe máy điện" contains "điện" (does not contain "ô tô", "oto", "car", "đạp", "bicycle") => 100,000 VND
        Assert.Equal(100_000m, result.TotalAmount);
        Assert.Equal(1, result.ActiveVehicleCount);
        Assert.Single(result.Items);
        Assert.Equal("Xe máy điện", result.Items[0].VehicleType);
    }

    [Fact]
    public async Task CalculateApartmentParkingFeeAsync_WhenVehicleTypeIsEmptyOrWhitespace_DefaultsToXeMay100k()
    {
        using var context = CreateContext();
        var (_, apartment, resident) = SeedBaseData(context);

        var emptyTypeVehicle = new Vehicle
        {
            VehicleId = 1,
            ApartmentId = apartment.ApartmentId,
            ResidentId = resident.ResidentId,
            VehicleType = "   ",
            LicensePlate = "29X1-EMPTY",
            Status = "Đã duyệt",
            CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        context.Vehicles.Add(emptyTypeVehicle);
        await context.SaveChangesAsync();

        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var result = await service.CalculateApartmentParkingFeeAsync(
            apartment.ApartmentId,
            apartment.BuildingId,
            2026,
            10);

        Assert.Equal(100_000m, result.TotalAmount);
        Assert.Equal(1, result.ActiveVehicleCount);
        Assert.Single(result.Items);
        Assert.Equal("Xe máy", result.Items[0].VehicleType);
        Assert.Equal(100_000m, result.Items[0].MonthlyRate);
    }

    [Fact]
    public async Task GenerateDraftAsync_WhenApartmentHasActiveVehicles_BundlesParkingFeeIntoManagementLine()
    {
        using var context = CreateContext();
        SeedFullBillingEnvironment(context);

        // Add 1 Car (1,200,000) and 1 Motorbike (100,000) => Parking total = 1,300,000
        context.Vehicles.AddRange(
            new Vehicle
            {
                VehicleId = 1,
                ApartmentId = 1,
                ResidentId = 1,
                VehicleType = "Ô tô",
                LicensePlate = "30A-88888",
                Status = "Đã duyệt",
                CreatedAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Vehicle
            {
                VehicleId = 2,
                ApartmentId = 1,
                ResidentId = 1,
                VehicleType = "Xe máy",
                LicensePlate = "29B1-12345",
                Status = "Đã duyệt",
                CreatedAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
        await context.SaveChangesAsync();

        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var result = await service.GenerateDraftAsync(1, 2026, 8, new DateTime(2026, 9, 10), "user-manager");

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Invoice);

        // Retains exactly 3 lines (Management, Electricity, Water) - zero DB Check Constraint violation
        Assert.Equal(3, result.Invoice.Lines.Count);

        var mgmtLine = result.Invoice.Lines.Single(l => l.ChargeType == FeeChargeType.Management);
        // Base management = 50 m² * 1000 = 50,000 + 1,300,000 parking fee = 1,350,000
        Assert.Equal(1_350_000m, mgmtLine.Amount);
        Assert.Contains("[Bao gồm] Phí gửi xe tháng 08/2026:", mgmtLine.Description);
        Assert.Contains("1 Ô tô", mgmtLine.Description);
        Assert.Contains("1 Xe máy", mgmtLine.Description);
        Assert.Contains("Phí gửi xe: 1,300,000 VND (2 phương tiện)", mgmtLine.RateBreakdown);

        // TotalAmount = Management (1,350,000) + Electricity (130,000) + Water (5,000) = 1,485,000
        Assert.Equal(1_485_000m, result.Invoice.TotalAmount);
    }

    [Fact]
    public async Task GenerateDraftAsync_WhenApartmentHasNoVehicles_DoesNotAlterManagementCharge()
    {
        using var context = CreateContext();
        SeedFullBillingEnvironment(context);

        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var result = await service.GenerateDraftAsync(1, 2026, 8, new DateTime(2026, 9, 10), "user-manager");

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Invoice);

        Assert.Equal(3, result.Invoice.Lines.Count);

        var mgmtLine = result.Invoice.Lines.Single(l => l.ChargeType == FeeChargeType.Management);
        Assert.Equal(50_000m, mgmtLine.Amount);
        Assert.DoesNotContain("[Bao gồm]", mgmtLine.Description);
        Assert.DoesNotContain("Phí gửi xe:", mgmtLine.RateBreakdown);

        // TotalAmount = 50,000 + 130,000 + 5,000 = 185,000
        Assert.Equal(185_000m, result.Invoice.TotalAmount);
    }

    private static void SeedFullBillingEnvironment(ApplicationDbContext context)
    {
        var user = new ApplicationUser
        {
            Id = "user-resident",
            UserName = "resident@test.local",
            Email = "resident@test.local",
            FullName = "Test Resident",
            IsActive = true
        };
        var manager = new ApplicationUser
        {
            Id = "user-manager",
            UserName = "manager@test.local",
            Email = "manager@test.local",
            FullName = "Test Manager",
            IsActive = true
        };
        var building = new Building
        {
            BuildingId = 1,
            BuildingCode = "BLD-1",
            BuildingName = "Tòa thử nghiệm",
            Address = "Test",
            NumberOfFloors = 10
        };
        var apartment = new Apartment
        {
            ApartmentId = 1,
            BuildingId = 1,
            Building = building,
            ApartmentCode = "A-101",
            Floor = 1,
            Area = 50,
            Status = "Đang sử dụng"
        };
        var resident = new Resident
        {
            ResidentId = 1,
            UserId = user.Id,
            User = user,
            CitizenId = "123456789"
        };
        var management = new FeeTariff
        {
            BuildingId = 1,
            Building = building,
            ChargeType = FeeChargeType.Management,
            EffectiveFrom = new DateTime(2026, 1, 1),
            MonthlyRatePerSquareMeter = 1000,
            CreatedByUserId = manager.Id,
            CreatedByUser = manager
        };
        var electricity = new FeeTariff
        {
            BuildingId = 1,
            Building = building,
            ChargeType = FeeChargeType.Electricity,
            EffectiveFrom = new DateTime(2026, 1, 1),
            CreatedByUserId = manager.Id,
            CreatedByUser = manager,
            Tiers =
            [
                new FeeTariffTier { TierOrder = 1, UpperConsumption = 50, UnitRate = 2000 },
                new FeeTariffTier { TierOrder = 2, UpperConsumption = null, UnitRate = 3000 }
            ]
        };
        var water = new FeeTariff
        {
            BuildingId = 1,
            Building = building,
            ChargeType = FeeChargeType.Water,
            EffectiveFrom = new DateTime(2026, 1, 1),
            CreatedByUserId = manager.Id,
            CreatedByUser = manager,
            Tiers = [new FeeTariffTier { TierOrder = 1, UpperConsumption = null, UnitRate = 500 }]
        };

        context.AddRange(user, manager, building, apartment, resident, management, electricity, water);
        context.ApartmentResidents.Add(new ApartmentResident
        {
            ApartmentId = 1,
            Apartment = apartment,
            ResidentId = 1,
            Resident = resident,
            Relationship = "Chủ hộ",
            MoveInDate = new DateTime(2026, 1, 1),
            IsOwner = true
        });
        context.UtilityMeterReadings.AddRange(
            new UtilityMeterReading
            {
                ApartmentId = 1,
                Apartment = apartment,
                UtilityType = FeeChargeType.Electricity,
                BillingYear = 2026,
                BillingMonth = 8,
                PreviousReading = 0,
                CurrentReading = 60,
                ReadAtUtc = DateTime.UtcNow,
                RecordedByUserId = manager.Id,
                RecordedByUser = manager
            },
            new UtilityMeterReading
            {
                ApartmentId = 1,
                Apartment = apartment,
                UtilityType = FeeChargeType.Water,
                BillingYear = 2026,
                BillingMonth = 8,
                PreviousReading = 0,
                CurrentReading = 10,
                ReadAtUtc = DateTime.UtcNow,
                RecordedByUserId = manager.Id,
                RecordedByUser = manager
            });
        context.SaveChanges();
    }
}
