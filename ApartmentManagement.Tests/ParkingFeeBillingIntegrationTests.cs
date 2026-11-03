using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ApartmentManagement.Tests;

public sealed class ParkingFeeBillingIntegrationTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static void SeedBillingData(ApplicationDbContext context)
    {
        var user = new ApplicationUser
        {
            Id = "user-resident",
            UserName = "resident@test.local",
            NormalizedUserName = "RESIDENT@TEST.LOCAL",
            Email = "resident@test.local",
            NormalizedEmail = "RESIDENT@TEST.LOCAL",
            FullName = "Nguyen Van A",
            IsActive = true
        };
        var manager = new ApplicationUser
        {
            Id = "user-manager",
            UserName = "manager@test.local",
            NormalizedUserName = "MANAGER@TEST.LOCAL",
            Email = "manager@test.local",
            NormalizedEmail = "MANAGER@TEST.LOCAL",
            FullName = "Test Manager",
            IsActive = true
        };
        var building = new Building
        {
            BuildingId = 1,
            BuildingCode = "BLD-1",
            BuildingName = "Tòa A",
            Address = "123 Đường Test",
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
        var managementTariff = new FeeTariff
        {
            BuildingId = 1,
            Building = building,
            ChargeType = FeeChargeType.Management,
            EffectiveFrom = new DateTime(2026, 1, 1),
            MonthlyRatePerSquareMeter = 1000,
            CreatedByUserId = manager.Id,
            CreatedByUser = manager
        };
        var electricityTariff = new FeeTariff
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
        var waterTariff = new FeeTariff
        {
            BuildingId = 1,
            Building = building,
            ChargeType = FeeChargeType.Water,
            EffectiveFrom = new DateTime(2026, 1, 1),
            CreatedByUserId = manager.Id,
            CreatedByUser = manager,
            Tiers = [new FeeTariffTier { TierOrder = 1, UpperConsumption = null, UnitRate = 500 }]
        };

        context.AddRange(user, manager, building, apartment, resident, managementTariff, electricityTariff, waterTariff);
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

    [Fact]
    public async Task GenerateDraft_WhenNoVehicles_RetainsStandardManagementFee()
    {
        await using var context = CreateContext();
        SeedBillingData(context);
        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var result = await service.GenerateDraftAsync(1, 2026, 8, new DateTime(2026, 9, 10), "user-resident");

        Assert.True(result.Success, result.ErrorMessage);
        var invoice = result.Invoice!;

        // 3 lines only: Management, Electricity, Water
        Assert.Equal(3, invoice.Lines.Count);
        Assert.All(invoice.Lines, l => Assert.Contains(l.ChargeType, new[] { FeeChargeType.Management, FeeChargeType.Electricity, FeeChargeType.Water }));

        var mgmtLine = invoice.Lines.Single(l => l.ChargeType == FeeChargeType.Management);
        Assert.Equal(50000m, mgmtLine.Amount); // 50m2 * 1,000 VND
        Assert.DoesNotContain("[Bao gồm]", mgmtLine.Description);

        // Electricity = 50*2000 + 10*3000 = 130,000
        // Water = 10*500 = 5,000
        // Total = 50,000 + 130,000 + 5,000 = 185,000
        Assert.Equal(185000m, invoice.TotalAmount);
        Assert.Equal(invoice.Lines.Sum(l => l.Amount), invoice.TotalAmount);
    }

    [Fact]
    public async Task GenerateDraft_WhenApartmentHasApprovedVehicles_AddsParkingFeeToManagementLine()
    {
        await using var context = CreateContext();
        SeedBillingData(context);

        // 1 car (1,200,000 VND) and 1 motorbike (100,000 VND)
        var car = new Vehicle
        {
            ApartmentId = 1,
            ResidentId = 1,
            VehicleType = "Ô tô",
            LicensePlate = "30A-111.11",
            Status = "Đã duyệt",
            CreatedAt = new DateTime(2026, 8, 1)
        };
        var motorbike = new Vehicle
        {
            ApartmentId = 1,
            ResidentId = 1,
            VehicleType = "Xe máy",
            LicensePlate = "29B-222.22",
            Status = "Đã duyệt",
            CreatedAt = new DateTime(2026, 8, 5)
        };
        // A pending vehicle that should NOT be charged
        var pendingVehicle = new Vehicle
        {
            ApartmentId = 1,
            ResidentId = 1,
            VehicleType = "Xe đạp",
            LicensePlate = "XD-01",
            Status = "Chờ duyệt",
            CreatedAt = new DateTime(2026, 8, 5)
        };

        context.Vehicles.AddRange(car, motorbike, pendingVehicle);
        await context.SaveChangesAsync();

        var service = new BillingService(context, NullLogger<BillingService>.Instance);
        var result = await service.GenerateDraftAsync(1, 2026, 8, new DateTime(2026, 9, 10), "user-resident");

        Assert.True(result.Success, result.ErrorMessage);
        var invoice = result.Invoice!;

        // Check constraint compliance: only 3 standard lines, no foreign ChargeType
        Assert.Equal(3, invoice.Lines.Count);
        Assert.All(invoice.Lines, l => Assert.Contains(l.ChargeType, new[] { FeeChargeType.Management, FeeChargeType.Electricity, FeeChargeType.Water }));

        // Management line = 50,000 base + 1,200,000 car + 100,000 motorbike = 1,350,000 VND
        var mgmtLine = invoice.Lines.Single(l => l.ChargeType == FeeChargeType.Management);
        Assert.Equal(1350000m, mgmtLine.Amount);

        // Description must describe the included parking details
        Assert.Contains("[Bao gồm] Phí gửi xe tháng 08/2026:", mgmtLine.Description);
        Assert.Contains("1 Ô tô", mgmtLine.Description);
        Assert.Contains("1 Xe máy", mgmtLine.Description);
        Assert.Contains("Phí gửi xe:", mgmtLine.RateBreakdown);
        Assert.Contains("2 phương tiện", mgmtLine.RateBreakdown);

        // Conservation of total amount
        var expectedTotal = 1350000m + 130000m + 5000m; // 1,485,000 VND
        Assert.Equal(expectedTotal, invoice.TotalAmount);
        Assert.Equal(invoice.Lines.Sum(l => l.Amount), invoice.TotalAmount);
    }

    [Fact]
    public async Task GenerateDraft_WhenVehicleHasActiveParkingCard_AddsParkingFeeToManagementLine()
    {
        await using var context = CreateContext();
        SeedBillingData(context);

        // Vehicle with ParkingCard Active and bicycle type
        var bike = new Vehicle
        {
            ApartmentId = 1,
            ResidentId = 1,
            VehicleType = "Xe đạp điện",
            LicensePlate = "XD-ELECTRIC",
            Status = "Chờ duyệt", // Status is pending, but card is active
            CreatedAt = new DateTime(2026, 7, 1),
            ParkingCard = new ParkingCard
            {
                CardCode = "CARD-001",
                Status = "Active",
                IssuedAtUtc = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        };
        context.Vehicles.Add(bike);
        await context.SaveChangesAsync();

        var service = new BillingService(context, NullLogger<BillingService>.Instance);
        var result = await service.GenerateDraftAsync(1, 2026, 8, new DateTime(2026, 9, 10), "user-resident");

        Assert.True(result.Success, result.ErrorMessage);
        var invoice = result.Invoice!;

        var mgmtLine = invoice.Lines.Single(l => l.ChargeType == FeeChargeType.Management);
        // Base 50,000 + Bicycle Rate 50,000 = 100,000
        Assert.Equal(100000m, mgmtLine.Amount);
        Assert.Contains("[Bao gồm] Phí gửi xe tháng 08/2026:", mgmtLine.Description);
        Assert.Contains("1 Xe đạp điện", mgmtLine.Description);

        // Total = 100,000 + 130,000 + 5,000 = 235,000
        Assert.Equal(235000m, invoice.TotalAmount);
        Assert.Equal(invoice.Lines.Sum(l => l.Amount), invoice.TotalAmount);
    }

    [Fact]
    public async Task GenerateDraft_IgnoresVehiclesFromOtherApartmentsOrAfterBillingPeriod()
    {
        await using var context = CreateContext();
        SeedBillingData(context);

        // Vehicle in apartment 2
        var otherAptVehicle = new Vehicle
        {
            ApartmentId = 2,
            ResidentId = 1,
            VehicleType = "Ô tô",
            LicensePlate = "30A-OTHER",
            Status = "Đã duyệt",
            CreatedAt = new DateTime(2026, 8, 1)
        };
        // Vehicle created after August 2026 (September 2026)
        var futureVehicle = new Vehicle
        {
            ApartmentId = 1,
            ResidentId = 1,
            VehicleType = "Ô tô",
            LicensePlate = "30A-FUTURE",
            Status = "Đã duyệt",
            CreatedAt = new DateTime(2026, 9, 1)
        };
        context.Vehicles.AddRange(otherAptVehicle, futureVehicle);
        await context.SaveChangesAsync();

        var service = new BillingService(context, NullLogger<BillingService>.Instance);
        var result = await service.GenerateDraftAsync(1, 2026, 8, new DateTime(2026, 9, 10), "user-resident");

        Assert.True(result.Success, result.ErrorMessage);
        var invoice = result.Invoice!;

        var mgmtLine = invoice.Lines.Single(l => l.ChargeType == FeeChargeType.Management);
        // Pure base fee 50,000 because other vehicle belongs to apt 2 and future vehicle is after Aug 2026
        Assert.Equal(50000m, mgmtLine.Amount);
        Assert.DoesNotContain("[Bao gồm]", mgmtLine.Description);
        Assert.Equal(185000m, invoice.TotalAmount);
        Assert.Equal(invoice.Lines.Sum(l => l.Amount), invoice.TotalAmount);
    }
}

