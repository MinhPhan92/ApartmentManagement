using System.Net;
using System.Net.Http.Json;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Tests.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace ApartmentManagement.Tests;

public sealed class SecurityIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string ResidentOneUserId = "user-resident-1";
    private const string ResidentTwoUserId = "user-resident-2";
    private readonly CustomWebApplicationFactory _factory;

    public SecurityIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        SeedTestData();
    }

    [Fact]
    public async Task Anonymous_MvcEndpoint_RedirectsToIdentityLogin()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync("/Apartments");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Identity/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Anonymous_ApiEndpoint_ReturnsUnauthorized()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync("/api/residents/1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PublicRegistration_ReturnsNotFound()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync("/Identity/Account/Register");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Resident_CreateApartmentPage_ReturnsForbidden()
    {
        using var client = CreateAuthenticatedClient(AppRoles.Resident, ResidentOneUserId);
        using var response = await client.GetAsync("/Apartments/Create");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Resident_ResidentList_ReturnsForbidden()
    {
        using var client = CreateAuthenticatedClient(AppRoles.Resident, ResidentOneUserId);
        using var response = await client.GetAsync("/api/residents");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Resident_AnotherProfile_ReturnsForbidden()
    {
        using var client = CreateAuthenticatedClient(AppRoles.Resident, ResidentOneUserId);
        using var response = await client.GetAsync("/api/residents/2");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Resident_OwnProfile_ReturnsSuccess()
    {
        using var client = CreateAuthenticatedClient(AppRoles.Resident, ResidentOneUserId);
        using var response = await client.GetAsync("/api/residents/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Nguyễn Văn A", body);
        Assert.DoesNotContain("Trần Thị B", body);
    }

    [Fact]
    public async Task Manager_PostWithoutAntiforgeryToken_ReturnsBadRequest()
    {
        using var client = CreateAuthenticatedClient(AppRoles.BuildingManager, "manager-user");
        using var content = CreateApartmentForm("A-999", token: null);
        using var response = await client.PostAsync("/Apartments/Create", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Manager_PostWithAntiforgeryToken_CreatesApartment()
    {
        using var client = CreateAuthenticatedClient(AppRoles.BuildingManager, "manager-user");
        var (_, token) = await AntiforgeryHelper.ExtractAsync(client, "/Apartments/Create");
        using var content = CreateApartmentForm("A-998", token);
        using var response = await client.PostAsync("/Apartments/Create", content);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(context.Apartments.Any(x => x.ApartmentCode == "A-998"));
    }

    [Fact]
    public async Task Resident_ResidentManagementPage_ReturnsForbidden()
    {
        using var client = CreateAuthenticatedClient(AppRoles.Resident, ResidentOneUserId);
        using var response = await client.GetAsync("/Residents");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Manager_ResidentManagementPage_ReturnsSuccess()
    {
        using var client = CreateAuthenticatedClient(AppRoles.BuildingManager, "manager-user");
        using var response = await client.GetAsync("/Residents");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Manager_CanProvisionResidentThroughMvc()
    {
        using var client = CreateAuthenticatedClient(AppRoles.BuildingManager, "manager-user");
        var (_, token) = await AntiforgeryHelper.ExtractAsync(client, "/Residents/Create");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FullName"] = "Lê Văn C",
            ["Email"] = "resident4@test.local",
            ["TemporaryPassword"] = "Resident@1234",
            ["CitizenId"] = "000000000004",
            ["Gender"] = "Nam",
            ["__RequestVerificationToken"] = token
        });
        using var response = await client.PostAsync("/Residents/Create", content);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(context.Residents.Any(x => x.CitizenId == "000000000004"));
        Assert.True(context.Users.Any(x => x.Email == "resident4@test.local"));
    }

    [Fact]
    public async Task Manager_CanEditResidentThroughMvc()
    {
        using var client = CreateAuthenticatedClient(AppRoles.BuildingManager, "manager-user");
        var (_, token) = await AntiforgeryHelper.ExtractAsync(client, "/Residents/Edit/1");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ResidentId"] = "1",
            ["FullName"] = "Nguyễn Văn A Updated",
            ["Email"] = "resident1@test.local",
            ["CitizenId"] = "000000000001",
            ["Gender"] = "Nam",
            ["__RequestVerificationToken"] = token
        });
        using var response = await client.PostAsync("/Residents/Edit/1", content);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal("Nguyễn Văn A Updated", context.Users.Single(x => x.Id == ResidentOneUserId).FullName);
    }

    [Fact]
    public async Task Manager_CannotDeleteResidentWithOccupancyHistory()
    {
        using var client = CreateAuthenticatedClient(AppRoles.BuildingManager, "manager-user");
        var (_, token) = await AntiforgeryHelper.ExtractAsync(client, "/Residents/Delete/1");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = "1",
            ["__RequestVerificationToken"] = token
        });
        using var response = await client.PostAsync("/Residents/Delete/1", content);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Residents/Delete/1", response.Headers.Location?.OriginalString);
        using var scope = _factory.Services.CreateScope();
        Assert.True(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Residents.Any(x => x.ResidentId == 1));
    }

    [Fact]
    public async Task Manager_CanDeleteResidentWithoutOccupancyHistory()
    {
        using var client = CreateAuthenticatedClient(AppRoles.BuildingManager, "manager-user");
        var (_, token) = await AntiforgeryHelper.ExtractAsync(client, "/Residents/Delete/3");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = "3",
            ["__RequestVerificationToken"] = token
        });
        using var response = await client.PostAsync("/Residents/Delete/3", content);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(context.Residents.Any(x => x.ResidentId == 3));
        Assert.False(context.Users.Any(x => x.Id == "user-resident-3"));
    }

    [Fact]
    public async Task BuildingManager_AccountManagement_ReturnsForbidden()
    {
        using var client = CreateAuthenticatedClient(AppRoles.BuildingManager, "manager-user");
        using var response = await client.GetAsync("/Accounts");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_AccountManagement_ReturnsSuccess()
    {
        using var client = CreateAuthenticatedClient(AppRoles.SuperAdmin, "admin-test");
        using var response = await client.GetAsync("/Accounts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task BuildingManager_CannotEscalateRole()
    {
        using var client = CreateAuthenticatedClient(AppRoles.BuildingManager, "manager-user");
        var (_, token) = await AntiforgeryHelper.ExtractAsync(client, "/Identity/Account/Login");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserId"] = ResidentOneUserId,
            ["Role"] = AppRoles.SuperAdmin,
            ["__RequestVerificationToken"] = token
        });
        using var response = await client.PostAsync("/Accounts/ChangeRole", content);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_CannotDeactivateSelfOrLastSuperAdmin()
    {
        using var client = CreateAuthenticatedClient(AppRoles.SuperAdmin, "admin-test");
        var (_, token) = await AntiforgeryHelper.ExtractAsync(client, "/Accounts/Details/admin-test");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = "admin-test",
            ["isActive"] = "false",
            ["__RequestVerificationToken"] = token
        });
        using var response = await client.PostAsync("/Accounts/SetActive", content);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(context.Users.Single(x => x.Id == "admin-test").IsActive);
    }

    [Fact]
    public async Task SuperAdmin_ResetPassword_ChangesLoginCredential()
    {
        using var adminClient = CreateAuthenticatedClient(AppRoles.SuperAdmin, "admin-test");
        var (_, token) = await AntiforgeryHelper.ExtractAsync(
            adminClient, $"/Accounts/ResetPassword/{ResidentOneUserId}");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserId"] = ResidentOneUserId,
            ["TemporaryPassword"] = "Changed@1234",
            ["ConfirmPassword"] = "Changed@1234",
            ["__RequestVerificationToken"] = token
        });
        using var resetResponse = await adminClient.PostAsync("/Accounts/ResetPassword", content);
        Assert.Equal(HttpStatusCode.Redirect, resetResponse.StatusCode);

        using var loginClient = CreateClient();
        using var loginResponse = await LoginAsync(loginClient, "resident1@test.local", "Changed@1234");
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
    }

    [Fact]
    public async Task InactiveAccount_CannotLogin()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<ApartmentManagement.Services.IAccountManagementService>();
            var result = await service.SetActiveAsync(ResidentOneUserId, false, "admin-test");
            Assert.True(result.Success);
        }

        using var client = CreateClient();
        using var response = await LoginAsync(client, "resident1@test.local", "Resident@1234");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("đang bị khóa hoặc vô hiệu hóa", html);
        using var protectedResponse = await client.GetAsync("/Dashboard/Resident");
        Assert.Equal(HttpStatusCode.Redirect, protectedResponse.StatusCode);
    }

    [Fact]
    public async Task DeactivatedAccount_ExistingSessionIsRejected()
    {
        using var client = CreateClient();
        using (var loginResponse = await LoginAsync(client, "resident1@test.local", "Resident@1234"))
            Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        using (var before = await client.GetAsync("/Dashboard/Resident"))
            Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<ApartmentManagement.Services.IAccountManagementService>();
            Assert.True((await service.SetActiveAsync(ResidentOneUserId, false, "admin-test")).Success);
        }

        using var after = await client.GetAsync("/Dashboard/Resident");
        Assert.Equal(HttpStatusCode.Redirect, after.StatusCode);
        Assert.Equal("/Identity/Account/Login", after.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task RoleChange_ExistingSessionIsInvalidatedImmediately()
    {
        using var client = CreateClient();
        using (var loginResponse = await LoginAsync(client, "resident1@test.local", "Resident@1234"))
            Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<ApartmentManagement.Services.IAccountManagementService>();
            var result = await service.ChangeRoleAsync(
                ResidentOneUserId, AppRoles.Technician, "admin-test");
            Assert.True(result.Success);
        }

        using var response = await client.GetAsync("/Dashboard/Resident");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Identity/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task AccountMutation_WithoutAntiforgeryToken_ReturnsBadRequest()
    {
        using var client = CreateAuthenticatedClient(AppRoles.SuperAdmin, "admin-test");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = ResidentOneUserId,
            ["isActive"] = "false"
        });
        using var response = await client.PostAsync("/Accounts/SetActive", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("1234567890")]
    [InlineData("12345678A")]
    public async Task ResidentCreate_InvalidCitizenId_IsRejected(string citizenId)
    {
        using var client = CreateAuthenticatedClient(AppRoles.BuildingManager, "manager-user");
        var (_, token) = await AntiforgeryHelper.ExtractAsync(client, "/Residents/Create");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FullName"] = "Cư dân không hợp lệ",
            ["Email"] = $"invalid-{Guid.NewGuid()}@test.local",
            ["TemporaryPassword"] = "Resident@1234",
            ["CitizenId"] = citizenId,
            ["__RequestVerificationToken"] = token
        });
        using var response = await client.PostAsync("/Residents/Create", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        Assert.False(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Residents.Any(x => x.CitizenId == citizenId));
    }

    [Fact]
    public async Task ResidentCreate_FutureDateOfBirth_IsRejected()
    {
        using var client = CreateAuthenticatedClient(AppRoles.BuildingManager, "manager-user");
        var (_, token) = await AntiforgeryHelper.ExtractAsync(client, "/Residents/Create");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FullName"] = "Cư dân tương lai",
            ["Email"] = "future@test.local",
            ["TemporaryPassword"] = "Resident@1234",
            ["CitizenId"] = "000000000099",
            ["DateOfBirth"] = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd"),
            ["__RequestVerificationToken"] = token
        });
        using var response = await client.PostAsync("/Residents/Create", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Ngày sinh phải từ", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task ResidentCreate_DuplicateCitizenId_IsRejectedWithoutPartialUser()
    {
        using var client = CreateAuthenticatedClient(AppRoles.BuildingManager, "manager-user");
        var (_, token) = await AntiforgeryHelper.ExtractAsync(client, "/Residents/Create");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FullName"] = "Duplicate Resident",
            ["Email"] = "duplicate@test.local",
            ["TemporaryPassword"] = "Resident@1234",
            ["CitizenId"] = "000000000001",
            ["__RequestVerificationToken"] = token
        });
        using var response = await client.PostAsync("/Residents/Create", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(context.Users.Any(x => x.Email == "duplicate@test.local"));
    }

    [Fact]
    public async Task ResidentsApi_InvalidPayload_ReturnsValidationProblem()
    {
        using var client = CreateAuthenticatedClient(AppRoles.BuildingManager, "manager-user");
        var (_, token) = await AntiforgeryHelper.ExtractAsync(client, "/Residents/Create");
        client.DefaultRequestHeaders.Add("RequestVerificationToken", token);
        using var response = await client.PostAsJsonAsync("/api/residents", new
        {
            Email = "not-an-email",
            FullName = new string('X', 201),
            TemporaryPassword = "Resident@1234",
            CitizenId = "ABC",
            DateOfBirth = DateTime.UtcNow.AddDays(1),
            EmergencyContact = "phone"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public void ResidentModel_HasExpectedDatabaseIntegrityMetadata()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var model = context.GetService<IDesignTimeModel>().Model;
        var resident = model.FindEntityType(typeof(Resident))!;
        Assert.True(resident.GetIndexes().Single(x =>
            x.Properties.Single().Name == nameof(Resident.CitizenId)).IsUnique);
        Assert.Equal(255, resident.FindProperty(nameof(Resident.Address))!.GetMaxLength());
        Assert.Contains(resident.GetCheckConstraints(), x => x.Name == "CK_Residents_CitizenId_Format");
        Assert.Contains(resident.GetCheckConstraints(), x => x.Name == "CK_Residents_DateOfBirth_Minimum");
    }

    [Fact]
    public async Task Resident_OccupancyManagementPage_ReturnsForbidden()
    {
        using var client = CreateAuthenticatedClient(AppRoles.Resident, ResidentOneUserId);
        using var response = await client.GetAsync("/Occupancies");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Resident_OccupancyApi_ReturnsOnlyOwnHistory()
    {
        using var client = CreateAuthenticatedClient(AppRoles.Resident, ResidentOneUserId);
        using var response = await client.GetAsync("/api/apartmentresidents");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Nguyễn Văn A", body);
        Assert.DoesNotContain("Trần Thị B", body);
    }

    [Fact]
    public async Task Manager_CanMoveInResident()
    {
        using var client = CreateAuthenticatedClient(AppRoles.BuildingManager, "manager-user");
        var (_, token) = await AntiforgeryHelper.ExtractAsync(client, "/Occupancies/Create");
        using var content = CreateMoveInForm(1, 3, DateTime.Today, token);
        using var response = await client.PostAsync("/Occupancies/Create", content);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(context.ApartmentResidents.Any(x =>
            x.ApartmentId == 1 && x.ResidentId == 3 && x.MoveOutDate == null));
        Assert.Equal("Đang sử dụng", context.Apartments.Single(x => x.ApartmentId == 1).Status);
    }

    [Fact]
    public async Task Manager_CannotCreateDuplicateActiveOccupancy()
    {
        using var client = CreateAuthenticatedClient(AppRoles.BuildingManager, "manager-user");
        var (_, token) = await AntiforgeryHelper.ExtractAsync(client, "/Occupancies/Create");
        using var content = CreateMoveInForm(1, 1, DateTime.Today, token);
        using var response = await client.PostAsync("/Occupancies/Create", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("đã có hồ sơ cư trú đang hoạt động", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));

        using var scope = _factory.Services.CreateScope();
        Assert.Single(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .ApartmentResidents.Where(x => x.ApartmentId == 1 && x.ResidentId == 1));
    }

    [Fact]
    public async Task Manager_CanMoveOutAndResidentCanMoveInAgain()
    {
        int occupancyId;
        using (var scope = _factory.Services.CreateScope())
            occupancyId = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .ApartmentResidents.Single(x => x.ApartmentId == 1 && x.ResidentId == 1).ApartmentResidentId;

        using var client = CreateAuthenticatedClient(AppRoles.BuildingManager, "manager-user");
        var (_, token) = await AntiforgeryHelper.ExtractAsync(client, $"/Occupancies/MoveOut/{occupancyId}");
        using (var moveOut = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ApartmentResidentId"] = occupancyId.ToString(),
            ["MoveOutDate"] = DateTime.Today.ToString("yyyy-MM-dd"),
            ["__RequestVerificationToken"] = token
        }))
        using (var response = await client.PostAsync("/Occupancies/MoveOut", moveOut))
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var moveIn = CreateMoveInForm(1, 1, DateTime.Today, token);
        using var moveInResponse = await client.PostAsync("/Occupancies/Create", moveIn);
        Assert.Equal(HttpStatusCode.Redirect, moveInResponse.StatusCode);

        using var verifyScope = _factory.Services.CreateScope();
        var records = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .ApartmentResidents.Where(x => x.ApartmentId == 1 && x.ResidentId == 1).ToList();
        Assert.Equal(2, records.Count);
        Assert.Single(records, x => x.MoveOutDate == null);
    }

    [Fact]
    public async Task MoveOut_LastActiveResident_SetsApartmentVacant()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ApartmentManagement.Services.IOccupancyService>();
        var ids = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .ApartmentResidents.Where(x => x.ApartmentId == 1).Select(x => x.ApartmentResidentId).ToList();
        foreach (var id in ids) Assert.True((await service.MoveOutAsync(id, DateTime.Today)).Success);

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal("Trống", context.Apartments.Single(x => x.ApartmentId == 1).Status);
        Assert.Equal(2, context.ApartmentResidents.Count(x => x.ApartmentId == 1 && x.MoveOutDate != null));
    }

    [Fact]
    public async Task MoveOut_BeforeMoveIn_IsRejectedWithoutChangingHistory()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var occupancy = context.ApartmentResidents.First();
        var service = scope.ServiceProvider.GetRequiredService<ApartmentManagement.Services.IOccupancyService>();
        var result = await service.MoveOutAsync(occupancy.ApartmentResidentId, occupancy.MoveInDate.AddDays(-1));
        Assert.False(result.Success);
        Assert.Null(context.ApartmentResidents.Single(x =>
            x.ApartmentResidentId == occupancy.ApartmentResidentId).MoveOutDate);
    }

    [Fact]
    public async Task MoveIn_WithoutAntiforgeryToken_ReturnsBadRequest()
    {
        using var client = CreateAuthenticatedClient(AppRoles.BuildingManager, "manager-user");
        using var content = CreateMoveInForm(1, 3, DateTime.Today, token: null);
        using var response = await client.PostAsync("/Occupancies/Create", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_ContractManagement_RedirectsToLogin()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync("/Contracts");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Identity/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    [Theory]
    [InlineData(AppRoles.Resident, ResidentOneUserId)]
    [InlineData(AppRoles.BuildingManager, "manager-user")]
    public async Task NonSuperAdmin_ContractManagement_ReturnsForbidden(string role, string userId)
    {
        using var client = CreateAuthenticatedClient(role, userId);
        using var response = await client.GetAsync("/Contracts");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_CanViewContractManagement()
    {
        using var client = CreateAuthenticatedClient(AppRoles.SuperAdmin, "admin-test");
        using var response = await client.GetAsync("/Contracts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Quản lý hợp đồng", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ContractCreate_RequiresAntiforgeryToken()
    {
        using var client = CreateAuthenticatedClient(AppRoles.SuperAdmin, "admin-test");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ContractCode"] = "CSRF-LEASE",
            ["ApartmentId"] = "1",
            ["LessorResidentId"] = "1",
            ["LesseeResidentId"] = "2",
            ["StartDate"] = "2027-01-01",
            ["MonthlyRent"] = "1000",
            ["DepositAmount"] = "500"
        });
        using var response = await client.PostAsync("/Contracts/Create", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ContractCreate_InvalidFormShowsValidationErrors()
    {
        using var client = CreateAuthenticatedClient(AppRoles.SuperAdmin, "admin-test");
        var (_, token) = await AntiforgeryHelper.ExtractAsync(client, "/Contracts/Create");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ContractCode"] = "",
            ["ApartmentId"] = "0",
            ["LessorResidentId"] = "0",
            ["LesseeResidentId"] = "0",
            ["StartDate"] = "",
            ["MonthlyRent"] = "0",
            ["DepositAmount"] = "-1",
            ["__RequestVerificationToken"] = token
        });
        using var response = await client.PostAsync("/Contracts/Create", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Vui lòng nhập mã hợp đồng.", body);
        Assert.Contains("Vui lòng chọn căn hộ.", body);
    }

    [Fact]
    public async Task ContractCreate_IgnoresClientSuppliedStatusAndCreator()
    {
        using var client = CreateAuthenticatedClient(AppRoles.SuperAdmin, "admin-test");
        var (_, token) = await AntiforgeryHelper.ExtractAsync(client, "/Contracts/Create");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ContractCode"] = $"MVC-{Guid.NewGuid():N}",
            ["ApartmentId"] = "1",
            ["LessorResidentId"] = "1",
            ["LesseeResidentId"] = "2",
            ["StartDate"] = "2027-01-01",
            ["EndDate"] = "2027-12-31",
            ["MonthlyRent"] = "1000",
            ["DepositAmount"] = "500",
            ["Status"] = "Active",
            ["CreatedByUserId"] = ResidentOneUserId,
            ["__RequestVerificationToken"] = token
        });
        using var response = await client.PostAsync("/Contracts/Create", content);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var contract = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .ApartmentContracts.SingleAsync(x => x.ContractCode.StartsWith("MVC-"));
        Assert.Equal(ApartmentContractStatus.Draft, contract.Status);
        Assert.Equal("admin-test", contract.CreatedByUserId);
    }

    [Fact]
    public async Task SuperAdmin_CanCancelDraftAndReasonIsRecorded()
    {
        using var client = CreateAuthenticatedClient(AppRoles.SuperAdmin, "admin-test");
        var (_, createToken) = await AntiforgeryHelper.ExtractAsync(client, "/Contracts/Create");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ContractCode"] = $"CANCEL-{Guid.NewGuid():N}",
            ["ApartmentId"] = "1",
            ["LessorResidentId"] = "1",
            ["LesseeResidentId"] = "2",
            ["StartDate"] = "2027-01-01",
            ["EndDate"] = "2027-12-31",
            ["MonthlyRent"] = "1000",
            ["DepositAmount"] = "500",
            ["__RequestVerificationToken"] = createToken
        });
        using var createResponse = await client.PostAsync("/Contracts/Create", content);
        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);
        var detailsPath = createResponse.Headers.Location!.ToString();

        int contractId;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var contract = await context.ApartmentContracts
                .SingleAsync(x => x.ContractCode.StartsWith("CANCEL-"));
            contract.RowVersion = [7];
            await context.SaveChangesAsync();
            contractId = contract.ApartmentContractId;
        }

        using var detailsResponse = await client.GetAsync(detailsPath);
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        var detailsHtml = await detailsResponse.Content.ReadAsStringAsync();
        var tokenMatch = System.Text.RegularExpressions.Regex.Match(
            detailsHtml,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(tokenMatch.Success);
        var cancelToken = tokenMatch.Groups[1].Value;
        using var cancelContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["RowVersionToken"] = Convert.ToBase64String([7]),
            ["Reason"] = "Yêu cầu từ khách thuê",
            ["__RequestVerificationToken"] = cancelToken
        });
        using var cancelResponse = await client.PostAsync($"/Contracts/Cancel/{contractId}", cancelContent);

        Assert.Equal(HttpStatusCode.Redirect, cancelResponse.StatusCode);
        using var verifyScope = _factory.Services.CreateScope();
        var cancelled = await verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .ApartmentContracts.Include(x => x.StatusHistory)
            .SingleAsync(x => x.ApartmentContractId == contractId);
        Assert.Equal(ApartmentContractStatus.Cancelled, cancelled.Status);
        Assert.Equal("Yêu cầu từ khách thuê", cancelled.StatusHistory.Last().Reason);
    }

    [Fact]
    public async Task Anonymous_MyContracts_RedirectsToLogin()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync("/MyContracts");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Identity/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Anonymous_MyContractDetails_RedirectsToLogin()
    {
        var contractId = GetContractId("PORTAL-R1");
        using var client = CreateClient();
        using var response = await client.GetAsync($"/MyContracts/Details/{contractId}");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task Resident_MyContracts_ShowsOnlyOwnParticipatingContracts()
    {
        using var client = CreateAuthenticatedClient(AppRoles.Resident, ResidentOneUserId);

        using var response = await client.GetAsync("/MyContracts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("PORTAL-R1", body);
        Assert.DoesNotContain("PORTAL-R2", body);
    }

    [Fact]
    public async Task Resident_CanViewOwnContractDetails()
    {
        var contractId = GetContractId("PORTAL-R1");
        using var client = CreateAuthenticatedClient(AppRoles.Resident, ResidentOneUserId);

        using var response = await client.GetAsync($"/MyContracts/Details/{contractId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("PORTAL-R1", body);
        Assert.Contains("Tiền thuê hàng tháng", body);
        Assert.Contains("Nguyễn Văn A", body);
        Assert.DoesNotContain("PORTAL-R2", body);
    }

    [Fact]
    public async Task Resident_CannotViewAnotherResidentsContractById()
    {
        var otherResidentContractId = GetContractId("PORTAL-R2");
        using var client = CreateAuthenticatedClient(AppRoles.Resident, ResidentOneUserId);

        using var response = await client.GetAsync($"/MyContracts/Details/{otherResidentContractId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Resident_CannotSelectContractOwnerThroughQueryString()
    {
        using var client = CreateAuthenticatedClient(AppRoles.Resident, ResidentOneUserId);

        using var response = await client.GetAsync($"/MyContracts?userId={Uri.EscapeDataString(ResidentTwoUserId)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("PORTAL-R1", body);
        Assert.DoesNotContain("PORTAL-R2", body);
    }

    [Fact]
    public async Task BuildingManager_CannotAccessResidentContractPortal()
    {
        using var client = CreateAuthenticatedClient(AppRoles.BuildingManager, "manager-user");

        using var response = await client.GetAsync("/MyContracts");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ResidentContractPortal_HasNoWriteEndpoint()
    {
        using var client = CreateAuthenticatedClient(AppRoles.Resident, ResidentOneUserId);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Status"] = "Active",
            ["UserId"] = ResidentTwoUserId
        });

        using var response = await client.PostAsync("/MyContracts/Details/1", content);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().ApartmentContracts
            .Where(x => x.Status != ApartmentContractStatus.Draft)
            .ToListAsync());
    }

    private int GetContractId(string contractCode)
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .ApartmentContracts.Single(x => x.ContractCode == contractCode).ApartmentContractId;
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost")
    });

    private HttpClient CreateAuthenticatedClient(string role, string userId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
        return client;
    }

    private static FormUrlEncodedContent CreateApartmentForm(string code, string? token)
    {
        var values = new Dictionary<string, string>
        {
            ["BuildingId"] = "1",
            ["ApartmentCode"] = code,
            ["Floor"] = "1",
            ["Area"] = "65",
            ["Status"] = "Đang sử dụng"
        };

        if (token != null)
        {
            values["__RequestVerificationToken"] = token;
        }

        return new FormUrlEncodedContent(values);
    }

    private static FormUrlEncodedContent CreateMoveInForm(
        int apartmentId, int residentId, DateTime moveInDate, string? token)
    {
        var values = new Dictionary<string, string>
        {
            ["ApartmentId"] = apartmentId.ToString(),
            ["ResidentId"] = residentId.ToString(),
            ["Relationship"] = "Thành viên",
            ["MoveInDate"] = moveInDate.ToString("yyyy-MM-dd"),
            ["IsOwner"] = "false"
        };
        if (token != null) values["__RequestVerificationToken"] = token;
        return new FormUrlEncodedContent(values);
    }

    private static async Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string username,
        string password)
    {
        var (_, token) = await AntiforgeryHelper.ExtractAsync(client, "/Identity/Account/Login");
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.UserNameOrEmail"] = username,
            ["Input.Password"] = password,
            ["Input.RememberMe"] = "false",
            ["__RequestVerificationToken"] = token
        });
        return await client.PostAsync("/Identity/Account/Login", content);
    }

    private void SeedTestData()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();

        foreach (var role in AppRoles.AllRoles)
        {
            context.Roles.Add(new IdentityRole(role)
            {
                Id = $"role-{role.ToLowerInvariant()}",
                NormalizedName = role.ToUpperInvariant()
            });
        }

        var building = new Building
        {
            BuildingId = 1,
            BuildingCode = "BLOCK-A",
            BuildingName = "Block A",
            Address = "Test address",
            NumberOfFloors = 10
        };
        var apartment = new Apartment
        {
            ApartmentId = 1,
            BuildingId = building.BuildingId,
            Building = building,
            ApartmentCode = "A-101",
            Floor = 1,
            Area = 65,
            Status = "Đang sử dụng"
        };
        var firstUser = CreateUser(ResidentOneUserId, "resident1@test.local", "Nguyễn Văn A");
        var secondUser = CreateUser(ResidentTwoUserId, "resident2@test.local", "Trần Thị B");
        var thirdUser = CreateUser("user-resident-3", "resident3@test.local", "Lê Văn C");
        var firstResident = new Resident
        {
            ResidentId = 1,
            UserId = firstUser.Id,
            User = firstUser,
            CitizenId = "000000000001"
        };
        var secondResident = new Resident
        {
            ResidentId = 2,
            UserId = secondUser.Id,
            User = secondUser,
            CitizenId = "000000000002"
        };
        var thirdResident = new Resident
        {
            ResidentId = 3,
            UserId = thirdUser.Id,
            User = thirdUser,
            CitizenId = "000000000003"
        };

        context.AddRange(
            building, apartment,
            firstUser, secondUser, thirdUser,
            firstResident, secondResident, thirdResident);
        context.ApartmentResidents.AddRange(
            new ApartmentResident
            {
                ApartmentId = apartment.ApartmentId,
                ResidentId = firstResident.ResidentId,
                Apartment = apartment,
                Resident = firstResident,
                Relationship = "Chủ hộ",
                MoveInDate = new DateTime(2026, 1, 1),
                IsOwner = true
            },
            new ApartmentResident
            {
                ApartmentId = apartment.ApartmentId,
                ResidentId = secondResident.ResidentId,
                Apartment = apartment,
                Resident = secondResident,
                Relationship = "Thành viên",
                MoveInDate = new DateTime(2026, 1, 1),
                IsOwner = false
            });
        context.SaveChanges();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True(userManager.AddPasswordAsync(firstUser, "Resident@1234").GetAwaiter().GetResult().Succeeded);
        Assert.True(userManager.AddToRoleAsync(firstUser, AppRoles.Resident).GetAwaiter().GetResult().Succeeded);
        var admin = CreateUser("admin-test", "admin@test.local", "Test SuperAdmin");
        Assert.True(userManager.CreateAsync(admin, "Admin@Test1234").GetAwaiter().GetResult().Succeeded);
        Assert.True(userManager.AddToRoleAsync(admin, AppRoles.SuperAdmin).GetAwaiter().GetResult().Succeeded);

        var contractService = scope.ServiceProvider.GetRequiredService<ApartmentManagement.Services.IContractService>();
        var firstContract = contractService.CreateDraftAsync(new ApartmentManagement.Services.ContractDraftInput
        {
            ContractCode = "PORTAL-R1",
            ApartmentId = apartment.ApartmentId,
            StartDate = new DateTime(2027, 1, 1),
            EndDate = new DateTime(2027, 12, 31),
            MonthlyRent = 1200,
            DepositAmount = 800,
            Parties =
            [
                new ApartmentManagement.Services.ContractPartyInput(firstResident.ResidentId, ContractPartyRole.Lessor),
                new ApartmentManagement.Services.ContractPartyInput(thirdResident.ResidentId, ContractPartyRole.Lessee)
            ]
        }, admin.Id).GetAwaiter().GetResult();
        Assert.True(firstContract.Success, firstContract.ErrorMessage);
        var secondContract = contractService.CreateDraftAsync(new ApartmentManagement.Services.ContractDraftInput
        {
            ContractCode = "PORTAL-R2",
            ApartmentId = apartment.ApartmentId,
            StartDate = new DateTime(2027, 1, 1),
            EndDate = new DateTime(2027, 12, 31),
            MonthlyRent = 1400,
            DepositAmount = 900,
            Parties =
            [
                new ApartmentManagement.Services.ContractPartyInput(secondResident.ResidentId, ContractPartyRole.Lessor),
                new ApartmentManagement.Services.ContractPartyInput(thirdResident.ResidentId, ContractPartyRole.Lessee)
            ]
        }, admin.Id).GetAwaiter().GetResult();
        Assert.True(secondContract.Success, secondContract.ErrorMessage);
    }

    private static ApplicationUser CreateUser(string id, string email, string fullName) => new()
    {
        Id = id,
        UserName = email,
        NormalizedUserName = email.ToUpperInvariant(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        FullName = fullName,
        EmailConfirmed = true,
        IsActive = true
    };
}
