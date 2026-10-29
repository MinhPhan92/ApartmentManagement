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

public sealed class AnnouncementsControllerTests
{
    private readonly Mock<IAnnouncementService> _mockAnnouncementService;
    private readonly Mock<IBuildingService> _mockBuildingService;
    private readonly AnnouncementsController _controller;

    public AnnouncementsControllerTests()
    {
        _mockAnnouncementService = new Mock<IAnnouncementService>();
        _mockBuildingService = new Mock<IBuildingService>();
        _controller = new AnnouncementsController(_mockAnnouncementService.Object, _mockBuildingService.Object);
    }

    private void SetUserContext(string role, string userId = "test-user")
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, role)
        ], "TestAuth");

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    [Fact]
    public void Controller_HasAuthorizeAttribute()
    {
        var attr = typeof(AnnouncementsController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    public async Task Recent_WhenResident_CallsGetAnnouncementsForResident_AndReturnsTop5()
    {
        const string residentId = "resident-001";
        SetUserContext(AppRoles.Resident, residentId);

        var list = new List<Announcement>
        {
            new() { AnnouncementId = 1, Title = "Tin 1", Category = "Bảo trì", CreatedAtUtc = DateTime.UtcNow },
            new() { AnnouncementId = 2, Title = "Tin 2", Category = "Thu phí", CreatedAtUtc = DateTime.UtcNow.AddHours(-1) },
            new() { AnnouncementId = 3, Title = "Tin 3", Category = "Thông báo chung", CreatedAtUtc = DateTime.UtcNow.AddHours(-2) },
            new() { AnnouncementId = 4, Title = "Tin 4", Category = "An ninh", CreatedAtUtc = DateTime.UtcNow.AddHours(-3) },
            new() { AnnouncementId = 5, Title = "Tin 5", Category = "Thông báo chung", CreatedAtUtc = DateTime.UtcNow.AddHours(-4) },
            new() { AnnouncementId = 6, Title = "Tin 6", Category = "Thông báo chung", CreatedAtUtc = DateTime.UtcNow.AddHours(-5) }
        };

        _mockAnnouncementService.Setup(s => s.GetAnnouncementsForResidentAsync(residentId, 5))
            .ReturnsAsync(list);

        var result = await _controller.Recent();

        var jsonResult = Assert.IsType<JsonResult>(result);
        var data = Assert.IsAssignableFrom<IEnumerable<object>>(jsonResult.Value);
        Assert.Equal(5, data.Count());
        _mockAnnouncementService.Verify(s => s.GetAnnouncementsForResidentAsync(residentId, 5), Times.Once);
        _mockAnnouncementService.Verify(s => s.GetAllAnnouncementsAsync(It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task Recent_WhenStaff_CallsGetAllAnnouncements_AndReturnsTop5()
    {
        const string staffId = "manager-001";
        SetUserContext(AppRoles.BuildingManager, staffId);

        var list = new List<Announcement>
        {
            new() { AnnouncementId = 1, Title = "BQL Tin 1", Category = "Thông báo chung", CreatedAtUtc = DateTime.UtcNow }
        };

        _mockAnnouncementService.Setup(s => s.GetAllAnnouncementsAsync(null, 5))
            .ReturnsAsync(list);

        var result = await _controller.Recent();

        var jsonResult = Assert.IsType<JsonResult>(result);
        var data = Assert.IsAssignableFrom<IEnumerable<object>>(jsonResult.Value);
        Assert.Single(data);
        _mockAnnouncementService.Verify(s => s.GetAllAnnouncementsAsync(null, 5), Times.Once);
        _mockAnnouncementService.Verify(s => s.GetAnnouncementsForResidentAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Recent_WhenNoAnnouncements_ReturnsEmptyArray()
    {
        SetUserContext(AppRoles.BuildingManager);

        _mockAnnouncementService.Setup(s => s.GetAllAnnouncementsAsync(null, 5))
            .ReturnsAsync(new List<Announcement>());

        var result = await _controller.Recent();

        var jsonResult = Assert.IsType<JsonResult>(result);
        var data = Assert.IsAssignableFrom<IEnumerable<object>>(jsonResult.Value);
        Assert.Empty(data);
    }
}

