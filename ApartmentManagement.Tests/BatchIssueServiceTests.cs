using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ApartmentManagement.Tests;

public sealed class BatchIssueServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static (ApplicationUser manager, Building building) SeedBuilding(ApplicationDbContext context, int buildingId = 1)
    {
        var manager = new ApplicationUser
        {
            Id = $"manager-{buildingId}",
            UserName = $"manager{buildingId}@test.local",
            Email = $"manager{buildingId}@test.local",
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
        context.SaveChanges();
        return (manager, building);
    }

    private static ApartmentInvoice AddInvoice(
        ApplicationDbContext context,
        Building building,
        int apartmentId,
        string aptCode,
        int year,
        int month,
        ApartmentInvoiceStatus status,
        decimal totalAmount = 500000)
    {
        var apartment = new Apartment
        {
            ApartmentId = apartmentId,
            BuildingId = building.BuildingId,
            Building = building,
            ApartmentCode = aptCode,
            Floor = 1,
            Area = 50,
            Status = "Đang sử dụng"
        };
        var user = new ApplicationUser
        {
            Id = $"resident-user-{apartmentId}",
            UserName = $"res{apartmentId}@test.local",
            Email = $"res{apartmentId}@test.local",
            FullName = $"Resident {aptCode}",
            IsActive = true
        };
        var resident = new Resident
        {
            ResidentId = apartmentId * 10,
            UserId = user.Id,
            User = user,
            CitizenId = $"0012026{apartmentId:D5}"
        };
        var invoice = new ApartmentInvoice
        {
            InvoiceCode = $"INV-{year:D4}{month:D2}-{apartmentId:D4}",
            ApartmentId = apartmentId,
            Apartment = apartment,
            ResidentId = resident.ResidentId,
            Resident = resident,
            BillingYear = year,
            BillingMonth = month,
            DueDate = new DateTime(year, month, 1).AddMonths(1).AddDays(14),
            ApartmentArea = 50,
            TotalAmount = totalAmount,
            Status = status,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = "test-manager",
            RowVersion = [1, 2, 3, 4],
            Lines =
            [
                new ApartmentInvoiceLine
                {
                    ChargeType = FeeChargeType.Management,
                    Description = "Phí quản lý",
                    Quantity = 50,
                    UnitRate = 5000,
                    Amount = 250000
                },
                new ApartmentInvoiceLine
                {
                    ChargeType = FeeChargeType.Electricity,
                    Description = "Tiền điện",
                    Quantity = 50,
                    UnitRate = 3000,
                    Amount = 150000
                },
                new ApartmentInvoiceLine
                {
                    ChargeType = FeeChargeType.Water,
                    Description = "Tiền nước",
                    Quantity = 10,
                    UnitRate = 10000,
                    Amount = 100000
                }
            ]
        };

        context.AddRange(apartment, user, resident, invoice);
        context.SaveChanges();
        return invoice;
    }

    [Fact]
    public async Task GetDraftInvoicesForBatchIssueAsync_ReturnsOnlyDraftInvoicesInTargetBuildingAndPeriod()
    {
        await using var context = CreateContext();
        var (_, bld1) = SeedBuilding(context, 1);
        var (_, bld2) = SeedBuilding(context, 2);

        // Building 1, 2026-08: 2 Drafts, 1 Issued, 1 Void
        var d1 = AddInvoice(context, bld1, 101, "A-101", 2026, 8, ApartmentInvoiceStatus.Draft);
        var d2 = AddInvoice(context, bld1, 102, "A-102", 2026, 8, ApartmentInvoiceStatus.Draft);
        AddInvoice(context, bld1, 103, "A-103", 2026, 8, ApartmentInvoiceStatus.Issued);
        AddInvoice(context, bld1, 104, "A-104", 2026, 8, ApartmentInvoiceStatus.Void);

        // Building 1, 2026-09: 1 Draft
        AddInvoice(context, bld1, 105, "A-105", 2026, 9, ApartmentInvoiceStatus.Draft);

        // Building 2, 2026-08: 1 Draft
        AddInvoice(context, bld2, 201, "B-201", 2026, 8, ApartmentInvoiceStatus.Draft);

        var service = new BillingService(context, NullLogger<BillingService>.Instance);

        var drafts = await service.GetDraftInvoicesForBatchIssueAsync(1, 2026, 8);

        Assert.Equal(2, drafts.Count);
        Assert.Contains(drafts, x => x.InvoiceId == d1.ApartmentInvoiceId && x.ApartmentCode == "A-101");
        Assert.Contains(drafts, x => x.InvoiceId == d2.ApartmentInvoiceId && x.ApartmentCode == "A-102");
        Assert.All(drafts, d =>
        {
            Assert.Equal(250000, d.ManagementFee);
            Assert.Equal(150000, d.ElectricityFee);
            Assert.Equal(100000, d.WaterFee);
            Assert.Equal(500000, d.TotalAmount);
            Assert.NotEmpty(d.RowVersionToken);
        });
    }

    [Fact]
    public async Task IssueBuildingInvoicesAsync_ValidSelectedInvoices_UpdatesStatusToIssued()
    {
        await using var context = CreateContext();
        var (_, building) = SeedBuilding(context, 1);
        var inv1 = AddInvoice(context, building, 101, "A-101", 2026, 8, ApartmentInvoiceStatus.Draft);
        var inv2 = AddInvoice(context, building, 102, "A-102", 2026, 8, ApartmentInvoiceStatus.Draft);

        var service = new BillingService(context, NullLogger<BillingService>.Instance);
        var input = new BatchIssueInput(
            building.BuildingId,
            2026,
            8,
            [
                new BatchIssueItemInput(inv1.ApartmentInvoiceId, inv1.RowVersion),
                new BatchIssueItemInput(inv2.ApartmentInvoiceId, inv2.RowVersion)
            ]);

        var result = await service.IssueBuildingInvoicesAsync(input, "accountant-user");

        Assert.True(result.Success);
        Assert.Equal(2, result.TotalSelected);
        Assert.Equal(2, result.IssuedCount);
        Assert.Equal(0, result.AlreadyIssuedCount);
        Assert.Equal(0, result.FailedCount);

        var updated1 = await context.ApartmentInvoices.FindAsync(inv1.ApartmentInvoiceId);
        var updated2 = await context.ApartmentInvoices.FindAsync(inv2.ApartmentInvoiceId);

        Assert.Equal(ApartmentInvoiceStatus.Issued, updated1!.Status);
        Assert.NotNull(updated1.IssuedAtUtc);
        Assert.Equal("accountant-user", updated1.IssuedByUserId);

        Assert.Equal(ApartmentInvoiceStatus.Issued, updated2!.Status);
        Assert.NotNull(updated2.IssuedAtUtc);
    }

    [Fact]
    public async Task IssueBuildingInvoicesAsync_AlreadyIssuedInvoice_MarksAsAlreadyIssuedSafely()
    {
        await using var context = CreateContext();
        var (_, building) = SeedBuilding(context, 1);
        var inv1 = AddInvoice(context, building, 101, "A-101", 2026, 8, ApartmentInvoiceStatus.Issued);
        var inv2 = AddInvoice(context, building, 102, "A-102", 2026, 8, ApartmentInvoiceStatus.Draft);

        var service = new BillingService(context, NullLogger<BillingService>.Instance);
        var input = new BatchIssueInput(
            building.BuildingId,
            2026,
            8,
            [
                new BatchIssueItemInput(inv1.ApartmentInvoiceId, inv1.RowVersion),
                new BatchIssueItemInput(inv2.ApartmentInvoiceId, inv2.RowVersion)
            ]);

        var result = await service.IssueBuildingInvoicesAsync(input, "accountant-user");

        Assert.True(result.Success);
        Assert.Equal(2, result.TotalSelected);
        Assert.Equal(1, result.IssuedCount);
        Assert.Equal(1, result.AlreadyIssuedCount);
        Assert.Equal(0, result.FailedCount);

        var item1 = result.Items.Single(x => x.InvoiceId == inv1.ApartmentInvoiceId);
        Assert.Equal(BatchIssueItemStatus.AlreadyIssued, item1.Status);

        var item2 = result.Items.Single(x => x.InvoiceId == inv2.ApartmentInvoiceId);
        Assert.Equal(BatchIssueItemStatus.Issued, item2.Status);
    }

    [Fact]
    public async Task IssueBuildingInvoicesAsync_VoidInvoice_MarksAsInvalidStatusWithoutStoppingBatch()
    {
        await using var context = CreateContext();
        var (_, building) = SeedBuilding(context, 1);
        var inv1 = AddInvoice(context, building, 101, "A-101", 2026, 8, ApartmentInvoiceStatus.Void);
        var inv2 = AddInvoice(context, building, 102, "A-102", 2026, 8, ApartmentInvoiceStatus.Draft);

        var service = new BillingService(context, NullLogger<BillingService>.Instance);
        var input = new BatchIssueInput(
            building.BuildingId,
            2026,
            8,
            [
                new BatchIssueItemInput(inv1.ApartmentInvoiceId, inv1.RowVersion),
                new BatchIssueItemInput(inv2.ApartmentInvoiceId, inv2.RowVersion)
            ]);

        var result = await service.IssueBuildingInvoicesAsync(input, "accountant-user");

        Assert.True(result.Success);
        Assert.Equal(2, result.TotalSelected);
        Assert.Equal(1, result.IssuedCount);
        Assert.Equal(1, result.FailedCount);

        var item1 = result.Items.Single(x => x.InvoiceId == inv1.ApartmentInvoiceId);
        Assert.Equal(BatchIssueItemStatus.InvalidStatus, item1.Status);

        var item2 = result.Items.Single(x => x.InvoiceId == inv2.ApartmentInvoiceId);
        Assert.Equal(BatchIssueItemStatus.Issued, item2.Status);
    }

    [Fact]
    public async Task IssueBuildingInvoicesAsync_EmptySelection_ReturnsValidationFailure()
    {
        await using var context = CreateContext();
        var service = new BillingService(context, NullLogger<BillingService>.Instance);
        var input = new BatchIssueInput(1, 2026, 8, []);

        var result = await service.IssueBuildingInvoicesAsync(input, "accountant-user");

        Assert.False(result.Success);
        Assert.Equal(0, result.TotalSelected);
    }
}

