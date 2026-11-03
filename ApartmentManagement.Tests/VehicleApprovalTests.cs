using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ApartmentManagement.Tests;

public class VehicleApprovalTests
{
    private ApplicationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task ApproveVehicle_WhenPending_WithNewCardCode_SetsStatusApprovedAndCreatesCard()
    {
        using var context = CreateInMemoryContext();
        var service = new VehicleService(context);

        var vehicle = new Vehicle { LicensePlate = "29A-12345", VehicleType = "Ô tô", Status = "Chờ duyệt", ApartmentId = 1 };
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        var result = await service.ApproveVehicleAsync(vehicle.VehicleId, "CARD-8888", "Đủ giấy tờ", "admin_1");

        Assert.True(result.Success);
        Assert.Equal("Đã duyệt", vehicle.Status);
        Assert.NotNull(vehicle.ParkingCard);
        Assert.Equal("CARD-8888", vehicle.ParkingCard.CardCode);
        Assert.Equal("Active", vehicle.ParkingCard.Status);
    }

    [Fact]
    public async Task ApproveVehicle_WhenCardCodeAlreadyInUseByAnotherVehicle_ReturnsError()
    {
        using var context = CreateInMemoryContext();
        var service = new VehicleService(context);

        context.ParkingCards.Add(new ParkingCard { CardCode = "CARD-DUPLICATE", Status = "Active", VehicleId = 99 });
        var vehicle = new Vehicle { LicensePlate = "29A-99999", VehicleType = "Xe máy", Status = "Chờ duyệt", ApartmentId = 1 };
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        var result = await service.ApproveVehicleAsync(vehicle.VehicleId, "CARD-DUPLICATE", null, "admin_1");

        Assert.False(result.Success);
        Assert.Contains("đang được sử dụng", result.ErrorMessage);
        Assert.Equal("Chờ duyệt", vehicle.Status);
    }

    [Fact]
    public async Task RejectVehicle_WithoutReason_ReturnsError()
    {
        using var context = CreateInMemoryContext();
        var service = new VehicleService(context);

        var vehicle = new Vehicle { LicensePlate = "29A-11111", Status = "Chờ duyệt", ApartmentId = 1 };
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        var result = await service.RejectVehicleAsync(vehicle.VehicleId, "   ", "admin_1");

        Assert.False(result.Success);
        Assert.Equal("Chờ duyệt", vehicle.Status);
    }

    [Fact]
    public async Task RejectVehicle_WhenPending_WithReason_SetsStatusRejectedAndLocksCard()
    {
        using var context = CreateInMemoryContext();
        var service = new VehicleService(context);

        var vehicle = new Vehicle { LicensePlate = "29A-77777", VehicleType = "Ô tô", Status = "Chờ duyệt", ApartmentId = 1 };
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        var card = new ParkingCard { VehicleId = vehicle.VehicleId, CardCode = "CARD-7777", Status = "Active" };
        context.ParkingCards.Add(card);
        await context.SaveChangesAsync();

        var result = await service.RejectVehicleAsync(vehicle.VehicleId, "Hết vị trí đỗ ô tô", "admin_1");

        Assert.True(result.Success);
        Assert.Equal("Từ chối", vehicle.Status);
        Assert.Equal("Hết vị trí đỗ ô tô", vehicle.ReviewNotes);
        Assert.Equal("Locked", card.Status);
    }

    [Fact]
    public async Task ApproveVehicle_WithoutCardCode_SetsStatusApprovedWithoutCard()
    {
        using var context = CreateInMemoryContext();
        var service = new VehicleService(context);

        var vehicle = new Vehicle { LicensePlate = "29A-55555", VehicleType = "Xe máy", Status = "Chờ duyệt", ApartmentId = 1 };
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        var result = await service.ApproveVehicleAsync(vehicle.VehicleId, null, "Duyệt cấp thẻ sau", "admin_1");

        Assert.True(result.Success);
        Assert.Equal("Đã duyệt", vehicle.Status);
        Assert.Null(vehicle.ParkingCard);
    }

