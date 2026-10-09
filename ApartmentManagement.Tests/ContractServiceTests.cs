using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ApartmentManagement.Tests;

public sealed class ContractServiceTests
{
    private static readonly DateTime PastStart = new(2020, 1, 1);
    private static readonly DateTime PastEnd = new(2020, 12, 31);

    [Fact]
    public async Task CreateDraft_StoresDraftAndInitialHistory()
    {
        using var fixture = await CreateFixtureAsync();

        var result = await fixture.Service.CreateDraftAsync(ValidDraft(), "admin");

        Assert.True(result.Success);
        Assert.Equal(ApartmentContractStatus.Draft, result.Contract!.Status);
        Assert.Single(result.Contract.StatusHistory);
        Assert.Null(result.Contract.StatusHistory.Single().PreviousStatus);
        Assert.Equal(ApartmentContractStatus.Draft, result.Contract.StatusHistory.Single().NewStatus);
    }

    [Fact]
    public async Task CreateDraft_RejectsDuplicateNormalizedContractCode()
    {
        using var fixture = await CreateFixtureAsync();
        Assert.True((await fixture.Service.CreateDraftAsync(ValidDraft("lease-1"), "admin")).Success);

        var result = await fixture.Service.CreateDraftAsync(ValidDraft(" LEASE-1 "), "admin");

        Assert.False(result.Success);
        Assert.Equal("Mã hợp đồng đã tồn tại.", result.ErrorMessage);
    }

    [Theory]
    [InlineData(0, 0, "Tiền thuê hàng tháng phải lớn hơn 0.")]
    [InlineData(100, -1, "Tiền đặt cọc không thể nhỏ hơn 0.")]
    public async Task CreateDraft_RejectsInvalidFinancialValues(
        decimal rent,
        decimal deposit,
        string expectedError)
    {
        using var fixture = await CreateFixtureAsync();

        var result = await fixture.Service.CreateDraftAsync(
            ValidDraft() with { MonthlyRent = rent, DepositAmount = deposit }, "admin");

        Assert.False(result.Success);
        Assert.Equal(expectedError, result.ErrorMessage);
    }

