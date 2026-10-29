using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ApartmentManagement.Tests;

public sealed class BatchMeterReadingTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static (Building building, List<Apartment> apartments) SeedBuildingWithApartments(
        ApplicationDbContext context, int buildingId = 1, int apartmentCount = 3)
    {
        var building = new Building
        {
            BuildingId = buildingId,
            BuildingCode = $"BLD-{buildingId}",
            BuildingName = $"Tòa {buildingId}",
            Address = "Địa chỉ test",
            NumberOfFloors = 10
        };
        context.Buildings.Add(building);

        var apartments = new List<Apartment>();
        for (int i = 1; i <= apartmentCount; i++)
        {
            var apt = new Apartment
            {
                ApartmentId = buildingId * 100 + i,
                BuildingId = buildingId,
                Building = building,
                ApartmentCode = $"A-{i:D3}",
                Floor = 1,
                Area = 50,
                Status = "Đang sử dụng"
            };
            apartments.Add(apt);
            context.Apartments.Add(apt);
        }

        context.SaveChanges();
        return (building, apartments);
    }

    [Fact]
    public async Task GetBatchMeterReadingSheetAsync_PreloadsPrecedingReadingAsOldReading()
    {
        await using var context = CreateContext();
        var (building, apartments) = SeedBuildingWithApartments(context, 1, 2);
        var apt1 = apartments[0];
        var apt2 = apartments[1];

        // Seed readings for month 7
        context.UtilityMeterReadings.AddRange(
            new UtilityMeterReading
            {
                ApartmentId = apt1.ApartmentId,
                UtilityType = FeeChargeType.Electricity,
                BillingYear = 2026,
                BillingMonth = 7,
                PreviousReading = 0,
                CurrentReading = 150,
                ReadAtUtc = DateTime.UtcNow,
                RecordedByUserId = "tech-01"
            },
            new UtilityMeterReading
            {
                ApartmentId = apt1.ApartmentId,
                UtilityType = FeeChargeType.Water,
                BillingYear = 2026,
                BillingMonth = 7,
                PreviousReading = 0,
                CurrentReading = 25,
                ReadAtUtc = DateTime.UtcNow,
                RecordedByUserId = "tech-01"
            });
        await context.SaveChangesAsync();

        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        // Fetch sheet for month 8
        var sheet = await service.GetBatchMeterReadingSheetAsync(building.BuildingId, 2026, 8);

        Assert.Equal(2, sheet.Rows.Count);

        var row1 = sheet.Rows.Single(r => r.ApartmentId == apt1.ApartmentId);
        Assert.Equal(150, row1.ElectricityOldReading);
        Assert.Equal(25, row1.WaterOldReading);
        Assert.Null(row1.ElectricityNewReading);
        Assert.Null(row1.WaterNewReading);
        Assert.False(row1.HasExistingReading);
        Assert.False(row1.IsInvoiceLocked);

        var row2 = sheet.Rows.Single(r => r.ApartmentId == apt2.ApartmentId);
        Assert.Equal(0, row2.ElectricityOldReading);
        Assert.Equal(0, row2.WaterOldReading);
    }

    [Fact]
    public async Task GetBatchMeterReadingSheetAsync_IdentifiesLockedInvoices()
    {
        await using var context = CreateContext();
        var (building, apartments) = SeedBuildingWithApartments(context, 1, 1);
        var apt = apartments[0];

        // Create invoice in month 8
        context.ApartmentInvoices.Add(new ApartmentInvoice
        {
            InvoiceCode = "INV-LOCKED-01",
            ApartmentId = apt.ApartmentId,
            ResidentId = 1,
            BillingYear = 2026,
            BillingMonth = 8,
            DueDate = new DateTime(2026, 9, 15),
            ApartmentArea = 50,
            TotalAmount = 100000,
            Status = ApartmentInvoiceStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = "manager-01"
        });
        await context.SaveChangesAsync();

        var service = new BillingService(context, NullLogger<BillingService>.Instance);
        var sheet = await service.GetBatchMeterReadingSheetAsync(building.BuildingId, 2026, 8);

        var row = Assert.Single(sheet.Rows);
        Assert.True(row.IsInvoiceLocked);
        Assert.Contains("Đã có hóa đơn", row.StatusNote);
    }

    [Fact]
    public async Task SaveBatchMeterReadingsAsync_ValidReadings_SavesNewOrUpdatesExisting()
    {
        await using var context = CreateContext();
        var (building, apartments) = SeedBuildingWithApartments(context, 1, 2);
        var apt1 = apartments[0];
        var apt2 = apartments[1];

        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var input = new BatchMeterReadingSaveInput(
            building.BuildingId,
            2026,
            8,
            [
                new BatchMeterReadingItemInput(apt1.ApartmentId, apt1.ApartmentCode, 0, 100, 0, 15),
                new BatchMeterReadingItemInput(apt2.ApartmentId, apt2.ApartmentCode, 0, 200, 0, 30)
            ]);

        var result = await service.SaveBatchMeterReadingsAsync(input, "tech-01");

        Assert.True(result.Success);
        Assert.Equal(2, result.TotalProcessed);
        Assert.Equal(2, result.SavedCount);
        Assert.Equal(0, result.FailedCount);

        var readingsInDb = await context.UtilityMeterReadings.ToListAsync();
        Assert.Equal(4, readingsInDb.Count); // 2 elec + 2 water

        // Now test update (correction before invoice)
        var updateInput = new BatchMeterReadingSaveInput(
            building.BuildingId,
            2026,
            8,
            [
                new BatchMeterReadingItemInput(apt1.ApartmentId, apt1.ApartmentCode, 0, 120, 0, 18) // Updated 100->120, 15->18
            ]);

        var updateResult = await service.SaveBatchMeterReadingsAsync(updateInput, "tech-01");
        Assert.True(updateResult.Success);
        Assert.Equal(1, updateResult.SavedCount);

        var updatedElec = await context.UtilityMeterReadings.SingleAsync(r =>
            r.ApartmentId == apt1.ApartmentId && r.UtilityType == FeeChargeType.Electricity);
        Assert.Equal(120, updatedElec.CurrentReading);
    }

    [Fact]
    public async Task SaveBatchMeterReadingsAsync_SkipsRowsWithNoNewReadings()
    {
        await using var context = CreateContext();
        var (building, apartments) = SeedBuildingWithApartments(context, 1, 2);
        var apt1 = apartments[0];
        var apt2 = apartments[1];

        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var input = new BatchMeterReadingSaveInput(
            building.BuildingId,
            2026,
            8,
            [
                new BatchMeterReadingItemInput(apt1.ApartmentId, apt1.ApartmentCode, 0, 100, 0, 15),
                new BatchMeterReadingItemInput(apt2.ApartmentId, apt2.ApartmentCode, 0, null, 0, null) // Skipped
            ]);

        var result = await service.SaveBatchMeterReadingsAsync(input, "tech-01");

        Assert.True(result.Success);
        Assert.Equal(2, result.TotalProcessed);
        Assert.Equal(1, result.SavedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(0, result.FailedCount);

        Assert.Equal(2, await context.UtilityMeterReadings.CountAsync());
    }

    [Fact]
    public async Task SaveBatchMeterReadingsAsync_RejectsWhenNewReadingLessThanOldReading()
    {
        await using var context = CreateContext();
        var (building, apartments) = SeedBuildingWithApartments(context, 1, 1);
        var apt = apartments[0];

        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var input = new BatchMeterReadingSaveInput(
            building.BuildingId,
            2026,
            8,
            [
                new BatchMeterReadingItemInput(apt.ApartmentId, apt.ApartmentCode, 100, 90, 20, 25) // Elec: 90 < 100 -> Invalid
            ]);

        var result = await service.SaveBatchMeterReadingsAsync(input, "tech-01");

        Assert.True(result.Success);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(0, result.SavedCount);

        var failedItem = Assert.Single(result.Items);
        Assert.False(failedItem.Success);
        Assert.Contains("lớn hơn hoặc bằng", failedItem.Message);
        Assert.Empty(await context.UtilityMeterReadings.ToListAsync());
    }

    [Fact]
    public async Task SaveBatchMeterReadingsAsync_RejectsWhenInvoiceIsLocked()
    {
        await using var context = CreateContext();
        var (building, apartments) = SeedBuildingWithApartments(context, 1, 1);
        var apt = apartments[0];

        context.ApartmentInvoices.Add(new ApartmentInvoice
        {
            InvoiceCode = "INV-LOCKED-02",
            ApartmentId = apt.ApartmentId,
            ResidentId = 1,
            BillingYear = 2026,
            BillingMonth = 8,
            DueDate = new DateTime(2026, 9, 15),
            ApartmentArea = 50,
            TotalAmount = 100000,
            Status = ApartmentInvoiceStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = "manager-01"
        });
        await context.SaveChangesAsync();

        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var input = new BatchMeterReadingSaveInput(
            building.BuildingId,
            2026,
            8,
            [
                new BatchMeterReadingItemInput(apt.ApartmentId, apt.ApartmentCode, 0, 100, 0, 20)
            ]);

        var result = await service.SaveBatchMeterReadingsAsync(input, "tech-01");

        Assert.True(result.Success);
        Assert.Equal(1, result.FailedCount);
        var item = Assert.Single(result.Items);
        Assert.False(item.Success);
        Assert.Contains("đã lập hóa đơn", item.Message);
    }

    [Fact]
    public async Task SaveBatchMeterReadingsAsync_AtomicPerApartment_ErrorInOneDoesNotPreventOthers()
    {
        await using var context = CreateContext();
        var (building, apartments) = SeedBuildingWithApartments(context, 1, 2);
        var apt1 = apartments[0];
        var apt2 = apartments[1];

        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var input = new BatchMeterReadingSaveInput(
            building.BuildingId,
            2026,
            8,
            [
                new BatchMeterReadingItemInput(apt1.ApartmentId, apt1.ApartmentCode, 100, 50, 20, 25), // Invalid (50 < 100)
                new BatchMeterReadingItemInput(apt2.ApartmentId, apt2.ApartmentCode, 0, 150, 0, 30)   // Valid
            ]);

        var result = await service.SaveBatchMeterReadingsAsync(input, "tech-01");

        Assert.True(result.Success);
        Assert.Equal(2, result.TotalProcessed);
        Assert.Equal(1, result.SavedCount);
        Assert.Equal(1, result.FailedCount);

        var apt2Readings = await context.UtilityMeterReadings
            .Where(r => r.ApartmentId == apt2.ApartmentId)
            .ToListAsync();
        Assert.Equal(2, apt2Readings.Count);

        var apt1Readings = await context.UtilityMeterReadings
            .Where(r => r.ApartmentId == apt1.ApartmentId)
            .ToListAsync();
        Assert.Empty(apt1Readings);
    }
}

