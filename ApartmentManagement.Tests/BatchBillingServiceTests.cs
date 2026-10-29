using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ApartmentManagement.Tests;

public sealed class BatchBillingServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static (ApplicationUser manager, Building building) SeedBuildingAndTariffs(
        ApplicationDbContext context,
        int buildingId = 1,
        bool includeManagement = true,
        bool includeElectricity = true,
        bool includeWater = true)
    {
        var manager = new ApplicationUser
        {
            Id = $"user-manager-{buildingId}",
            UserName = $"manager{buildingId}@test.local",
            NormalizedUserName = $"MANAGER{buildingId}@TEST.LOCAL",
            Email = $"manager{buildingId}@test.local",
            NormalizedEmail = $"MANAGER{buildingId}@TEST.LOCAL",
            FullName = "Test Manager",
            IsActive = true
        };
        var building = new Building
        {
            BuildingId = buildingId,
            BuildingCode = $"BLD-{buildingId}",
            BuildingName = $"Tòa {buildingId}",
            Address = "Test Address",
            NumberOfFloors = 10
        };

        context.AddRange(manager, building);

        if (includeManagement)
        {
            context.FeeTariffs.Add(new FeeTariff
            {
                BuildingId = buildingId,
                Building = building,
                ChargeType = FeeChargeType.Management,
                EffectiveFrom = new DateTime(2026, 1, 1),
                MonthlyRatePerSquareMeter = 1000,
                CreatedByUserId = manager.Id,
                CreatedByUser = manager
            });
        }

        if (includeElectricity)
        {
            context.FeeTariffs.Add(new FeeTariff
            {
                BuildingId = buildingId,
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
            });
        }

        if (includeWater)
        {
            context.FeeTariffs.Add(new FeeTariff
            {
                BuildingId = buildingId,
                Building = building,
                ChargeType = FeeChargeType.Water,
                EffectiveFrom = new DateTime(2026, 1, 1),
                CreatedByUserId = manager.Id,
                CreatedByUser = manager,
                Tiers = [new FeeTariffTier { TierOrder = 1, UpperConsumption = null, UnitRate = 500 }]
            });
        }

        context.SaveChanges();
        return (manager, building);
    }

    private static Apartment AddApartmentWithResidentAndReadings(
        ApplicationDbContext context,
        Building building,
        int apartmentId,
        string apartmentCode,
        decimal area = 50m,
        bool hasOwner = true,
        bool hasMultipleOwners = false,
        bool hasElectricityReading = true,
        bool hasWaterReading = true,
        int year = 2026,
        int month = 8)
    {
        var apartment = new Apartment
        {
            ApartmentId = apartmentId,
            BuildingId = building.BuildingId,
            Building = building,
            ApartmentCode = apartmentCode,
            Floor = 1,
            Area = area,
            Status = "Đang sử dụng"
        };
        context.Apartments.Add(apartment);

        if (hasOwner)
        {
            var user = new ApplicationUser
            {
                Id = $"user-apt-{apartmentId}",
                UserName = $"resident{apartmentId}@test.local",
                NormalizedUserName = $"RESIDENT{apartmentId}@TEST.LOCAL",
                Email = $"resident{apartmentId}@test.local",
                NormalizedEmail = $"RESIDENT{apartmentId}@TEST.LOCAL",
                FullName = $"Resident {apartmentId}",
                IsActive = true
            };
            var resident = new Resident
            {
                ResidentId = apartmentId * 10,
                UserId = user.Id,
                User = user,
                CitizenId = $"0012026{apartmentId:D5}"
            };
            context.AddRange(user, resident);
            context.ApartmentResidents.Add(new ApartmentResident
            {
                ApartmentId = apartmentId,
                Apartment = apartment,
                ResidentId = resident.ResidentId,
                Resident = resident,
                Relationship = "Chủ hộ",
                MoveInDate = new DateTime(2026, 1, 1),
                IsOwner = true
            });

            if (hasMultipleOwners)
            {
                var user2 = new ApplicationUser
                {
                    Id = $"user-apt-{apartmentId}-coowner",
                    UserName = $"coowner{apartmentId}@test.local",
                    NormalizedUserName = $"COOWNER{apartmentId}@TEST.LOCAL",
                    Email = $"coowner{apartmentId}@test.local",
                    NormalizedEmail = $"COOWNER{apartmentId}@TEST.LOCAL",
                    FullName = $"Co-Owner {apartmentId}",
                    IsActive = true
                };
                var resident2 = new Resident
                {
                    ResidentId = apartmentId * 10 + 1,
                    UserId = user2.Id,
                    User = user2,
                    CitizenId = $"0012027{apartmentId:D5}"
                };
                context.AddRange(user2, resident2);
                context.ApartmentResidents.Add(new ApartmentResident
                {
                    ApartmentId = apartmentId,
                    Apartment = apartment,
                    ResidentId = resident2.ResidentId,
                    Resident = resident2,
                    Relationship = "Đồng sở hữu",
                    MoveInDate = new DateTime(2026, 1, 1),
                    IsOwner = true
                });
            }
        }

        if (hasElectricityReading)
        {
            context.UtilityMeterReadings.Add(new UtilityMeterReading
            {
                ApartmentId = apartmentId,
                Apartment = apartment,
                UtilityType = FeeChargeType.Electricity,
                BillingYear = year,
                BillingMonth = month,
                PreviousReading = 0,
                CurrentReading = 60,
                ReadAtUtc = DateTime.UtcNow,
                RecordedByUserId = $"user-manager-{building.BuildingId}"
            });
        }

        if (hasWaterReading)
        {
            context.UtilityMeterReadings.Add(new UtilityMeterReading
            {
                ApartmentId = apartmentId,
                Apartment = apartment,
                UtilityType = FeeChargeType.Water,
                BillingYear = year,
                BillingMonth = month,
                PreviousReading = 0,
                CurrentReading = 10,
                ReadAtUtc = DateTime.UtcNow,
                RecordedByUserId = $"user-manager-{building.BuildingId}"
            });
        }

        context.SaveChanges();
        return apartment;
    }

    [Theory]
    [InlineData(0, 2026, 8, "2026-09-15", "user-manager", "Vui lòng chọn tòa nhà.")]
    [InlineData(1, 2026, 8, "2026-09-15", "", "Người thực hiện không hợp lệ.")]
    [InlineData(1, 1999, 8, "2026-09-15", "user-manager", "Kỳ hóa đơn không hợp lệ.")]
    [InlineData(1, 2026, 13, "2026-09-15", "user-manager", "Kỳ hóa đơn không hợp lệ.")]
    [InlineData(1, 2026, 8, "2026-08-31", "user-manager", "Hạn thanh toán phải từ ngày đầu tiên sau kỳ tính phí trở đi.")]
    public async Task TC_BATCH_10_GenerateBuildingDraftsAsync_RejectsInvalidInputParameters(
        int buildingId, int year, int month, string dueDateStr, string actorUserId, string expectedError)
    {
        await using var context = CreateContext();
        var service = new BillingService(context, NullLogger<BillingService>.Instance);
        var dueDate = DateTime.Parse(dueDateStr);

        var result = await service.GenerateBuildingDraftsAsync(buildingId, year, month, dueDate, actorUserId);

        Assert.False(result.Success);
        Assert.Equal(expectedError, result.ErrorMessage);
        Assert.Equal(0, result.TotalScanned);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task TC_BATCH_06_GenerateBuildingDraftsAsync_BuildingNotFound_ReturnsFailure()
    {
        await using var context = CreateContext();
        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var result = await service.GenerateBuildingDraftsAsync(999, 2026, 8, new DateTime(2026, 9, 15), "user-manager");

        Assert.False(result.Success);
        Assert.Equal("Không tìm thấy tòa nhà.", result.ErrorMessage);
        Assert.Equal(0, result.TotalScanned);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task TC_BATCH_06_GenerateBuildingDraftsAsync_MissingTariff_FailsFastAtBuildingLevel(
        bool hasMgmt, bool hasElec, bool hasWater)
    {
        await using var context = CreateContext();
        SeedBuildingAndTariffs(context, 1, hasMgmt, hasElec, hasWater);
        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var result = await service.GenerateBuildingDraftsAsync(1, 2026, 8, new DateTime(2026, 9, 15), "user-manager");

        Assert.False(result.Success);
        Assert.Contains("biểu phí quản lý, điện và nước", result.ErrorMessage);
        Assert.Equal(0, result.TotalScanned);
    }

    [Fact]
    public async Task GenerateBuildingDraftsAsync_BuildingWithNoApartments_ReturnsZeroScanned()
    {
        await using var context = CreateContext();
        SeedBuildingAndTariffs(context, 1);
        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var result = await service.GenerateBuildingDraftsAsync(1, 2026, 8, new DateTime(2026, 9, 15), "user-manager");

        Assert.True(result.Success);
        Assert.Equal(0, result.TotalScanned);
        Assert.Equal(0, result.CreatedCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task TC_BATCH_01_GenerateBuildingDraftsAsync_EligibleApartments_CreatesDraftInvoices()
    {
        await using var context = CreateContext();
        var (_, building) = SeedBuildingAndTariffs(context, 1);

        AddApartmentWithResidentAndReadings(context, building, 1, "A-101", area: 50);
        AddApartmentWithResidentAndReadings(context, building, 2, "A-102", area: 70);

        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var result = await service.GenerateBuildingDraftsAsync(1, 2026, 8, new DateTime(2026, 9, 15), "user-manager");

        Assert.True(result.Success);
        Assert.Equal(2, result.TotalScanned);
        Assert.Equal(2, result.CreatedCount);
        Assert.Equal(0, result.AlreadyExistsCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(2, result.Items.Count);

        foreach (var item in result.Items)
        {
            Assert.Equal(BatchBillingItemStatus.Created, item.Status);
            Assert.NotNull(item.InvoiceId);
            Assert.NotNull(item.InvoiceCode);
            Assert.True(item.TotalAmount > 0);
        }

        // Verify stored in DB with Status = Draft and 3 lines
        var invoices = await context.ApartmentInvoices.Include(x => x.Lines).ToListAsync();
        Assert.Equal(2, invoices.Count);
        Assert.All(invoices, inv =>
        {
            Assert.Equal(ApartmentInvoiceStatus.Draft, inv.Status);
            Assert.Equal(3, inv.Lines.Count);
        });
    }

    [Fact]
    public async Task TC_BATCH_02_GenerateBuildingDraftsAsync_AlreadyExistingInvoice_SkipsSafelyWithoutOverwriting()
    {
        await using var context = CreateContext();
        var (_, building) = SeedBuildingAndTariffs(context, 1);
        AddApartmentWithResidentAndReadings(context, building, 1, "A-101");

        // Manually create existing invoice
        var existing = new ApartmentInvoice
        {
            InvoiceCode = "INV-EXISTING-001",
            ApartmentId = 1,
            ResidentId = 10,
            BillingYear = 2026,
            BillingMonth = 8,
            DueDate = new DateTime(2026, 9, 15),
            ApartmentArea = 50,
            TotalAmount = 250000,
            Status = ApartmentInvoiceStatus.Issued,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = "user-manager"
        };
        context.ApartmentInvoices.Add(existing);
        context.SaveChanges();

        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var result = await service.GenerateBuildingDraftsAsync(1, 2026, 8, new DateTime(2026, 9, 15), "user-manager");

        Assert.True(result.Success);
        Assert.Equal(1, result.TotalScanned);
        Assert.Equal(0, result.CreatedCount);
        Assert.Equal(1, result.AlreadyExistsCount);
        Assert.Equal(0, result.SkippedCount);

        var item = Assert.Single(result.Items);
        Assert.Equal(BatchBillingItemStatus.AlreadyExists, item.Status);
        Assert.Equal(existing.ApartmentInvoiceId, item.InvoiceId);
        Assert.Equal(existing.InvoiceCode, item.InvoiceCode);

        // Verify DB still only has the original invoice unchanged
        var invoiceInDb = await context.ApartmentInvoices.SingleAsync();
        Assert.Equal(ApartmentInvoiceStatus.Issued, invoiceInDb.Status);
        Assert.Equal("INV-EXISTING-001", invoiceInDb.InvoiceCode);
    }

    [Fact]
    public async Task TC_BATCH_03_GenerateBuildingDraftsAsync_MissingMeterReadings_SkipsWithClearReason()
    {
        await using var context = CreateContext();
        var (_, building) = SeedBuildingAndTariffs(context, 1);

        AddApartmentWithResidentAndReadings(context, building, 1, "A-101", hasElectricityReading: true, hasWaterReading: false);
        AddApartmentWithResidentAndReadings(context, building, 2, "A-102", hasElectricityReading: true, hasWaterReading: true);

        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var result = await service.GenerateBuildingDraftsAsync(1, 2026, 8, new DateTime(2026, 9, 15), "user-manager");

        Assert.True(result.Success);
        Assert.Equal(2, result.TotalScanned);
        Assert.Equal(1, result.CreatedCount);
        Assert.Equal(1, result.SkippedCount);

        var skipped = result.Items.Single(x => x.ApartmentCode == "A-101");
        Assert.Equal(BatchBillingItemStatus.Skipped, skipped.Status);
        Assert.Contains("chỉ số", skipped.Message);

        var created = result.Items.Single(x => x.ApartmentCode == "A-102");
        Assert.Equal(BatchBillingItemStatus.Created, created.Status);
    }

    [Fact]
    public async Task TC_BATCH_04_GenerateBuildingDraftsAsync_MissingOwner_SkipsWithClearReason()
    {
        await using var context = CreateContext();
        var (_, building) = SeedBuildingAndTariffs(context, 1);

        AddApartmentWithResidentAndReadings(context, building, 1, "A-101", hasOwner: false);

        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var result = await service.GenerateBuildingDraftsAsync(1, 2026, 8, new DateTime(2026, 9, 15), "user-manager");

        Assert.True(result.Success);
        Assert.Equal(1, result.TotalScanned);
        Assert.Equal(0, result.CreatedCount);
        Assert.Equal(1, result.SkippedCount);

        var item = Assert.Single(result.Items);
        Assert.Equal(BatchBillingItemStatus.Skipped, item.Status);
        Assert.Contains("Không có chủ hộ đang cư trú", item.Message);
    }

    [Fact]
    public async Task TC_BATCH_05_GenerateBuildingDraftsAsync_MultipleOwners_SkipsWithClearReason()
    {
        await using var context = CreateContext();
        var (_, building) = SeedBuildingAndTariffs(context, 1);

        AddApartmentWithResidentAndReadings(context, building, 1, "A-101", hasMultipleOwners: true);

        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var result = await service.GenerateBuildingDraftsAsync(1, 2026, 8, new DateTime(2026, 9, 15), "user-manager");

        Assert.True(result.Success);
        Assert.Equal(1, result.TotalScanned);
        Assert.Equal(0, result.CreatedCount);
        Assert.Equal(1, result.SkippedCount);

        var item = Assert.Single(result.Items);
        Assert.Equal(BatchBillingItemStatus.Skipped, item.Status);
        Assert.Contains("nhiều Chủ hộ", item.Message);
    }

    [Fact]
    public async Task TC_BATCH_07_GenerateBuildingDraftsAsync_ZeroAreaApartment_SkipsWithoutBreakingOtherApartments()
    {
        await using var context = CreateContext();
        var (_, building) = SeedBuildingAndTariffs(context, 1);

        AddApartmentWithResidentAndReadings(context, building, 1, "A-101", area: 0);
        AddApartmentWithResidentAndReadings(context, building, 2, "A-102", area: 50);

        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var result = await service.GenerateBuildingDraftsAsync(1, 2026, 8, new DateTime(2026, 9, 15), "user-manager");

        Assert.True(result.Success);
        Assert.Equal(2, result.TotalScanned);
        Assert.Equal(1, result.CreatedCount);
        Assert.Equal(1, result.SkippedCount);

        var skipped = result.Items.Single(x => x.ApartmentCode == "A-101");
        Assert.Equal(BatchBillingItemStatus.Skipped, skipped.Status);
        Assert.Contains("Diện tích", skipped.Message);

        var created = result.Items.Single(x => x.ApartmentCode == "A-102");
        Assert.Equal(BatchBillingItemStatus.Created, created.Status);
    }

    [Fact]
    public async Task TC_BATCH_08_GenerateBuildingDraftsAsync_Idempotency_SecondRunMarksAllAsAlreadyExists()
    {
        await using var context = CreateContext();
        var (_, building) = SeedBuildingAndTariffs(context, 1);

        AddApartmentWithResidentAndReadings(context, building, 1, "A-101");
        AddApartmentWithResidentAndReadings(context, building, 2, "A-102");

        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        // Run 1
        var result1 = await service.GenerateBuildingDraftsAsync(1, 2026, 8, new DateTime(2026, 9, 15), "user-manager");
        Assert.Equal(2, result1.CreatedCount);

        // Run 2
        var result2 = await service.GenerateBuildingDraftsAsync(1, 2026, 8, new DateTime(2026, 9, 15), "user-manager");
        Assert.True(result2.Success);
        Assert.Equal(2, result2.TotalScanned);
        Assert.Equal(0, result2.CreatedCount);
        Assert.Equal(2, result2.AlreadyExistsCount);
        Assert.Equal(0, result2.SkippedCount);
        Assert.Equal(0, result2.FailedCount);

        Assert.Equal(2, await context.ApartmentInvoices.CountAsync());
    }

    [Fact]
    public async Task TC_BATCH_09_GenerateBuildingDraftsAsync_MetricsReconciliation_CountsAlwaysEqualSum()
    {
        await using var context = CreateContext();
        var (_, building) = SeedBuildingAndTariffs(context, 1);

        // Apt 1: Eligible -> Created
        AddApartmentWithResidentAndReadings(context, building, 1, "A-101");
        // Apt 2: Missing readings -> Skipped
        AddApartmentWithResidentAndReadings(context, building, 2, "A-102", hasElectricityReading: false);
        // Apt 3: Missing owner -> Skipped
        AddApartmentWithResidentAndReadings(context, building, 3, "A-103", hasOwner: false);
        // Apt 4: Existing invoice -> AlreadyExists
        AddApartmentWithResidentAndReadings(context, building, 4, "A-104");
        context.ApartmentInvoices.Add(new ApartmentInvoice
        {
            InvoiceCode = "INV-PRE-04",
            ApartmentId = 4,
            ResidentId = 40,
            BillingYear = 2026,
            BillingMonth = 8,
            DueDate = new DateTime(2026, 9, 15),
            ApartmentArea = 50,
            TotalAmount = 100000,
            Status = ApartmentInvoiceStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = "user-manager"
        });
        context.SaveChanges();

        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var result = await service.GenerateBuildingDraftsAsync(1, 2026, 8, new DateTime(2026, 9, 15), "user-manager");

        Assert.True(result.Success);
        Assert.Equal(4, result.TotalScanned);
        Assert.Equal(1, result.CreatedCount);
        Assert.Equal(1, result.AlreadyExistsCount);
        Assert.Equal(2, result.SkippedCount);
        Assert.Equal(0, result.FailedCount);

        // Crucial invariant check:
        Assert.Equal(result.TotalScanned, result.CreatedCount + result.AlreadyExistsCount + result.SkippedCount + result.FailedCount);
        Assert.Equal(result.TotalScanned, result.Items.Count);
    }
}

