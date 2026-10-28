using System.Security.Claims;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ApartmentManagement.Tests;

file static class ApartmentContractTestExtensions
{
    public static void SetContractCode(this ApartmentContract contract, string contractCode)
    {
        typeof(ApartmentContract).GetProperty(nameof(ApartmentContract.ContractCode))!
            .SetValue(contract, contractCode.Trim());
        typeof(ApartmentContract).GetProperty(nameof(ApartmentContract.ContractCodeNormalized))!
            .SetValue(contract, contractCode.Trim().ToUpperInvariant());
    }
}

public sealed class SearchServiceTests
{
    private static ApplicationDbContext CreateTestDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new ApplicationDbContext(options);

        var buildingRuby = new Building
        {
            BuildingId = 1,
            BuildingCode = "RUBY-1",
            BuildingName = "Tòa Ruby 1",
            Address = "123 Đường Số 1",
            NumberOfFloors = 20
        };

        var buildingDiamond = new Building
        {
            BuildingId = 2,
            BuildingCode = "DIA-1",
            BuildingName = "Tòa Diamond",
            Address = "456 Đường Số 2",
            NumberOfFloors = 25
        };

        var apt101 = new Apartment
        {
            ApartmentId = 1,
            ApartmentCode = "101",
            BuildingId = 1,
            Building = buildingRuby,
            Floor = 1,
            Area = 65,
            Status = "Đang sử dụng"
        };

        var apt102 = new Apartment
        {
            ApartmentId = 2,
            ApartmentCode = "102",
            BuildingId = 1,
            Building = buildingRuby,
            Floor = 1,
            Area = 70,
            Status = "Trống"
        };

        var apt201 = new Apartment
        {
            ApartmentId = 3,
            ApartmentCode = "201",
            BuildingId = 2,
            Building = buildingDiamond,
            Floor = 2,
            Area = 85,
            Status = "Đang sử dụng"
        };

        var userR1 = new ApplicationUser
        {
            Id = "user-r1",
            FullName = "Nguyễn Văn An",
            Email = "an@gmail.com",
            PhoneNumber = "0901234567",
            IsActive = true
        };

        var userR2 = new ApplicationUser
        {
            Id = "user-r2",
            FullName = "Trần Thị Bình",
            Email = "binh@gmail.com",
            PhoneNumber = "0907654321",
            IsActive = true
        };

        var resident1 = new Resident
        {
            ResidentId = 1,
            UserId = "user-r1",
            User = userR1,
            CitizenId = "079090000001"
        };

        var resident2 = new Resident
        {
            ResidentId = 2,
            UserId = "user-r2",
            User = userR2,
            CitizenId = "079090000002"
        };

        var contract1 = new ApartmentContract
        {
            ApartmentContractId = 1,
            ApartmentId = 1,
            Apartment = apt101,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            MonthlyRent = 10000000,
            DepositAmount = 20000000,
            Status = ApartmentContractStatus.Active,
            CreatedAtUtc = DateTime.UtcNow,
            Parties = new List<ContractParty>
            {
                new() { ResidentId = 1, Resident = resident1, Role = ContractPartyRole.Lessee }
            }
        };
        contract1.SetContractCode("HD-RUBY-101");

        var contract2 = new ApartmentContract
        {
            ApartmentContractId = 2,
            ApartmentId = 2,
            Apartment = apt102,
            StartDate = new DateTime(2026, 2, 1),
            EndDate = new DateTime(2027, 1, 31),
            MonthlyRent = 12000000,
            DepositAmount = 24000000,
            Status = ApartmentContractStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            Parties = new List<ContractParty>
            {
                new() { ResidentId = 2, Resident = resident2, Role = ContractPartyRole.Lessee }
            }
        };
        contract2.SetContractCode("HD-RUBY-102");

        var invoice1 = new ApartmentInvoice
        {
            ApartmentInvoiceId = 1,
            InvoiceCode = "INV-2026-001",
            ApartmentId = 1,
            Apartment = apt101,
            ResidentId = 1,
            Resident = resident1,
            BillingYear = 2026,
            BillingMonth = 10,
            DueDate = DateTime.UtcNow.AddDays(15),
            ApartmentArea = 65,
            TotalAmount = 1500000,
            Status = ApartmentInvoiceStatus.Issued,
            CreatedAtUtc = DateTime.UtcNow
        };

