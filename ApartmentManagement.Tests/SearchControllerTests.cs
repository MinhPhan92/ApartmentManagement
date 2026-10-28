using System.Reflection;
using System.Security.Claims;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Controllers;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ApartmentManagement.Tests;

public sealed class SearchControllerTests
{
    private static ControllerContext CreateControllerContext(string role = AppRoles.BuildingManager, string userId = "test-mgr")
    {
        var httpContext = new DefaultHttpContext();
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, role)
        ], "TestAuth");

        httpContext.User = new ClaimsPrincipal(identity);

        return new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public void SearchController_HasAuthorizeAttribute()
    {
        var authAttr = typeof(SearchController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
    }

    [Fact]
    public void SearchController_HasRouteAttribute()
    {
        var routeAttr = typeof(SearchController).GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(routeAttr);
        Assert.Equal("api/search", routeAttr.Template);
    }

    [Fact]
    public async Task Search_CallsService_AndReturnsOkWithResponseDto()
    {
        var mockService = new Mock<ISearchService>();
        var expectedDto = new GlobalSearchResponseDto("101", 1,
        [
            new GlobalSearchGroupDto("Căn hộ",
            [
                new GlobalSearchResultItemDto("Căn hộ", "Căn 101", "Tầng 1", "/Apartments/Details/1", "Trống", "bg-secondary")
            ])
        ]);

        mockService.Setup(s => s.SearchAsync("101", It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(expectedDto);

        var controller = new SearchController(mockService.Object)
        {
            ControllerContext = CreateControllerContext()
        };

        var actionResult = await controller.Search("101");

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var returnedDto = Assert.IsType<GlobalSearchResponseDto>(okResult.Value);
        Assert.Equal("101", returnedDto.Query);
        Assert.Equal(1, returnedDto.TotalCount);
        Assert.Single(returnedDto.Groups);
    }
}