    [Fact]
    public async Task IssueOrReplaceCard_WhenApproved_AssignsCardSuccessfully()
    {
        using var context = CreateInMemoryContext();
        var service = new VehicleService(context);

        var vehicle = new Vehicle { LicensePlate = "29A-66666", VehicleType = "Ô tô", Status = "Đã duyệt", ApartmentId = 1 };
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        var result = await service.IssueOrReplaceCardAsync(vehicle.VehicleId, "CARD-NEW-01", "admin_1");

        Assert.True(result.Success);
        var card = await context.ParkingCards.FirstOrDefaultAsync(c => c.VehicleId == vehicle.VehicleId);
        Assert.NotNull(card);
        Assert.Equal("CARD-NEW-01", card.CardCode);
        Assert.Equal("Active", card.Status);
    }

    [Fact]
    public async Task ReportLostOrLockCard_WhenCardIsActive_SetsStatusLostAndLogsNotes()
    {
        using var context = CreateInMemoryContext();
        var service = new VehicleService(context);

        var vehicle = new Vehicle { LicensePlate = "29A-LOST-01", VehicleType = "Ô tô", Status = "Đã duyệt", ApartmentId = 1 };
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        var card = new ParkingCard { VehicleId = vehicle.VehicleId, CardCode = "CARD-ACTIVE-99", Status = "Active" };
        context.ParkingCards.Add(card);
        await context.SaveChangesAsync();

        var result = await service.ReportLostOrLockCardAsync(vehicle.VehicleId, "Rơi mất trên đường", "resident_user");

        Assert.True(result.Success);
        Assert.Equal("Lost", card.Status);
        Assert.Contains("Khóa thẻ", vehicle.ReviewNotes);
        Assert.Contains("Rơi mất trên đường", vehicle.ReviewNotes);
    }

    [Fact]
    public async Task ReportLostOrLockCard_WhenNoCardExists_ReturnsError()
    {
        using var context = CreateInMemoryContext();
        var service = new VehicleService(context);

        var vehicle = new Vehicle { LicensePlate = "29A-NOCARD", VehicleType = "Xe máy", Status = "Đã duyệt", ApartmentId = 1 };
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        var result = await service.ReportLostOrLockCardAsync(vehicle.VehicleId, "Mất thẻ", "admin_1");

        Assert.False(result.Success);
        Assert.Contains("chưa được cấp thẻ", result.ErrorMessage);
    }

    [Fact]
    public async Task ReportLostOrLockCard_WhenCardAlreadyLostOrLocked_ReturnsError()
    {
        using var context = CreateInMemoryContext();
        var service = new VehicleService(context);

        var vehicle = new Vehicle { LicensePlate = "29A-ALREADY-LOST", VehicleType = "Xe máy", Status = "Đã duyệt", ApartmentId = 1 };
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        var card = new ParkingCard { VehicleId = vehicle.VehicleId, CardCode = "CARD-LOST-88", Status = "Lost" };
        context.ParkingCards.Add(card);
        await context.SaveChangesAsync();

        var result = await service.ReportLostOrLockCardAsync(vehicle.VehicleId, "Báo mất lại", "resident_user");

        Assert.False(result.Success);
        Assert.Contains("đã ở trạng thái khóa trước đó", result.ErrorMessage);
    }

    [Fact]
    public async Task IssueOrReplaceCard_WhenReplacingExistingLostCard_UpdatesCodeAndActivatesCard()
    {
        using var context = CreateInMemoryContext();
        var service = new VehicleService(context);

        var vehicle = new Vehicle { LicensePlate = "29A-REPLACE", VehicleType = "Ô tô", Status = "Đã duyệt", ApartmentId = 1 };
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        var oldCard = new ParkingCard { VehicleId = vehicle.VehicleId, CardCode = "CARD-OLD-LOST", Status = "Lost" };
        context.ParkingCards.Add(oldCard);
        await context.SaveChangesAsync();

        var result = await service.IssueOrReplaceCardAsync(vehicle.VehicleId, "CARD-REPLACED-01", "admin_1");

        Assert.True(result.Success);
        Assert.Equal("CARD-REPLACED-01", oldCard.CardCode);
        Assert.Equal("Active", oldCard.Status);
    }