        var invoice2 = new ApartmentInvoice
        {
            ApartmentInvoiceId = 2,
            InvoiceCode = "INV-2026-002",
            ApartmentId = 2,
            Apartment = apt102,
            ResidentId = 2,
            Resident = resident2,
            BillingYear = 2026,
            BillingMonth = 10,
            DueDate = DateTime.UtcNow.AddDays(15),
            ApartmentArea = 70,
            TotalAmount = 2000000,
            Status = ApartmentInvoiceStatus.Issued,
            CreatedAtUtc = DateTime.UtcNow
        };

        var invoice3 = new ApartmentInvoice
        {
            ApartmentInvoiceId = 3,
            InvoiceCode = "INV-2026-003",
            ApartmentId = 1,
            Apartment = apt101,
            ResidentId = 1,
            Resident = resident1,
            BillingYear = 2026,
            BillingMonth = 11,
            DueDate = DateTime.UtcNow.AddDays(45),
            ApartmentArea = 65,
            TotalAmount = 1500000,
            Status = ApartmentInvoiceStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow
        };

        context.AddRange(
            buildingRuby, buildingDiamond,
            apt101, apt102, apt201,
            userR1, userR2,
            resident1, resident2,
            contract1, contract2,
            invoice1, invoice2, invoice3);
        context.SaveChanges();

