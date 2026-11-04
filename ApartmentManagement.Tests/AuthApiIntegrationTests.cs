using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Controllers;
using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApartmentManagement.Tests;

public sealed class AuthApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateHttpsClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true,
        BaseAddress = new Uri("https://localhost")
    });

    [Fact]
    public async Task GetCsrfToken_Anonymous_ReturnsTokenAndSetsCookie()
    {
        using var client = CreateHttpsClient();

        using var response = await client.GetAsync("/api/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CsrfTokenResponse>();
        Assert.NotNull(body);
        Assert.Equal("RequestVerificationToken", body!.HeaderName);
        Assert.False(string.IsNullOrWhiteSpace(body.RequestToken));
        Assert.Contains(response.Headers, h => h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetCurrentUser_Unauthenticated_Returns401InsteadOfRedirect()
    {
        using var client = CreateHttpsClient();

        using var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithoutAntiforgeryToken_Returns400BadRequest()
    {
        using var client = CreateHttpsClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("admin@apartment.local", "Admin@123", false));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_Me_And_Logout_WithCsrfToken_SucceedsAndMaintainsCookieSession()
    {
        await EnsureUserAsync("auth.resident@apartment.local", "Resident@123!", AppRoles.Resident, isActive: true);

        using var client = CreateHttpsClient();

        // 1. Fetch CSRF token
        var csrf = await FetchCsrfAsync(client);

        // 2. Login with valid credentials + CSRF header
        using var loginReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest("auth.resident@apartment.local", "Resident@123!", false))
        };
        loginReq.Headers.Add(csrf.HeaderName, csrf.RequestToken);

        using var loginRes = await client.SendAsync(loginReq);
        var rawJson = await loginRes.Content.ReadAsStringAsync();
        Assert.True(loginRes.StatusCode == HttpStatusCode.OK, $"Expected OK but got {loginRes.StatusCode}: {rawJson}");

        Assert.DoesNotContain("PasswordHash", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SecurityStamp", rawJson, StringComparison.OrdinalIgnoreCase);

        var loggedInUser = JsonSerializer.Deserialize<CurrentUserResponse>(
            rawJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(loggedInUser);
        Assert.Equal("resident", loggedInUser!.Portal);
        Assert.Equal("/resident/dashboard", loggedInUser.DefaultRoute);
        Assert.Contains(AppRoles.Resident, loggedInUser.Roles);
        Assert.True(loggedInUser.Permissions.RequireResident);
        Assert.False(loggedInUser.Permissions.RequireManagement);

        // 3. Verify GET /api/auth/me preserves session across requests (browser refresh simulation)
        using var meRes = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meRes.StatusCode);

        // 4. Refresh CSRF token for authenticated principal and Logout
        var authCsrf = await FetchCsrfAsync(client);
        using var logoutReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logoutReq.Headers.Add(authCsrf.HeaderName, authCsrf.RequestToken);

        using var logoutRes = await client.SendAsync(logoutReq);
        Assert.Equal(HttpStatusCode.OK, logoutRes.StatusCode);

        // 5. Verify session is cleared after logout
        using var afterLogoutMeRes = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogoutMeRes.StatusCode);
    }

    [Fact]
    public async Task Login_InvalidPassword_Returns401WithSafeMessage()
    {
        await EnsureUserAsync("auth.valid@apartment.local", "ValidPass@123!", AppRoles.Resident, isActive: true);

        using var client = CreateHttpsClient();

        var csrf = await FetchCsrfAsync(client);
        using var loginReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest("auth.valid@apartment.local", "WrongPassword@999", false))
        };
        loginReq.Headers.Add(csrf.HeaderName, csrf.RequestToken);

        using var loginRes = await client.SendAsync(loginReq);
        var body = await loginRes.Content.ReadAsStringAsync();
        Assert.True(loginRes.StatusCode == HttpStatusCode.Unauthorized, $"Expected Unauthorized but got {loginRes.StatusCode}: {body}");
    }

    [Fact]
    public async Task Login_InactiveUser_Returns403Forbidden()
    {
        await EnsureUserAsync("auth.locked@apartment.local", "LockedPass@123!", AppRoles.Resident, isActive: false);

        using var client = CreateHttpsClient();

        var csrf = await FetchCsrfAsync(client);
        using var loginReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest("auth.locked@apartment.local", "LockedPass@123!", false))
        };
        loginReq.Headers.Add(csrf.HeaderName, csrf.RequestToken);

        using var loginRes = await client.SendAsync(loginReq);
        var body = await loginRes.Content.ReadAsStringAsync();
        Assert.True(loginRes.StatusCode == HttpStatusCode.Forbidden, $"Expected Forbidden but got {loginRes.StatusCode}: {body}");
    }

    [Theory]
    [InlineData(AppRoles.SuperAdmin, "management", true, true, true, false)]
    [InlineData(AppRoles.BuildingManager, "management", false, true, true, false)]
    [InlineData(AppRoles.Accountant, "management", false, false, true, false)]
    [InlineData(AppRoles.Technician, "management", false, false, false, true)]
    public async Task GetCurrentUser_RolePermissions_MapAccuratelyToPolicies(
        string role,
        string expectedPortal,
        bool expectedSuperAdmin,
        bool expectedManagement,
        bool expectedAccountantOrManager,
        bool expectedTechnician)
    {
        var email = $"role.{role.ToLowerInvariant()}@apartment.local";
        await EnsureUserAsync(email, "RolePass@123!", role, isActive: true);

        using var client = CreateHttpsClient();

        var csrf = await FetchCsrfAsync(client);
        using var loginReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest(email, "RolePass@123!", false))
        };
        loginReq.Headers.Add(csrf.HeaderName, csrf.RequestToken);

        using var loginRes = await client.SendAsync(loginReq);
        var body = await loginRes.Content.ReadAsStringAsync();
        Assert.True(loginRes.StatusCode == HttpStatusCode.OK, $"Expected OK but got {loginRes.StatusCode}: {body}");

        var current = JsonSerializer.Deserialize<CurrentUserResponse>(
            body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(current);
        Assert.Equal(expectedPortal, current!.Portal);
        Assert.Equal(expectedSuperAdmin, current.Permissions.RequireSuperAdmin);
        Assert.Equal(expectedManagement, current.Permissions.RequireManagement);
        Assert.Equal(expectedAccountantOrManager, current.Permissions.RequireAccountantOrManager);
        Assert.Equal(expectedTechnician, current.Permissions.RequireTechnician);
    }

    [Fact]
    public async Task ExistingMvcRoutesAndSpaRoutes_WorkSideBySide()
    {
        using var client = CreateHttpsClient();

        using var mvcLoginRes = await client.GetAsync("/Identity/Account/Login");
        Assert.Equal(HttpStatusCode.OK, mvcLoginRes.StatusCode);

        using var homeRes = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, homeRes.StatusCode);

        using var spaLoginRes = await client.GetAsync("/login");
        Assert.Equal(HttpStatusCode.OK, spaLoginRes.StatusCode);

        using var spaResidentRes = await client.GetAsync("/resident/dashboard");
        Assert.Equal(HttpStatusCode.OK, spaResidentRes.StatusCode);

        using var spaManagementRes = await client.GetAsync("/management/dashboard");
        Assert.Equal(HttpStatusCode.OK, spaManagementRes.StatusCode);
    }

    private static async Task<CsrfTokenResponse> FetchCsrfAsync(HttpClient client)
    {
        using var res = await client.GetAsync("/api/auth/csrf");
        res.EnsureSuccessStatusCode();
        var csrf = await res.Content.ReadFromJsonAsync<CsrfTokenResponse>();
        return csrf!;
    }

    private async Task EnsureUserAsync(string email, string password, string role, bool isActive)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }

        var existing = await userManager.FindByEmailAsync(email);
        if (existing != null)
        {
            existing.IsActive = isActive;
            await userManager.UpdateAsync(existing);
            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = $"Test {role}",
            IsActive = isActive
        };

        var createResult = await userManager.CreateAsync(user, password);
        Assert.True(createResult.Succeeded);

        var roleResult = await userManager.AddToRoleAsync(user, role);
        Assert.True(roleResult.Succeeded);
    }
}
