using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ApartmentManagement.Tests.SqlServer;

public sealed class SqlServerContractServiceTests
{
    private static readonly DateTime LeaseStart = new(2027, 1, 1);
    private static readonly DateTime LeaseEnd = new(2027, 12, 31);

    [SqlServerFact]
    public async Task ConcurrentActivation_AllowsOnlyOneOverlappingLease()
    {
        await using var database = SqlServerTestDatabase.Create();
        int apartmentId;
        int[] residentIds;
        byte[] firstRowVersion;
        byte[] secondRowVersion;
        int firstContractId;
        int secondContractId;

        await using (var setup = database.CreateContext())
        {
            await setup.Database.MigrateAsync();
            (apartmentId, residentIds) = await SeedApartmentAndResidentsAsync(setup);
            var service = new ContractService(setup, NullLogger<ContractService>.Instance);
            var first = await service.CreateDraftAsync(Draft("SQL-OVERLAP-1", apartmentId, residentIds), "sql-admin");
            var second = await service.CreateDraftAsync(Draft("SQL-OVERLAP-2", apartmentId, residentIds), "sql-admin");
            Assert.True(first.Success, first.ErrorMessage);
            Assert.True(second.Success, second.ErrorMessage);
            firstContractId = first.Contract!.ApartmentContractId;
            firstRowVersion = first.Contract.RowVersion.ToArray();
            secondContractId = second.Contract!.ApartmentContractId;
            secondRowVersion = second.Contract.RowVersion.ToArray();
        }

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ready = 0;
        async Task<ContractServiceResult> ActivateAsync(int contractId, byte[] rowVersion)
        {
            await using var context = database.CreateContext();
            var service = new ContractService(context, NullLogger<ContractService>.Instance);
            if (Interlocked.Increment(ref ready) == 2) gate.TrySetResult();
            await gate.Task;
            return await service.ActivateAsync(contractId, "sql-admin", rowVersion);
        }

        var results = await Task.WhenAll(
            ActivateAsync(firstContractId, firstRowVersion),
            ActivateAsync(secondContractId, secondRowVersion));

        await using var verify = database.CreateContext();
        Assert.Single(results, x => x.Success);
        Assert.Single(results, x => !x.Success);
        Assert.Equal(1, await verify.ApartmentContracts.CountAsync(
            x => x.ApartmentId == apartmentId && x.Status == ApartmentContractStatus.Active));
    }

    [SqlServerFact]
    public async Task UpdateDraft_RejectsStaleSqlServerRowVersion()
    {
        await using var database = SqlServerTestDatabase.Create();
        int contractId;
        int apartmentId;
        int[] residentIds;
        byte[] originalRowVersion;

        await using (var setup = database.CreateContext())
        {
            await setup.Database.MigrateAsync();
            (apartmentId, residentIds) = await SeedApartmentAndResidentsAsync(setup);
            var service = new ContractService(setup, NullLogger<ContractService>.Instance);
            var created = await service.CreateDraftAsync(Draft("SQL-ROWVERSION", apartmentId, residentIds), "sql-admin");
            Assert.True(created.Success, created.ErrorMessage);
            contractId = created.Contract!.ApartmentContractId;
            originalRowVersion = created.Contract.RowVersion.ToArray();
        }

        await using (var update = database.CreateContext())
        {
            var service = new ContractService(update, NullLogger<ContractService>.Instance);
            var result = await service.UpdateDraftAsync(
                contractId,
                Draft("SQL-ROWVERSION-UPDATED", apartmentId, residentIds),
                "sql-admin",
                originalRowVersion);
            Assert.True(result.Success, result.ErrorMessage);
            Assert.NotEqual(originalRowVersion, result.Contract!.RowVersion);
        }

        await using var stale = database.CreateContext();
        var staleService = new ContractService(stale, NullLogger<ContractService>.Instance);
        var rejected = await staleService.UpdateDraftAsync(
            contractId,
            Draft("SQL-ROWVERSION-STALE", apartmentId, residentIds),
            "sql-admin",
            originalRowVersion);

        Assert.False(rejected.Success);
        Assert.Contains("Tải lại dữ liệu", rejected.ErrorMessage);
    }

    private static async Task<(int ApartmentId, int[] ResidentIds)> SeedApartmentAndResidentsAsync(
        ApartmentManagement.Data.ApplicationDbContext context)
    {
        var admin = new ApplicationUser
        {
            Id = "sql-admin",
            UserName = "sql-admin",
            NormalizedUserName = "SQL-ADMIN",
            FullName = "SQL Admin",
            IsActive = true
        };
        var lessorUser = NewUser("sql-lessor");
        var lesseeUser = NewUser("sql-lessee");
        var role = new Microsoft.AspNetCore.Identity.IdentityRole
        {
            Id = "sql-superadmin-role",
            Name = "SuperAdmin",
            NormalizedName = "SUPERADMIN"
        };
        var building = new Building
        {
            BuildingCode = "SQL-B1",
            BuildingName = "SQL Building",
            NumberOfFloors = 5
        };
        var apartment = new Apartment
        {
            Building = building,
            ApartmentCode = "SQL-A1",
            Floor = 1,
            Area = 60,
            Status = "Trống"
        };
        var lessor = new Resident
        {
            User = lessorUser,
            UserId = lessorUser.Id,
            CitizenId = "900000001"
        };
        var lessee = new Resident
        {
            User = lesseeUser,
            UserId = lesseeUser.Id,
            CitizenId = "900000002"
        };
        context.AddRange(
            admin,
            lessorUser,
            lesseeUser,
            role,
            new Microsoft.AspNetCore.Identity.IdentityUserRole<string>
            {
                UserId = admin.Id,
                RoleId = role.Id
            },
            building,
            apartment,
            lessor,
            lessee);
        await context.SaveChangesAsync();
        return (apartment.ApartmentId, [lessor.ResidentId, lessee.ResidentId]);
    }

    private static ApplicationUser NewUser(string id) => new()
    {
        Id = id,
        UserName = id,
        NormalizedUserName = id.ToUpperInvariant(),
        FullName = id,
        IsActive = true
    };

    private static ContractDraftInput Draft(string code, int apartmentId, int[] residentIds) => new()
    {
        ContractCode = code,
        ApartmentId = apartmentId,
        StartDate = LeaseStart,
        EndDate = LeaseEnd,
        MonthlyRent = 1200,
        DepositAmount = 800,
        Parties =
        [
            new ContractPartyInput(residentIds[0], ContractPartyRole.Lessor),
            new ContractPartyInput(residentIds[1], ContractPartyRole.Lessee)
        ]
    };
}