    [Fact]
    public async Task IssueOrReplaceCard_WhenCardCodeAlreadyActiveOnAnotherVehicle_ReturnsError()
    {
        using var context = CreateInMemoryContext();
        var service = new VehicleService(context);

        context.ParkingCards.Add(new ParkingCard { CardCode = "CARD-EXISTING-ACTIVE", Status = "Active", VehicleId = 55 });
        var vehicle = new Vehicle { LicensePlate = "29A-DUP-TEST", VehicleType = "Xe máy", Status = "Đã duyệt", ApartmentId = 1 };
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        var result = await service.IssueOrReplaceCardAsync(vehicle.VehicleId, "CARD-EXISTING-ACTIVE", "admin_1");

        Assert.False(result.Success);
        Assert.Contains("đang được sử dụng cho một xe khác", result.ErrorMessage);
    }

    [Fact]
    public async Task IssueOrReplaceCard_WhenVehicleNotApproved_ReturnsError()
    {
        using var context = CreateInMemoryContext();
        var service = new VehicleService(context);

        var vehicle = new Vehicle { LicensePlate = "29A-PENDING-CAR", VehicleType = "Ô tô", Status = "Chờ duyệt", ApartmentId = 1 };
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        var result = await service.IssueOrReplaceCardAsync(vehicle.VehicleId, "CARD-NEW-02", "admin_1");

        Assert.False(result.Success);
        Assert.Contains("chỉ có thể cấp hoặc đổi thẻ", result.ErrorMessage!.ToLowerInvariant());
    }

    [Fact]
    public async Task IssueOrReplaceCard_WhenCardCodeIsEmpty_ReturnsError()
    {
        using var context = CreateInMemoryContext();
        var service = new VehicleService(context);

        var vehicle = new Vehicle { LicensePlate = "29A-APPROVED-BLANK", VehicleType = "Ô tô", Status = "Đã duyệt", ApartmentId = 1 };
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        var result = await service.IssueOrReplaceCardAsync(vehicle.VehicleId, "   ", "admin_1");

        Assert.False(result.Success);
        Assert.Contains("Vui lòng nhập mã thẻ từ bãi đỗ", result.ErrorMessage);
    }

    [Fact]
    public async Task IssueOrReplaceCard_WhenCardCodeExistsOnAnotherVehicleWithStatusLost_ReturnsError()
    {
        using var context = CreateInMemoryContext();
        var service = new VehicleService(context);

        // Another vehicle has a card with status 'Lost'
        context.ParkingCards.Add(new ParkingCard { CardCode = "CARD-OTHER-LOST", Status = "Lost", VehicleId = 77 });
        var vehicle = new Vehicle { LicensePlate = "29A-NEW-LOST-TEST", VehicleType = "Xe máy", Status = "Đã duyệt", ApartmentId = 1 };
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        var result = await service.IssueOrReplaceCardAsync(vehicle.VehicleId, "CARD-OTHER-LOST", "admin_1");

        Assert.False(result.Success);
        Assert.Contains("đang được sử dụng cho một xe khác", result.ErrorMessage);
    }

    [Fact]
    public async Task IssueOrReplaceCard_WhenCardCodeExistsOnAnotherVehicleWithStatusLocked_ReturnsError()
    {
        using var context = CreateInMemoryContext();
        var service = new VehicleService(context);

        // Another vehicle has a card with status 'Locked'
        context.ParkingCards.Add(new ParkingCard { CardCode = "CARD-OTHER-LOCKED", Status = "Locked", VehicleId = 88 });
        var vehicle = new Vehicle { LicensePlate = "29A-NEW-LOCKED-TEST", VehicleType = "Ô tô", Status = "Đã duyệt", ApartmentId = 1 };
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        var result = await service.IssueOrReplaceCardAsync(vehicle.VehicleId, "CARD-OTHER-LOCKED", "admin_1");

        Assert.False(result.Success);
        Assert.Contains("đang được sử dụng cho một xe khác", result.ErrorMessage);
    }

