using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ApartmentManagement.Tests;

public sealed class BillingServiceTests
{
    [Fact]
    public void ProgressiveTariff_AppliesCumulativeBandsAtBoundary()
    {
        var amount = ProgressiveTariffCalculator.Calculate(50,
        [
            new FeeTariffTier { TierOrder = 1, UpperConsumption = 50, UnitRate = 100 },
            new FeeTariffTier { TierOrder = 2, UpperConsumption = null, UnitRate = 200 }
        ]);
        Assert.Equal(5000, amount);

        var overBoundary = ProgressiveTariffCalculator.Calculate(51,
        [
            new FeeTariffTier { TierOrder = 1, UpperConsumption = 50, UnitRate = 100 },
            new FeeTariffTier { TierOrder = 2, UpperConsumption = null, UnitRate = 200 }
        ]);
        Assert.Equal(5200, overBoundary);
    }

    [Fact]
    public void ProgressiveTariff_RoundsCurrencyAwayFromZero()
    {
        var result = ProgressiveTariffCalculator.Calculate(1,
        [
            new FeeTariffTier { TierOrder = 1, UpperConsumption = null, UnitRate = 10.5m }
        ]);
        Assert.Equal(11, result);
    }

    [Fact]
    public void ProgressiveTariff_RejectsInvalidTierOrder()
    {
        Assert.Throws<ArgumentException>(() => ProgressiveTariffCalculator.Calculate(10,
        [
            new FeeTariffTier { TierOrder = 1, UpperConsumption = 20, UnitRate = 100 },
            new FeeTariffTier { TierOrder = 3, UpperConsumption = null, UnitRate = 200 }
        ]));
    }

    [Fact]
    public async Task DraftInvoice_SnapshotsManagementAndProgressiveUtilityCharges()
    {
        await using var context = CreateContext();
        SeedBillingData(context);
        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var result = await service.GenerateDraftAsync(1, 2026, 8, new DateTime(2026, 9, 10), "user-resident");

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(185000, result.Invoice!.TotalAmount);
        Assert.Equal(ApartmentInvoiceStatus.Draft, result.Invoice.Status);
        Assert.Equal(3, result.Invoice.Lines.Count);
        Assert.Equal(130000, result.Invoice.Lines.Single(x => x.ChargeType == FeeChargeType.Electricity).Amount);
        Assert.Equal(5000, result.Invoice.Lines.Single(x => x.ChargeType == FeeChargeType.Water).Amount);
        Assert.Empty(await service.GetResidentInvoicesAsync("user-resident"));

        var issue = await service.IssueAsync(
            result.Invoice.ApartmentInvoiceId, "user-manager", result.Invoice.RowVersion);
        Assert.True(issue.Success, issue.ErrorMessage);
        Assert.Single(await service.GetResidentInvoicesAsync("user-resident"));
        Assert.Empty(await service.GetResidentInvoicesAsync("another-user"));
    }

    [Fact]
    public async Task DraftInvoice_RequiresUniqueInvoicePerApartmentAndPeriod()
    {
        await using var context = CreateContext();
        SeedBillingData(context);
        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        Assert.True((await service.GenerateDraftAsync(
            1, 2026, 8, new DateTime(2026, 9, 10), "user-resident")).Success);
        var duplicate = await service.GenerateDraftAsync(
            1, 2026, 8, new DateTime(2026, 9, 10), "user-resident");

        Assert.False(duplicate.Success);
        Assert.Contains("đã có hóa đơn", duplicate.ErrorMessage);
    }