        return context;
    }

    private static ClaimsPrincipal CreateUserPrincipal(string role, string userId = "test-user")
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, userId),
            new Claim(ClaimTypes.Role, role)
        ], "TestAuth");

        return new ClaimsPrincipal(identity);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a")]
    [InlineData(" 1 ")]
    public async Task SearchAsync_WhenQueryNullOrShort_ReturnsEmpty(string? query)
    {
        using var context = CreateTestDbContext();
        var service = new SearchService(context);
        var user = CreateUserPrincipal(AppRoles.SuperAdmin);

        var result = await service.SearchAsync(query, user);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Groups);
    }

    [Fact]
    public async Task SearchAsync_WhenQueryTooLong_TruncatesTo100Chars()
    {
        using var context = CreateTestDbContext();
        var service = new SearchService(context);
        var user = CreateUserPrincipal(AppRoles.BuildingManager);

        var longQuery = new string('x', 150);
        var result = await service.SearchAsync(longQuery, user);

        Assert.Equal(100, result.Query.Length);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task SearchAsync_WhenUserNotAuthenticated_ReturnsEmpty()
    {
        using var context = CreateTestDbContext();
        var service = new SearchService(context);
        var unauthUser = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await service.SearchAsync("Ruby", unauthUser);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Groups);
    }

    [Fact]
    public async Task SearchAsync_WhenUserIsTechnician_ReturnsEmpty()
    {
        using var context = CreateTestDbContext();
        var service = new SearchService(context);
        var user = CreateUserPrincipal(AppRoles.Technician, "tech-user");

        var result = await service.SearchAsync("Ruby", user);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Groups);
    }

    [Fact]
    public async Task SearchAsync_SuperAdmin_FindsAcrossAllManagementCategories()
    {
        using var context = CreateTestDbContext();
        var service = new SearchService(context);
        var user = CreateUserPrincipal(AppRoles.SuperAdmin, "admin-user");

        var result = await service.SearchAsync("Ruby", user);

        Assert.True(result.TotalCount > 0);
        var categories = result.Groups.Select(g => g.Category).ToList();
        Assert.Contains("Tòa nhà", categories);
        Assert.Contains("Căn hộ", categories);
        Assert.Contains("Hợp đồng", categories);

        var buildingGroup = result.Groups.Single(g => g.Category == "Tòa nhà");
        Assert.Contains(buildingGroup.Items, i => i.Url == "/Buildings/Details/1");

        var aptGroup = result.Groups.Single(g => g.Category == "Căn hộ");
        Assert.Contains(aptGroup.Items, i => i.Url.StartsWith("/Apartments/Details/"));

        var contractGroup = result.Groups.Single(g => g.Category == "Hợp đồng");
        Assert.Contains(contractGroup.Items, i => i.Url.StartsWith("/Contracts/Details/"));
    }

    [Fact]
    public async Task SearchAsync_BuildingManager_CanFindResidentsAndInvoices()
    {
        using var context = CreateTestDbContext();
        var service = new SearchService(context);
        var user = CreateUserPrincipal(AppRoles.BuildingManager, "manager-user");

        var result = await service.SearchAsync("An", user);

        Assert.True(result.TotalCount > 0);
        var residentGroup = result.Groups.FirstOrDefault(g => g.Category == "Cư dân");
        Assert.NotNull(residentGroup);
        Assert.Contains(residentGroup.Items, r => r.Title == "Nguyễn Văn An" && r.Url == "/Residents/Details/1");
    }

    [Fact]
    public async Task SearchAsync_Accountant_OnlyFindsInvoices()
    {
        using var context = CreateTestDbContext();
        var service = new SearchService(context);
        var user = CreateUserPrincipal(AppRoles.Accountant, "accountant-user");

        // "101" matches building Ruby, apartment 101, contract 101, and invoice 101
        var result = await service.SearchAsync("101", user);

        Assert.Single(result.Groups);
        Assert.Equal("Hóa đơn", result.Groups[0].Category);
        Assert.All(result.Groups[0].Items, item => Assert.StartsWith("/Billing/Details/", item.Url));
    }

    [Fact]
    public async Task SearchAsync_Resident_CannotSeeOtherResidentsDataOrInvoices()
    {
        using var context = CreateTestDbContext();
        var service = new SearchService(context);
        // Resident 1 (user-r1)
        var user = CreateUserPrincipal(AppRoles.Resident, "user-r1");

        // Try searching for "Bình" (Resident 2)
        var resultOther = await service.SearchAsync("Bình", user);
        Assert.Equal(0, resultOther.TotalCount);

        // Try searching for "102" (Apartment of Resident 2)
        var resultApt2 = await service.SearchAsync("102", user);
        Assert.Equal(0, resultApt2.TotalCount);

        // Try searching for "INV-2026-002" (Invoice of Resident 2)
        var resultInv2 = await service.SearchAsync("INV-2026-002", user);
        Assert.Equal(0, resultInv2.TotalCount);
    }

    [Fact]
    public async Task SearchAsync_Resident_CanSeeOwnContractsAndIssuedInvoices()
    {
        using var context = CreateTestDbContext();
        var service = new SearchService(context);
        var user = CreateUserPrincipal(AppRoles.Resident, "user-r1");

        // Resident 1 searches for their apartment code "101"
        var result = await service.SearchAsync("101", user);

        Assert.True(result.TotalCount > 0);
        var categories = result.Groups.Select(g => g.Category).ToList();
        Assert.Contains("Hợp đồng của tôi", categories);
        Assert.Contains("Hóa đơn của tôi", categories);

        // Verify URLs point to resident portal endpoints
        var contractGroup = result.Groups.Single(g => g.Category == "Hợp đồng của tôi");
        Assert.Contains(contractGroup.Items, c => c.Url == "/MyContracts/Details/1");

        var invoiceGroup = result.Groups.Single(g => g.Category == "Hóa đơn của tôi");
        Assert.Contains(invoiceGroup.Items, i => i.Url == "/MyInvoices/Details/1");
        // Verify draft invoice is not visible to resident
        Assert.DoesNotContain(invoiceGroup.Items, i => i.Url == "/MyInvoices/Details/3");
    }

    [Fact]
    public async Task SearchAsync_EnforcesMax5ItemsPerCategory()
    {
        using var context = CreateTestDbContext();

        // Add 10 additional apartments
        for (int i = 10; i <= 20; i++)
        {
            context.Apartments.Add(new Apartment
            {
                ApartmentId = i + 100,
                ApartmentCode = $"DUP-{i}",
                BuildingId = 1,
                Floor = 1,
                Area = 50,
                Status = "Trống"
            });
        }
        context.SaveChanges();

        var service = new SearchService(context);
        var user = CreateUserPrincipal(AppRoles.BuildingManager);

        var result = await service.SearchAsync("DUP", user);

        var aptGroup = result.Groups.Single(g => g.Category == "Căn hộ");
        Assert.Equal(5, aptGroup.Items.Count);
    }

    [Fact]
    public async Task SearchAsync_GeneratedUrlsAreWhitelistedInternalPaths()
    {
        using var context = CreateTestDbContext();
        var service = new SearchService(context);
        var user = CreateUserPrincipal(AppRoles.BuildingManager);

        var result = await service.SearchAsync("101", user);

        Assert.True(result.TotalCount > 0);
        foreach (var group in result.Groups)
        {
            foreach (var item in group.Items)
            {
                Assert.StartsWith("/", item.Url);
                Assert.True(
                    item.Url.StartsWith("/Buildings/Details/") ||
                    item.Url.StartsWith("/Apartments/Details/") ||
                    item.Url.StartsWith("/Residents/Details/") ||
                    item.Url.StartsWith("/Contracts/Details/") ||
                    item.Url.StartsWith("/Billing/Details/") ||
                    item.Url.StartsWith("/MyContracts/Details/") ||
                    item.Url.StartsWith("/MyInvoices/Details/"));
            }
        }
    }
}
