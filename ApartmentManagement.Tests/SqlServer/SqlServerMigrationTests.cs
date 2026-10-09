using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ApartmentManagement.Tests.SqlServer;

public sealed class SqlServerMigrationTests
{
    private const string LegacyMigration = "20260930151344_ApartmentModule";

    [SqlServerFact]
    public async Task LatestMigrations_CreateExpectedSqlServerConstraints()
    {
        await using var database = SqlServerTestDatabase.Create();
        await using var context = database.CreateContext();
        await context.Database.MigrateAsync();

        var names = await context.Database.SqlQueryRaw<string>(
            "SELECT [name] AS [Value] FROM sys.indexes WHERE [name] = 'UX_ApartmentResidents_ActiveAssignment' " +
            "UNION ALL SELECT [name] FROM sys.check_constraints WHERE [name] IN " +
            "('CK_Residents_CitizenId_Format', 'CK_Residents_DateOfBirth_Minimum', 'CK_ApartmentResidents_MoveOutDate')")
            .ToListAsync();

        Assert.Contains("UX_ApartmentResidents_ActiveAssignment", names);
        Assert.Contains("CK_Residents_CitizenId_Format", names);
        Assert.Contains("CK_Residents_DateOfBirth_Minimum", names);
        Assert.Contains("CK_ApartmentResidents_MoveOutDate", names);
    }