    [Fact]
    public async Task CreateDraft_RejectsEndDateBeforeStartDate()
    {
        using var fixture = await CreateFixtureAsync();
        var input = ValidDraft() with
        {
            StartDate = new DateTime(2026, 2, 1),
            EndDate = new DateTime(2026, 1, 31)
        };

        var result = await fixture.Service.CreateDraftAsync(input, "admin");

        Assert.False(result.Success);
        Assert.Equal("Ngày kết thúc không thể trước ngày bắt đầu.", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateDraft_RejectsUnknownApartment()
    {
        using var fixture = await CreateFixtureAsync();

        var result = await fixture.Service.CreateDraftAsync(
            ValidDraft() with { ApartmentId = 999 }, "admin");

        Assert.False(result.Success);
        Assert.Equal("Căn hộ không tồn tại.", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateDraft_RejectsActorWithoutSuperAdminRole()
    {
        using var fixture = await CreateFixtureAsync();

        var result = await fixture.Service.CreateDraftAsync(ValidDraft(), "resident-user-1");

        Assert.False(result.Success);
        Assert.Equal("Chỉ SuperAdmin mới được quản lý hợp đồng.", result.ErrorMessage);
    }

    [Theory]
    [InlineData(ContractPartyRole.Lessee)]
    [InlineData(ContractPartyRole.Lessor)]
    public async Task CreateDraft_RequiresBothContractPartyRoles(ContractPartyRole role)
    {
        using var fixture = await CreateFixtureAsync();
        var input = ValidDraft() with
        {
            Parties = [new ContractPartyInput(1, role)]
        };

        var result = await fixture.Service.CreateDraftAsync(input, "admin");

        Assert.False(result.Success);
        Assert.Equal("Hợp đồng phải có ít nhất một bên cho thuê và một bên thuê.", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateDraft_RejectsDuplicateResidentParty()
    {
        using var fixture = await CreateFixtureAsync();
        var input = ValidDraft() with
        {
            Parties =
            [
                new ContractPartyInput(1, ContractPartyRole.Lessor),
                new ContractPartyInput(1, ContractPartyRole.Lessee)
            ]
        };

        var result = await fixture.Service.CreateDraftAsync(input, "admin");

        Assert.False(result.Success);
        Assert.Equal("Mỗi cư dân chỉ được khai báo một lần trong cùng hợp đồng.", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateDraft_RejectsUnknownResident()
    {
        using var fixture = await CreateFixtureAsync();
        var input = ValidDraft() with
        {
            Parties =
            [
                new ContractPartyInput(1, ContractPartyRole.Lessor),
                new ContractPartyInput(999, ContractPartyRole.Lessee)
            ]
        };

        var result = await fixture.Service.CreateDraftAsync(input, "admin");

        Assert.False(result.Success);
        Assert.Equal("Một hoặc nhiều cư dân không tồn tại.", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateDraft_UpdatesDetailsAndParties()
    {
        using var fixture = await CreateFixtureAsync();
        var created = await fixture.Service.CreateDraftAsync(ValidDraft(), "admin");
        created.Contract!.RowVersion = [1];

        var result = await fixture.Service.UpdateDraftAsync(
            created.Contract.ApartmentContractId,
            ValidDraft("lease-updated") with
            {
                MonthlyRent = 1500,
                Parties =
                [
                    new ContractPartyInput(3, ContractPartyRole.Lessor),
                    new ContractPartyInput(2, ContractPartyRole.Lessee)
                ]
            },
            "admin",
            [1]);

        Assert.True(result.Success);
        Assert.Equal("lease-updated", result.Contract!.ContractCode);
        Assert.Equal(1500, result.Contract.MonthlyRent);
        Assert.Contains(result.Contract.Parties, x => x.ResidentId == 3);
        Assert.DoesNotContain(result.Contract.Parties, x => x.ResidentId == 1);
    }

    [Fact]
    public async Task UpdateDraft_RejectsStaleRowVersion()
    {
        using var fixture = await CreateFixtureAsync();
        var created = await fixture.Service.CreateDraftAsync(ValidDraft(), "admin");
        created.Contract!.RowVersion = [2];

        var result = await fixture.Service.UpdateDraftAsync(
            created.Contract.ApartmentContractId,
            ValidDraft("changed"),
            "admin",
            [1]);

        Assert.False(result.Success);
        Assert.Contains("Tải lại dữ liệu", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateDraft_RejectsNonDraftContract()
    {
        using var fixture = await CreateFixtureAsync();
        var active = await CreateActiveAsync(fixture, "active");

        var result = await fixture.Service.UpdateDraftAsync(
            active.ApartmentContractId,
            ValidDraft("changed"),
            "admin",
            active.RowVersion);

        Assert.False(result.Success);
        Assert.Equal("Chỉ có thể chỉnh sửa hợp đồng ở trạng thái nháp.", result.ErrorMessage);
    }

    [Fact]
    public async Task Activate_RecordsStatusHistory()
    {
        using var fixture = await CreateFixtureAsync();
        var draft = await fixture.Service.CreateDraftAsync(ValidDraft(), "admin");
        draft.Contract!.RowVersion = [1];

        var result = await fixture.Service.ActivateAsync(
            draft.Contract.ApartmentContractId, "admin", [1]);

        Assert.True(result.Success);
        Assert.Equal(ApartmentContractStatus.Active, result.Contract!.Status);
        Assert.Equal(2, result.Contract.StatusHistory.Count);
        Assert.Equal(ApartmentContractStatus.Draft, result.Contract.StatusHistory.Last().PreviousStatus);
    }

    [Fact]
    public async Task Activate_RejectsOverlappingActiveContract()
    {
        using var fixture = await CreateFixtureAsync();
        await CreateActiveAsync(fixture, "first");
        var second = await fixture.Service.CreateDraftAsync(
            ValidDraft("second") with { StartDate = PastEnd, EndDate = PastEnd.AddYears(1) }, "admin");
        second.Contract!.RowVersion = [2];

        var result = await fixture.Service.ActivateAsync(
            second.Contract.ApartmentContractId, "admin", [2]);

        Assert.False(result.Success);
        Assert.Contains("bị trùng", result.ErrorMessage);
    }

    [Fact]
    public async Task Complete_RejectsEarlyCompletionWithoutReason()
    {
        using var fixture = await CreateFixtureAsync();
        var draft = await fixture.Service.CreateDraftAsync(ValidDraft() with
        {
            StartDate = DateTime.UtcNow.Date.AddDays(-1),
            EndDate = DateTime.UtcNow.Date.AddDays(10)
        }, "admin");
        draft.Contract!.RowVersion = [1];
        var active = await fixture.Service.ActivateAsync(draft.Contract.ApartmentContractId, "admin", [1]);

        var result = await fixture.Service.CompleteAsync(
            active.Contract!.ApartmentContractId, "admin", [1]);

        Assert.False(result.Success);
        Assert.Equal("Vui lòng nhập lý do khi kết thúc hợp đồng trước hạn.", result.ErrorMessage);
    }

    [Fact]
    public async Task Complete_StoresReasonAndMakesContractTerminal()
    {
        using var fixture = await CreateFixtureAsync();
        var active = await CreateActiveAsync(fixture, "complete");

        var result = await fixture.Service.CompleteAsync(
            active.ApartmentContractId, "admin", [1], "Đã hoàn thành nghĩa vụ");

        Assert.True(result.Success);
        Assert.Equal(ApartmentContractStatus.Completed, result.Contract!.Status);
        Assert.Equal("Đã hoàn thành nghĩa vụ", result.Contract.StatusHistory.Last().Reason);
    }

    [Fact]
    public async Task Terminate_RequiresAndStoresReason()
    {
        using var fixture = await CreateFixtureAsync();
        var active = await CreateActiveAsync(fixture, "terminate");

        var missingReason = await fixture.Service.TerminateAsync(
            active.ApartmentContractId, "admin", [1], " ");
        var result = await fixture.Service.TerminateAsync(
            active.ApartmentContractId, "admin", [1], "Hai bên thỏa thuận");

        Assert.False(missingReason.Success);
        Assert.True(result.Success);
        Assert.Equal("Hai bên thỏa thuận", result.Contract!.StatusHistory.Last().Reason);
    }

    [Fact]
    public async Task Cancel_RequiresAndStoresReason()
    {
        using var fixture = await CreateFixtureAsync();
        var draft = await fixture.Service.CreateDraftAsync(ValidDraft(), "admin");
        draft.Contract!.RowVersion = [1];

        var missingReason = await fixture.Service.CancelAsync(
            draft.Contract.ApartmentContractId, "admin", [1], "");
        var result = await fixture.Service.CancelAsync(
            draft.Contract.ApartmentContractId, "admin", [1], "Khách hàng yêu cầu hủy");

        Assert.False(missingReason.Success);
        Assert.True(result.Success);
        Assert.Equal(ApartmentContractStatus.Cancelled, result.Contract!.Status);
        Assert.Equal("Khách hàng yêu cầu hủy", result.Contract.StatusHistory.Last().Reason);
    }

    [Fact]
    public async Task TerminalContract_CannotBeReactivated()
    {
        using var fixture = await CreateFixtureAsync();
        var draft = await fixture.Service.CreateDraftAsync(ValidDraft(), "admin");
        draft.Contract!.RowVersion = [1];
        var cancelled = await fixture.Service.CancelAsync(
            draft.Contract.ApartmentContractId, "admin", [1], "Không tiếp tục");

        var result = await fixture.Service.ActivateAsync(
            cancelled.Contract!.ApartmentContractId, "admin", [1]);

        Assert.False(result.Success);
        Assert.Equal("Chỉ có thể kích hoạt hợp đồng ở trạng thái nháp.", result.ErrorMessage);
    }

    [Fact]
    public async Task GetForResidentUser_ReturnsOnlyParticipatingContracts()
    {
        using var fixture = await CreateFixtureAsync();
        await fixture.Service.CreateDraftAsync(ValidDraft("resident-one"), "admin");
        await fixture.Service.CreateDraftAsync(
            ValidDraft("resident-two") with
            {
                Parties =
                [
                    new ContractPartyInput(2, ContractPartyRole.Lessor),
                    new ContractPartyInput(3, ContractPartyRole.Lessee)
                ]
            },
            "admin");

        var result = await fixture.Service.GetForResidentUserAsync("resident-user-1");

        Assert.Single(result);
        Assert.Equal("resident-one", result[0].ContractCode);
    }

    private static async Task<ApartmentContract> CreateActiveAsync(Fixture fixture, string code)
    {
        var draft = await fixture.Service.CreateDraftAsync(ValidDraft(code), "admin");
        Assert.True(draft.Success, draft.ErrorMessage);
        draft.Contract!.RowVersion = [1];
        var active = await fixture.Service.ActivateAsync(draft.Contract.ApartmentContractId, "admin", [1]);
        Assert.True(active.Success, active.ErrorMessage);
        active.Contract!.RowVersion = [1];
        return active.Contract;
    }

    private static ContractDraftInput ValidDraft(string code = "LEASE-1") => new()
    {
        ContractCode = code,
        ApartmentId = 1,
        StartDate = PastStart,
        EndDate = PastEnd,
        MonthlyRent = 1000,
        DepositAmount = 500,
        Parties =
        [
            new ContractPartyInput(1, ContractPartyRole.Lessor),
            new ContractPartyInput(2, ContractPartyRole.Lessee)
        ]
    };

    private static async Task<Fixture> CreateFixtureAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new ApplicationDbContext(options);
        var admin = new ApplicationUser { Id = "admin", UserName = "admin" };
        var residentUser1 = new ApplicationUser { Id = "resident-user-1", UserName = "resident1" };
        var residentUser2 = new ApplicationUser { Id = "resident-user-2", UserName = "resident2" };
        var residentUser3 = new ApplicationUser { Id = "resident-user-3", UserName = "resident3" };
        var superAdminRole = new Microsoft.AspNetCore.Identity.IdentityRole
        {
            Id = "superadmin-role",
            Name = "SuperAdmin",
            NormalizedName = "SUPERADMIN"
        };
        var building = new Building { BuildingCode = "B1", BuildingName = "Building", NumberOfFloors = 5 };
        var apartment = new Apartment
        {
            Building = building,
            BuildingId = 1,
            ApartmentCode = "A-1",
            Floor = 1,
            Area = 60,
            Status = "Trống"
        };
        context.AddRange(
            admin, residentUser1, residentUser2, residentUser3, superAdminRole,
            new Microsoft.AspNetCore.Identity.IdentityUserRole<string>
            {
                UserId = admin.Id,
                RoleId = superAdminRole.Id
            },
            building, apartment,
            new Resident { ResidentId = 1, User = residentUser1, UserId = residentUser1.Id, CitizenId = "000000001" },
            new Resident { ResidentId = 2, User = residentUser2, UserId = residentUser2.Id, CitizenId = "000000002" },
            new Resident { ResidentId = 3, User = residentUser3, UserId = residentUser3.Id, CitizenId = "000000003" });
        await context.SaveChangesAsync();
        return new Fixture(context, new ContractService(context, NullLogger<ContractService>.Instance));
    }

    private sealed class Fixture(ApplicationDbContext context, ContractService service) : IDisposable
    {
        public ApplicationDbContext Context { get; } = context;
        public ContractService Service { get; } = service;
        public void Dispose() => Context.Dispose();
    }
}