    [Fact]
    public async Task IssueOrReplaceCard_WhenUniqueConstraintViolationOccursOnSave_ReturnsFriendlyDuplicateError()
    {
        using var context = CreateFaultInjectingContext();
        var service = new VehicleService(context);

        var vehicle = new Vehicle { LicensePlate = "29A-RACE-01", VehicleType = "Ô tô", Status = "Đã duyệt", ApartmentId = 1 };
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        // Inject simulated unique index violation exception from SQL Server
        context.ExceptionToThrow = new DbUpdateException(
            "Cannot insert duplicate key row in object 'dbo.ParkingCards' with unique index 'UX_ParkingCards_CardCode'. The duplicate key value is (CARD-CONCURRENT).",
            new Exception("Violation of UNIQUE KEY constraint 'UX_ParkingCards_CardCode'."));

        var result = await service.IssueOrReplaceCardAsync(vehicle.VehicleId, "CARD-CONCURRENT", "admin_1");

        Assert.False(result.Success);
        Assert.Contains("đang được sử dụng cho một xe khác", result.ErrorMessage);
    }

    [Fact]
    public async Task IssueOrReplaceCard_WhenUniqueConstraintViolationOccurs_ChangeTrackerRemainsCleanForSubsequentOperations()
    {
        using var context = CreateFaultInjectingContext();
        var service = new VehicleService(context);

        var vehicle = new Vehicle { LicensePlate = "29A-RACE-02", VehicleType = "Ô tô", Status = "Đã duyệt", ApartmentId = 1 };
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        // Simulate unique index violation
        context.ExceptionToThrow = new DbUpdateException(
            "Violation of unique index UX_ParkingCards_CardCode",
            new Exception("UX_ParkingCards_CardCode"));

        var result = await service.IssueOrReplaceCardAsync(vehicle.VehicleId, "CARD-FAIL-RACE", "admin_1");
        Assert.False(result.Success);

        // Now attempt another normal DB operation on the same DbContext instance
        context.Vehicles.Add(new Vehicle { LicensePlate = "29A-SUBSEQUENT", VehicleType = "Xe máy", Status = "Chờ duyệt", ApartmentId = 1 });
        var savedRows = await context.SaveChangesAsync();

        Assert.True(savedRows > 0);
        var subsequentVehicle = await context.Vehicles.FirstOrDefaultAsync(v => v.LicensePlate == "29A-SUBSEQUENT");
        Assert.NotNull(subsequentVehicle);
    }

    [Fact]
    public async Task IssueOrReplaceCard_WhenDbUpdateExceptionUnrelatedToUniqueConstraint_DoesNotMaskAsDuplicateCard()
    {
        using var context = CreateFaultInjectingContext();
        var service = new VehicleService(context);

        var vehicle = new Vehicle { LicensePlate = "29A-UNRELATED-ERR", VehicleType = "Ô tô", Status = "Đã duyệt", ApartmentId = 1 };
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        // Inject an unrelated DB error (e.g. database connection / timeout failure)
        context.ExceptionToThrow = new DbUpdateException(
            "A transport-level error has occurred when sending the request to the server.",
            new Exception("Physical connection is not usable"));

        // Must rethrow rather than masking as duplicate card error
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() =>
            service.IssueOrReplaceCardAsync(vehicle.VehicleId, "CARD-TEST-UNRELATED", "admin_1"));

        Assert.Contains("transport-level error", ex.Message);
    }

    private FaultInjectingDbContext CreateFaultInjectingContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new FaultInjectingDbContext(options);
    }

    private sealed class FaultInjectingDbContext : ApplicationDbContext
    {
        public Exception? ExceptionToThrow { get; set; }

        public FaultInjectingDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow != null)
            {
                var ex = ExceptionToThrow;
                ExceptionToThrow = null; // Throw once
                throw ex;
            }
            return base.SaveChangesAsync(cancellationToken);
        }
    }
}

