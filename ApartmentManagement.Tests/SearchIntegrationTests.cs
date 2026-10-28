using System.Net;
using System.Net.Http.Json;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Models;
using ApartmentManagement.Tests.Helpers;
using Xunit;

namespace ApartmentManagement.Tests;

public sealed class SearchIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SearchIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Anonymous_SearchApi_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/search?q=101");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedManager_SearchApi_ReturnsSuccessWithResults()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", AppRoles.BuildingManager);
        client.DefaultRequestHeaders.Add("X-Test-UserId", "manager-user");

        using var response = await client.GetAsync("/api/search?q=BLOCK");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await response.Content.ReadFromJsonAsync<GlobalSearchResponseDto>();
        Assert.NotNull(data);
        Assert.Equal("BLOCK", data.Query);
    }

    [Fact]
    public async Task AuthenticatedResident_SearchApi_ReturnsSuccessWithoutError()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", AppRoles.Resident);
        client.DefaultRequestHeaders.Add("X-Test-UserId", "user-resident-1");

        using var response = await client.GetAsync("/api/search?q=PORTAL");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await response.Content.ReadFromJsonAsync<GlobalSearchResponseDto>();
        Assert.NotNull(data);
    }
}

