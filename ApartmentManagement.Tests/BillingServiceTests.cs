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

    [Fact]
    public async Task VoidAsync_DraftInvoice_Succeeds()
    {
        await using var context = CreateContext();
        SeedBillingData(context);
        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var draft = await service.GenerateDraftAsync(1, 2026, 8, new DateTime(2026, 9, 10), "user-manager");
        Assert.True(draft.Success);

        var voidResult = await service.VoidAsync(draft.Invoice!.ApartmentInvoiceId, "user-manager", draft.Invoice.RowVersion);
        Assert.True(voidResult.Success, voidResult.ErrorMessage);
        Assert.Equal(ApartmentInvoiceStatus.Void, voidResult.Invoice!.Status);

        var fetched = await service.GetInvoiceAsync(draft.Invoice.ApartmentInvoiceId);
        Assert.NotNull(fetched);
        Assert.Equal(ApartmentInvoiceStatus.Void, fetched.Status);
    }

    [Fact]
    public async Task VoidAsync_IssuedInvoice_WithoutPayment_Succeeds()
    {
        await using var context = CreateContext();
        SeedBillingData(context);
        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var draft = await service.GenerateDraftAsync(1, 2026, 8, new DateTime(2026, 9, 10), "user-manager");
        var issue = await service.IssueAsync(draft.Invoice!.ApartmentInvoiceId, "user-manager", draft.Invoice.RowVersion);
        Assert.True(issue.Success);

        var voidResult = await service.VoidAsync(draft.Invoice.ApartmentInvoiceId, "user-manager", issue.Invoice!.RowVersion);
        Assert.True(voidResult.Success, voidResult.ErrorMessage);
        Assert.Equal(ApartmentInvoiceStatus.Void, voidResult.Invoice!.Status);
    }

    [Fact]
    public async Task VoidAsync_IssuedInvoice_WithConfirmedPayment_IsRejected()
    {
        await using var context = CreateContext();
        SeedBillingData(context);
        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var draft = await service.GenerateDraftAsync(1, 2026, 8, new DateTime(2026, 9, 10), "user-manager");
        var issue = await service.IssueAsync(draft.Invoice!.ApartmentInvoiceId, "user-manager", draft.Invoice.RowVersion);

        // Add confirmed payment
        context.PaymentTransactions.Add(new PaymentTransaction
        {
            ApartmentInvoiceId = draft.Invoice.ApartmentInvoiceId,
            Reference = "AHCONFIRMED123",
            Amount = draft.Invoice.TotalAmount,
            Status = PaymentTransactionStatus.Confirmed,
            CreatedAtUtc = DateTime.UtcNow,
            ConfirmedAtUtc = DateTime.UtcNow,
            ConfirmedByUserId = "user-manager"
        });
        await context.SaveChangesAsync();

        var voidResult = await service.VoidAsync(draft.Invoice.ApartmentInvoiceId, "user-manager", issue.Invoice!.RowVersion);
        Assert.False(voidResult.Success);
        Assert.Contains("đã có giao dịch thanh toán", voidResult.ErrorMessage);
    }

    [Fact]
    public async Task VoidAsync_IssuedInvoice_WithPendingPayment_RemovesPendingAndSucceeds()
    {
        await using var context = CreateContext();
        SeedBillingData(context);
        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var draft = await service.GenerateDraftAsync(1, 2026, 8, new DateTime(2026, 9, 10), "user-manager");
        var issue = await service.IssueAsync(draft.Invoice!.ApartmentInvoiceId, "user-manager", draft.Invoice.RowVersion);

        // Add pending payment
        context.PaymentTransactions.Add(new PaymentTransaction
        {
            ApartmentInvoiceId = draft.Invoice.ApartmentInvoiceId,
            Reference = "AHPENDING123",
            Amount = draft.Invoice.TotalAmount,
            Status = PaymentTransactionStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var voidResult = await service.VoidAsync(draft.Invoice.ApartmentInvoiceId, "user-manager", issue.Invoice!.RowVersion);
        Assert.True(voidResult.Success, voidResult.ErrorMessage);
        Assert.Equal(ApartmentInvoiceStatus.Void, voidResult.Invoice!.Status);
        Assert.Empty(await context.PaymentTransactions.Where(x => x.ApartmentInvoiceId == draft.Invoice.ApartmentInvoiceId).ToListAsync());
    }

    [Fact]
    public async Task VoidAsync_AlreadyVoidInvoice_ReturnsError()
    {
        await using var context = CreateContext();
        SeedBillingData(context);
        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var draft = await service.GenerateDraftAsync(1, 2026, 8, new DateTime(2026, 9, 10), "user-manager");
        var voidFirst = await service.VoidAsync(draft.Invoice!.ApartmentInvoiceId, "user-manager", draft.Invoice.RowVersion);
        Assert.True(voidFirst.Success);

        var voidSecond = await service.VoidAsync(draft.Invoice.ApartmentInvoiceId, "user-manager", voidFirst.Invoice!.RowVersion);
        Assert.False(voidSecond.Success);
        Assert.Contains("trạng thái hủy", voidSecond.ErrorMessage);
    }

    [Fact]
    public async Task CloseTariffAsync_ValidDate_Succeeds()
    {
        await using var context = CreateContext();
        SeedBillingData(context);
        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var closeResult = await service.CloseTariffAsync(1, new DateTime(2026, 12, 31), "user-manager");
        Assert.True(closeResult.Success, closeResult.ErrorMessage);

        var tariff = await service.GetTariffAsync(1);
        Assert.NotNull(tariff);
        Assert.Equal(new DateTime(2026, 12, 31), tariff.EffectiveTo);
    }

    [Fact]
    public async Task CloseTariffAsync_DateBeforeEffectiveFrom_ReturnsError()
    {
        await using var context = CreateContext();
        SeedBillingData(context);
        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var closeResult = await service.CloseTariffAsync(1, new DateTime(2025, 12, 31), "user-manager");
        Assert.False(closeResult.Success);
        Assert.Contains("từ ngày bắt đầu", closeResult.ErrorMessage);
    }

    [Fact]
    public async Task CloseTariffAsync_NonExistentTariff_ReturnsError()
    {
        await using var context = CreateContext();
        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var closeResult = await service.CloseTariffAsync(999, new DateTime(2026, 12, 31), "user-manager");
        Assert.False(closeResult.Success);
        Assert.Equal("Không tìm thấy biểu phí.", closeResult.ErrorMessage);
    }

    [Fact]
    public async Task GetTariffAsync_ReturnsTariffWithDetails()
    {
        await using var context = CreateContext();
        SeedBillingData(context);
        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var tariff = await service.GetTariffAsync(2); // Electricity tariff
        Assert.NotNull(tariff);
        Assert.Equal(FeeChargeType.Electricity, tariff.ChargeType);
        Assert.Equal(2, tariff.Tiers.Count);
        Assert.NotNull(tariff.Building);
    }

    [Fact]
    public async Task GetBillingSummaryAsync_AccurateKPIs_PendingNotCountedAsCollected()
    {
        await using var context = CreateContext();
        SeedBillingData(context);
        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        // Invoice 1: Issued with confirmed payment
        var draft1 = await service.GenerateDraftAsync(1, 2026, 8, new DateTime(2026, 9, 10), "user-manager");
        var issue1 = await service.IssueAsync(draft1.Invoice!.ApartmentInvoiceId, "user-manager", draft1.Invoice.RowVersion);
        context.PaymentTransactions.Add(new PaymentTransaction
        {
            ApartmentInvoiceId = draft1.Invoice.ApartmentInvoiceId,
            Reference = "AHCONFIRMED-SUMMARY",
            Amount = draft1.Invoice.TotalAmount,
            Status = PaymentTransactionStatus.Confirmed,
            CreatedAtUtc = DateTime.UtcNow,
            ConfirmedAtUtc = DateTime.UtcNow,
            ConfirmedByUserId = "user-manager"
        });

        // Invoice 2: Issued with pending payment (must NOT be counted into collected)
        var user2 = new ApplicationUser
        {
            Id = "user-resident-2", UserName = "resident2@test.local",
            NormalizedUserName = "RESIDENT2@TEST.LOCAL", Email = "resident2@test.local",
            NormalizedEmail = "RESIDENT2@TEST.LOCAL", FullName = "Test Resident 2", IsActive = true
        };
        var apt2 = new Apartment { ApartmentId = 2, BuildingId = 1, ApartmentCode = "A-102", Floor = 1, Area = 60, Status = "Đang sử dụng" };
        var res2 = new Resident { ResidentId = 2, UserId = user2.Id, User = user2, CitizenId = "987654321" };
        context.AddRange(user2, apt2, res2);
        context.ApartmentResidents.Add(new ApartmentResident
        {
            ApartmentId = 2, ResidentId = 2, Relationship = "Chủ hộ", MoveInDate = new DateTime(2026, 1, 1), IsOwner = true
        });
        context.UtilityMeterReadings.AddRange(
            new UtilityMeterReading { ApartmentId = 2, UtilityType = FeeChargeType.Electricity, BillingYear = 2026, BillingMonth = 8, PreviousReading = 0, CurrentReading = 50, ReadAtUtc = DateTime.UtcNow, RecordedByUserId = "user-manager" },
            new UtilityMeterReading { ApartmentId = 2, UtilityType = FeeChargeType.Water, BillingYear = 2026, BillingMonth = 8, PreviousReading = 0, CurrentReading = 5, ReadAtUtc = DateTime.UtcNow, RecordedByUserId = "user-manager" });
        await context.SaveChangesAsync();

        var draft2 = await service.GenerateDraftAsync(2, 2026, 8, new DateTime(2026, 9, 10), "user-manager");
        var issue2 = await service.IssueAsync(draft2.Invoice!.ApartmentInvoiceId, "user-manager", draft2.Invoice.RowVersion);
        context.PaymentTransactions.Add(new PaymentTransaction
        {
            ApartmentInvoiceId = draft2.Invoice.ApartmentInvoiceId,
            Reference = "AHPENDING-SUMMARY",
            Amount = draft2.Invoice.TotalAmount,
            Status = PaymentTransactionStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var summary = await service.GetBillingSummaryAsync(buildingId: 1, year: 2026, month: 8);

        Assert.Equal(2, summary.TotalInvoices);
        Assert.Equal(2, summary.IssuedCount);
        Assert.Equal(draft1.Invoice.TotalAmount + draft2.Invoice.TotalAmount, summary.TotalInvoiced);
        // Total collected must ONLY count the confirmed payment from invoice 1
        Assert.Equal(draft1.Invoice.TotalAmount, summary.TotalCollected);
        // Total receivable must be invoice 2 total amount
        Assert.Equal(draft2.Invoice.TotalAmount, summary.TotalReceivable);
        Assert.Equal(1, summary.PaidCount);
        Assert.Equal(1, summary.UnpaidCount);
    }

    [Fact]
    public async Task GetInvoicesAsync_WithFilters_FiltersProperly()
    {
        await using var context = CreateContext();
        SeedBillingData(context);
        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var draft1 = await service.GenerateDraftAsync(1, 2026, 8, new DateTime(2026, 9, 10), "user-manager");
        await service.IssueAsync(draft1.Invoice!.ApartmentInvoiceId, "user-manager", draft1.Invoice.RowVersion);

        // Filter by Building
        var filteredByBld = await service.GetInvoicesAsync(new InvoiceFilterCriteria(BuildingId: 1));
        Assert.Single(filteredByBld);

        var filteredOtherBld = await service.GetInvoicesAsync(new InvoiceFilterCriteria(BuildingId: 99));
        Assert.Empty(filteredOtherBld);

        // Filter by Year / Month
        var filteredPeriod = await service.GetInvoicesAsync(new InvoiceFilterCriteria(Year: 2026, Month: 8));
        Assert.Single(filteredPeriod);

        var filteredWrongMonth = await service.GetInvoicesAsync(new InvoiceFilterCriteria(Year: 2026, Month: 12));
        Assert.Empty(filteredWrongMonth);

        // Filter by Status
        var filteredIssued = await service.GetInvoicesAsync(new InvoiceFilterCriteria(Status: ApartmentInvoiceStatus.Issued));
        Assert.Single(filteredIssued);

        var filteredDraft = await service.GetInvoicesAsync(new InvoiceFilterCriteria(Status: ApartmentInvoiceStatus.Draft));
        Assert.Empty(filteredDraft);
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