    [Fact]
    public async Task MeterReading_RequiresContinuityWithPreviousRecordedMonth()
    {
        await using var context = CreateContext();
        SeedBillingData(context);
        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var invalid = await service.RecordReadingAsync(
            new MeterReadingInput(1, FeeChargeType.Electricity, 2026, 9, 61, 80),
            "user-manager");
        Assert.False(invalid.Success);
        Assert.Contains("khớp", invalid.ErrorMessage);

        var valid = await service.RecordReadingAsync(
            new MeterReadingInput(1, FeeChargeType.Electricity, 2026, 9, 60, 80),
            "user-manager");
        Assert.True(valid.Success, valid.ErrorMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetTariffsAsync_ReturnsEmpty_ForNonPositiveBuildingId(int buildingId)
    {
        await using var context = CreateContext();
        SeedBillingData(context);
        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var tariffs = await service.GetTariffsAsync(buildingId);
        Assert.Empty(tariffs);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetResidentInvoicesAsync_ReturnsEmpty_ForNullOrWhitespaceUserId(string? userId)
    {
        await using var context = CreateContext();
        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var invoices = await service.GetResidentInvoicesAsync(userId!);
        Assert.Empty(invoices);
    }

    [Theory]
    [InlineData(0, "user-resident")]
    [InlineData(-1, "user-resident")]
    [InlineData(1, null)]
    [InlineData(1, "")]
    [InlineData(1, "   ")]
    public async Task GetResidentInvoiceAsync_ReturnsNull_ForNonPositiveIdOrNullUser(int invoiceId, string? userId)
    {
        await using var context = CreateContext();
        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var invoice = await service.GetResidentInvoiceAsync(userId!, invoiceId);
        Assert.Null(invoice);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetInvoiceAsync_ReturnsNull_ForNonPositiveId(int invoiceId)
    {
        await using var context = CreateContext();
        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var invoice = await service.GetInvoiceAsync(invoiceId);
        Assert.Null(invoice);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GenerateDraftAsync_RejectsNonPositiveApartmentId(int apartmentId)
    {
        await using var context = CreateContext();
        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var result = await service.GenerateDraftAsync(apartmentId, 2026, 8, new DateTime(2026, 9, 10), "user-manager");
        Assert.False(result.Success);
        Assert.Equal("Vui lòng chọn căn hộ.", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateDraftAsync_RejectsZeroAreaApartment()
    {
        await using var context = CreateContext();
        SeedBillingData(context);
        var apartment = await context.Apartments.FindAsync(1);
        apartment!.Area = 0;
        await context.SaveChangesAsync();

        var service = new BillingService(context, NullLogger<BillingService>.Instance);
        var result = await service.GenerateDraftAsync(1, 2026, 8, new DateTime(2026, 9, 10), "user-manager");

        Assert.False(result.Success);
        Assert.Equal("Diện tích căn hộ phải lớn hơn 0 để tính phí quản lý.", result.ErrorMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task RecordReadingAsync_RejectsNonPositiveApartmentId(int apartmentId)
    {
        await using var context = CreateContext();
        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var result = await service.RecordReadingAsync(
            new MeterReadingInput(apartmentId, FeeChargeType.Electricity, 2026, 9, 60, 80),
            "user-manager");
        Assert.False(result.Success);
        Assert.Equal("Vui lòng chọn căn hộ.", result.ErrorMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task IssueAsync_ReturnsError_WhenInvoiceNotFound(int invoiceId)
    {
        await using var context = CreateContext();
        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var result = await service.IssueAsync(invoiceId, "user-manager", [1]);
        Assert.False(result.Success);
        Assert.Equal("Không tìm thấy hóa đơn.", result.ErrorMessage);
    }

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
            Id = "user-resident", UserName = "resident@test.local",
            NormalizedUserName = "RESIDENT@TEST.LOCAL", Email = "resident@test.local",
            NormalizedEmail = "RESIDENT@TEST.LOCAL", FullName = "Test Resident", IsActive = true
        };
        var manager = new ApplicationUser
        {
            Id = "user-manager", UserName = "manager@test.local",
            NormalizedUserName = "MANAGER@TEST.LOCAL", Email = "manager@test.local",
            NormalizedEmail = "MANAGER@TEST.LOCAL", FullName = "Test Manager", IsActive = true
        };
        var building = new Building
        {
            BuildingId = 1, BuildingCode = "BLD-1", BuildingName = "Tòa thử nghiệm",
            Address = "Test", NumberOfFloors = 10
        };
        var apartment = new Apartment
        {
            ApartmentId = 1, BuildingId = 1, Building = building, ApartmentCode = "A-101",
            Floor = 1, Area = 50, Status = "Đang sử dụng"
        };
        var resident = new Resident
        {
            ResidentId = 1, UserId = user.Id, User = user, CitizenId = "123456789"
        };
        var management = new FeeTariff
        {
            BuildingId = 1, Building = building, ChargeType = FeeChargeType.Management,
            EffectiveFrom = new DateTime(2026, 1, 1), MonthlyRatePerSquareMeter = 1000,
            CreatedByUserId = manager.Id, CreatedByUser = manager
        };
        var electricity = new FeeTariff
        {
            BuildingId = 1, Building = building, ChargeType = FeeChargeType.Electricity,
            EffectiveFrom = new DateTime(2026, 1, 1), CreatedByUserId = manager.Id, CreatedByUser = manager,
            Tiers =
            [
                new FeeTariffTier { TierOrder = 1, UpperConsumption = 50, UnitRate = 2000 },
                new FeeTariffTier { TierOrder = 2, UpperConsumption = null, UnitRate = 3000 }
            ]
        };
        var water = new FeeTariff
        {
            BuildingId = 1, Building = building, ChargeType = FeeChargeType.Water,
            EffectiveFrom = new DateTime(2026, 1, 1), CreatedByUserId = manager.Id, CreatedByUser = manager,
            Tiers = [new FeeTariffTier { TierOrder = 1, UpperConsumption = null, UnitRate = 500 }]
        };

        context.AddRange(user, manager, building, apartment, resident, management, electricity, water);
        context.ApartmentResidents.Add(new ApartmentResident
        {
            ApartmentId = 1, Apartment = apartment, ResidentId = 1, Resident = resident,
            Relationship = "Chủ hộ", MoveInDate = new DateTime(2026, 1, 1), IsOwner = true
        });
        context.UtilityMeterReadings.AddRange(
            new UtilityMeterReading
            {
                ApartmentId = 1, Apartment = apartment, UtilityType = FeeChargeType.Electricity,
                BillingYear = 2026, BillingMonth = 8, PreviousReading = 0, CurrentReading = 60,
                ReadAtUtc = DateTime.UtcNow, RecordedByUserId = manager.Id, RecordedByUser = manager
            },
            new UtilityMeterReading
            {
                ApartmentId = 1, Apartment = apartment, UtilityType = FeeChargeType.Water,
                BillingYear = 2026, BillingMonth = 8, PreviousReading = 0, CurrentReading = 10,
                ReadAtUtc = DateTime.UtcNow, RecordedByUserId = manager.Id, RecordedByUser = manager
            });
        context.SaveChanges();
    }
}