    [SqlServerFact]
    public async Task ApartmentBillingMigration_CreatesBillingIndexesConstraintsAndRowVersion()
    {
        await using var database = SqlServerTestDatabase.Create();
        await using var context = database.CreateContext();
        await context.Database.MigrateAsync();

        var names = await context.Database.SqlQueryRaw<string>(
            "SELECT [name] AS [Value] FROM sys.indexes WHERE [name] IN " +
            "('IX_FeeTariffs_BuildingId_ChargeType_EffectiveFrom', " +
            "'IX_UtilityMeterReadings_ApartmentId_UtilityType_BillingYear_BillingMonth', " +
            "'IX_ApartmentInvoices_ApartmentId_BillingYear_BillingMonth') " +
            "UNION ALL SELECT [name] FROM sys.check_constraints WHERE [name] IN " +
            "('CK_FeeTariffs_ChargeType', 'CK_UtilityMeterReadings_Values', " +
            "'CK_ApartmentInvoices_IssueMetadata', 'CK_ApartmentInvoiceLines_Readings')")
            .ToListAsync();

        Assert.Contains("IX_FeeTariffs_BuildingId_ChargeType_EffectiveFrom", names);
        Assert.Contains("IX_UtilityMeterReadings_ApartmentId_UtilityType_BillingYear_BillingMonth", names);
        Assert.Contains("IX_ApartmentInvoices_ApartmentId_BillingYear_BillingMonth", names);
        Assert.Contains("CK_FeeTariffs_ChargeType", names);
        Assert.Contains("CK_UtilityMeterReadings_Values", names);
        Assert.Contains("CK_ApartmentInvoices_IssueMetadata", names);
        Assert.Contains("CK_ApartmentInvoiceLines_Readings", names);

        var rowVersionCount = await context.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS [Value] FROM sys.columns c " +
            "INNER JOIN sys.tables t ON t.object_id = c.object_id " +
            "WHERE t.name = 'ApartmentInvoices' AND c.name = 'RowVersion' " +
            "AND TYPE_NAME(c.user_type_id) = 'timestamp'")
            .SingleAsync();
        Assert.Equal(1, rowVersionCount);
    }

    [SqlServerFact]
    public async Task SimulatedPaymentsMigration_CreatesUniquePaymentConstraintsAndRowVersion()
    {
        await using var database = SqlServerTestDatabase.Create();
        await using var context = database.CreateContext();
        await context.Database.MigrateAsync();

        var names = await context.Database.SqlQueryRaw<string>(
            "SELECT [name] AS [Value] FROM sys.indexes WHERE [name] IN " +
            "('IX_PaymentTransactions_ApartmentInvoiceId', 'IX_PaymentTransactions_Reference') " +
            "UNION ALL SELECT [name] FROM sys.check_constraints WHERE [name] IN " +
            "('CK_PaymentTransactions_Amount', 'CK_PaymentTransactions_Status')")
            .ToListAsync();
        Assert.Contains("IX_PaymentTransactions_ApartmentInvoiceId", names);
        Assert.Contains("IX_PaymentTransactions_Reference", names);
        Assert.Contains("CK_PaymentTransactions_Amount", names);
        Assert.Contains("CK_PaymentTransactions_Status", names);

        var rowVersionCount = await context.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS [Value] FROM sys.columns c " +
            "INNER JOIN sys.tables t ON t.object_id = c.object_id " +
            "WHERE t.name = 'PaymentTransactions' AND c.name = 'RowVersion' " +
            "AND TYPE_NAME(c.user_type_id) = 'timestamp'")
            .SingleAsync();
        Assert.Equal(1, rowVersionCount);
    }

    [SqlServerFact]
    public async Task LegacyOccupancy_IsPreservedWhenMigratingToLatest()
    {
        await using var database = SqlServerTestDatabase.Create();
        await using var context = database.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(LegacyMigration);

        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO [AspNetUsers] ([Id],[FullName],[IsActive],[CreatedAt],[UserName],[NormalizedUserName],[EmailConfirmed],[PhoneNumberConfirmed],[TwoFactorEnabled],[LockoutEnabled],[AccessFailedCount])
            VALUES ('legacy-user',N'Cư dân cũ',1,SYSUTCDATETIME(),'legacy','LEGACY',1,0,0,1,0);
            INSERT INTO [Buildings] ([BuildingCode],[BuildingName],[NumberOfFloors],[CreatedAt]) VALUES ('LEGACY',N'Tòa cũ',10,SYSUTCDATETIME());
            DECLARE @buildingId int = SCOPE_IDENTITY();
            INSERT INTO [Apartments] ([BuildingId],[ApartmentCode],[Floor],[Area],[Status]) VALUES (@buildingId,'A-101',1,60,N'Đang sử dụng');
            DECLARE @apartmentId int = SCOPE_IDENTITY();
            INSERT INTO [Residents] ([UserId],[CitizenId]) VALUES ('legacy-user','000000000099');
            DECLARE @residentId int = SCOPE_IDENTITY();
            INSERT INTO [ApartmentResidents] ([ApartmentId],[ResidentId],[Relationship],[MoveInDate],[IsOwner])
            VALUES (@apartmentId,@residentId,N'Chủ hộ','2025-01-01',1);
            """);

        await migrator.MigrateAsync();
        var occupancy = await context.ApartmentResidents.AsNoTracking().SingleAsync();
        Assert.True(occupancy.ApartmentResidentId > 0);
        Assert.Equal(new DateTime(2025, 1, 1), occupancy.MoveInDate);
        Assert.Null(occupancy.MoveOutDate);
    }

    [SqlServerFact]
    public async Task ConcurrentMoveIn_DoesNotCreateDuplicateActiveOccupancy()
    {
        await using var database = SqlServerTestDatabase.Create();
        await using (var setup = database.CreateContext())
        {
            await setup.Database.MigrateAsync();
            var building = new Building { BuildingCode = "T1", BuildingName = "Tòa 1", NumberOfFloors = 5 };
            var apartment = new Apartment { Building = building, ApartmentCode = "101", Floor = 1, Area = 60, Status = "Trống" };
            var user = new ApplicationUser { Id = "concurrent-user", UserName = "resident@test.local", FullName = "Cư dân", IsActive = true };
            var resident = new Resident { User = user, UserId = user.Id, CitizenId = "000000000088" };
            setup.AddRange(building, apartment, user, resident);
            await setup.SaveChangesAsync();
        }

        int apartmentId;
        int residentId;
        await using (var read = database.CreateContext())
        {
            apartmentId = await read.Apartments.Select(x => x.ApartmentId).SingleAsync();
            residentId = await read.Residents.Select(x => x.ResidentId).SingleAsync();
        }

        async Task<(bool Success, string? ErrorMessage)> AttemptAsync()
        {
            await using var context = database.CreateContext();
            var service = new OccupancyService(context, NullLogger<OccupancyService>.Instance);
            var result = await service.MoveInAsync(new MoveInViewModel
            {
                ApartmentId = apartmentId,
                ResidentId = residentId,
                Relationship = "Thành viên",
                MoveInDate = DateTime.Today
            });
            return (result.Success, result.ErrorMessage);
        }

        var results = await Task.WhenAll(AttemptAsync(), AttemptAsync());
        await using var verify = database.CreateContext();
        Assert.Single(await verify.ApartmentResidents.Where(x => x.MoveOutDate == null).ToListAsync());
        Assert.Single(results, x => x.Success);
        Assert.All(results.Where(x => !x.Success), x => Assert.DoesNotContain("SqlException", x.ErrorMessage));
    }

    [SqlServerFact]
    public async Task Rollback_IsRejectedAfterRepeatedOccupancyHistoryExists()
    {
        await using var database = SqlServerTestDatabase.Create();
        await using var context = database.CreateContext();
        await context.Database.MigrateAsync();
        await SeedRepeatedHistoryAsync(context);

        var migrator = context.GetService<IMigrator>();
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => migrator.MigrateAsync("20261009042745_ResidentValidationAndIntegrity"));
        Assert.Contains("Không thể rollback", exception.ToString());
        Assert.Equal(2, await context.ApartmentResidents.CountAsync());
    }

    private static async Task SeedRepeatedHistoryAsync(ApplicationDbContext context)
    {
        var building = new Building { BuildingCode = "R1", BuildingName = "Rollback", NumberOfFloors = 5 };
        var apartment = new Apartment { Building = building, ApartmentCode = "101", Floor = 1, Area = 60, Status = "Đang sử dụng" };
        var user = new ApplicationUser { Id = "rollback-user", UserName = "rollback@test.local", FullName = "Rollback User", IsActive = true };
        var resident = new Resident { User = user, UserId = user.Id, CitizenId = "000000000077" };
        context.AddRange(building, apartment, user, resident);
        context.ApartmentResidents.AddRange(
            new ApartmentResident { Apartment = apartment, Resident = resident, Relationship = "Thành viên", MoveInDate = new DateTime(2024, 1, 1), MoveOutDate = new DateTime(2024, 6, 1) },
            new ApartmentResident { Apartment = apartment, Resident = resident, Relationship = "Thành viên", MoveInDate = new DateTime(2025, 1, 1) });
        await context.SaveChangesAsync();
    }
}
