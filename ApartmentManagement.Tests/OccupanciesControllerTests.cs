using System.Net;
using System.Reflection;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Controllers;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using ApartmentManagement.Tests.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace ApartmentManagement.Tests
{
    public class OccupanciesControllerUnitTests
    {
        private readonly Mock<IOccupancyService> _mockService;
        private readonly Mock<IApartmentService> _mockApartmentService;
        private readonly Mock<IResidentService> _mockResidentService;
        private readonly OccupanciesController _controller;

        public OccupanciesControllerUnitTests()
        {
            _mockService = new Mock<IOccupancyService>();
            _mockApartmentService = new Mock<IApartmentService>();
            _mockResidentService = new Mock<IResidentService>();

            var httpContext = new DefaultHttpContext();
            var tempDataProvider = Mock.Of<ITempDataProvider>();

            _controller = new OccupanciesController(
                _mockService.Object, _mockApartmentService.Object, _mockResidentService.Object)
            {
                TempData = new TempDataDictionary(httpContext, tempDataProvider)
            };
        }

        private static ApartmentResident CreateSampleOccupancy(int id = 1, bool isMovedOut = false)
        {
            return new ApartmentResident
            {
                ApartmentResidentId = id,
                ApartmentId = 10,
                ResidentId = 20,
                Relationship = "Chủ hộ",
                MoveInDate = new DateTime(2025, 1, 1),
                MoveOutDate = isMovedOut ? new DateTime(2025, 12, 1) : null,
                IsOwner = true,
                Apartment = new Apartment
                {
                    ApartmentId = 10,
                    ApartmentCode = "A101",
                    Building = new Building { BuildingId = 1, BuildingName = "Tòa Sapphire" }
                },
                Resident = new Resident
                {
                    ResidentId = 20,
                    CitizenId = "012345678901",
                    User = new ApplicationUser { Id = "user-1", FullName = "Nguyễn Văn A", Email = "a@test.local" }
                }
            };
        }

        #region Details Tests

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task Details_WhenIdNonPositive_ReturnsNotFound(int invalidId)
        {
            var result = await _controller.Details(invalidId);
            Assert.IsType<NotFoundResult>(result);
            _mockService.Verify(s => s.GetByIdAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Details_WhenNotFound_ReturnsNotFound()
        {
            _mockService.Setup(s => s.GetByIdAsync(99)).ReturnsAsync((ApartmentResident?)null);

            var result = await _controller.Details(99);
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Details_WhenFound_ReturnsViewResultWithModel()
        {
            var occupancy = CreateSampleOccupancy(1);
            _mockService.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(occupancy);

            var result = await _controller.Details(1);
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(occupancy, viewResult.Model);
        }

        #endregion

        #region MoveOut GET Tests

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task MoveOut_Get_WhenIdNonPositive_ReturnsNotFound(int invalidId)
        {
            var result = await _controller.MoveOut(invalidId);
            Assert.IsType<NotFoundResult>(result);
            _mockService.Verify(s => s.GetByIdAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task MoveOut_Get_WhenNotFound_ReturnsNotFound()
        {
            _mockService.Setup(s => s.GetByIdAsync(99)).ReturnsAsync((ApartmentResident?)null);

            var result = await _controller.MoveOut(99);
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task MoveOut_Get_WhenAlreadyMovedOut_ReturnsBadRequest()
        {
            var occupancy = CreateSampleOccupancy(1, isMovedOut: true);
            _mockService.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(occupancy);

            var result = await _controller.MoveOut(1);
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task MoveOut_Get_WhenActiveOccupancy_PopulatesViewBagAndReturnsView()
        {
            var occupancy = CreateSampleOccupancy(1, isMovedOut: false);
            _mockService.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(occupancy);

            var result = await _controller.MoveOut(1);
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<MoveOutViewModel>(viewResult.Model);
            Assert.Equal(1, model.ApartmentResidentId);
            Assert.Same(occupancy, _controller.ViewBag.Occupancy);
        }

        #endregion

        #region MoveOut POST Tests

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task MoveOut_Post_WhenIdNonPositive_ReturnsNotFound(int invalidId)
        {
            var model = new MoveOutViewModel { ApartmentResidentId = invalidId, MoveOutDate = DateTime.Today };

            var result = await _controller.MoveOut(model);
            Assert.IsType<NotFoundResult>(result);
            _mockService.Verify(s => s.MoveOutAsync(It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task MoveOut_Post_WhenOccupancyNotFound_ReturnsNotFound()
        {
            _mockService.Setup(s => s.GetByIdAsync(99)).ReturnsAsync((ApartmentResident?)null);

            var model = new MoveOutViewModel { ApartmentResidentId = 99, MoveOutDate = DateTime.Today };

            var result = await _controller.MoveOut(model);
            Assert.IsType<NotFoundResult>(result);
            _mockService.Verify(s => s.MoveOutAsync(It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task MoveOut_Post_WhenModelStateInvalid_PopulatesViewBagAndDoesNotCallService()
        {
            var occupancy = CreateSampleOccupancy(1);
            _mockService.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(occupancy);

            _controller.ModelState.AddModelError("MoveOutDate", "Ngày chuyển đi không hợp lệ.");
            var model = new MoveOutViewModel { ApartmentResidentId = 1 };

            var result = await _controller.MoveOut(model);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(model, viewResult.Model);
            Assert.Same(occupancy, _controller.ViewBag.Occupancy);
            _mockService.Verify(s => s.MoveOutAsync(It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task MoveOut_Post_WhenServiceReturnsNotFound_ReturnsNotFound()
        {
            var occupancy = CreateSampleOccupancy(1);
            _mockService.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(occupancy);
            _mockService.Setup(s => s.MoveOutAsync(1, It.IsAny<DateTime>()))
                .ReturnsAsync((false, "Không tìm thấy hồ sơ cư trú."));

            var model = new MoveOutViewModel { ApartmentResidentId = 1, MoveOutDate = DateTime.Today };

            var result = await _controller.MoveOut(model);
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task MoveOut_Post_WhenServiceFails_AddsModelError_ReconstructsViewBag_AndReturnsView()
        {
            var occupancy = CreateSampleOccupancy(1);
            _mockService.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(occupancy);
            _mockService.Setup(s => s.MoveOutAsync(1, It.IsAny<DateTime>()))
                .ReturnsAsync((false, "Ngày chuyển đi không thể trước ngày chuyển vào."));

            var model = new MoveOutViewModel { ApartmentResidentId = 1, MoveOutDate = new DateTime(2024, 1, 1) };

            var result = await _controller.MoveOut(model);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(model, viewResult.Model);
            Assert.Same(occupancy, _controller.ViewBag.Occupancy);
            Assert.False(_controller.ModelState.IsValid);
            Assert.NotNull(_controller.ModelState[string.Empty]);
        }

        [Fact]
        public async Task MoveOut_Post_WhenSuccess_RedirectsToDetails_AndSetsTempData()
        {
            var occupancy = CreateSampleOccupancy(1);
            _mockService.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(occupancy);
            _mockService.Setup(s => s.MoveOutAsync(1, It.IsAny<DateTime>()))
                .ReturnsAsync((true, null));

            var model = new MoveOutViewModel { ApartmentResidentId = 1, MoveOutDate = DateTime.Today };

            var result = await _controller.MoveOut(model);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(OccupanciesController.Details), redirect.ActionName);
            Assert.Equal(1, redirect.RouteValues?["id"]);
            Assert.NotNull(_controller.TempData["SuccessMessage"]);
        }

        #endregion

        #region Edit Tests

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task Edit_Get_WhenIdNonPositive_ReturnsNotFound(int invalidId)
        {
            var result = await _controller.Edit(invalidId);
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Edit_Get_WhenNotFound_ReturnsNotFound()
        {
            _mockService.Setup(s => s.GetByIdAsync(99)).ReturnsAsync((ApartmentResident?)null);

            var result = await _controller.Edit(99);
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Edit_Post_WhenIdMismatch_ReturnsNotFound()
        {
            var model = new OccupancyEditViewModel { ApartmentResidentId = 2 };

            var result = await _controller.Edit(1, model);
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Edit_Post_WhenServiceReportsNotFound_ReturnsNotFound()
        {
            _mockService.Setup(s => s.UpdateOccupancyAsync(It.IsAny<OccupancyEditViewModel>()))
                .ReturnsAsync((false, "Không tìm thấy hồ sơ cư trú."));

            var model = new OccupancyEditViewModel { ApartmentResidentId = 1, Relationship = "Thành viên" };

            var result = await _controller.Edit(1, model);
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Edit_Post_WhenServiceFails_AddsModelError_RepopulatesDisplay_AndReturnsView()
        {
            var occupancy = CreateSampleOccupancy(1);
            _mockService.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(occupancy);
            _mockService.Setup(s => s.UpdateOccupancyAsync(It.IsAny<OccupancyEditViewModel>()))
                .ReturnsAsync((false, "Căn hộ đã có chủ hộ đang hoạt động."));

            var model = new OccupancyEditViewModel
            {
                ApartmentResidentId = 1,
                Relationship = "Chủ hộ",
                IsOwner = true
            };

            var result = await _controller.Edit(1, model);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(model, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
            Assert.Equal("A101", model.ApartmentCode);
            Assert.Equal("Nguyễn Văn A", model.ResidentName);
        }

        #endregion

        #region Security & AntiForgery Attributes

        [Fact]
        public void Controller_HasAuthorizeAttribute_WithRequireManagementPolicy()
        {
            var authAttribute = typeof(OccupanciesController)
                .GetCustomAttribute<AuthorizeAttribute>();

            Assert.NotNull(authAttribute);
            Assert.Equal(AppPolicies.RequireManagement, authAttribute.Policy);
        }

        [Fact]
        public void PostActions_HaveValidateAntiForgeryTokenAttribute()
        {
            var methods = typeof(OccupanciesController)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

            var postMethods = methods.Where(m => m.GetCustomAttribute<HttpPostAttribute>() != null).ToList();

            Assert.NotEmpty(postMethods);
            foreach (var postMethod in postMethods)
            {
                var antiForgery = postMethod.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>();
                Assert.True(antiForgery != null, $"Phương thức POST '{postMethod.Name}' thiếu [ValidateAntiForgeryToken].");
            }
        }

        #endregion
    }

    public class OccupanciesControllerSecurityIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private const string AdminUserId = "dashboard-admin";
        private const string ManagerUserId = "dashboard-manager";
        private const string ResidentUserId = "dashboard-resident-1";

        private readonly CustomWebApplicationFactory _factory;

        public OccupanciesControllerSecurityIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private HttpClient CreateClient(bool allowAutoRedirect = false) =>
            _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = allowAutoRedirect,
                BaseAddress = new Uri("https://localhost")
            });

        private HttpClient CreateAuthenticatedClient(string role, string userId, bool allowAutoRedirect = false)
        {
            var client = CreateClient(allowAutoRedirect);
            client.DefaultRequestHeaders.Add("X-Test-Role", role);
            client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
            return client;
        }

        [Fact]
        public async Task Anonymous_OccupanciesIndex_RedirectsToLogin()
        {
            using var client = CreateClient();
            using var response = await client.GetAsync("/Occupancies");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Identity/Account/Login", response.Headers.Location?.AbsolutePath);
        }

        [Fact]
        public async Task Resident_OccupanciesIndex_ReturnsForbidden()
        {
            using var client = CreateAuthenticatedClient(AppRoles.Resident, ResidentUserId);
            using var response = await client.GetAsync("/Occupancies");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task BuildingManager_OccupanciesIndex_ReturnsSuccess()
        {
            using var client = CreateAuthenticatedClient(AppRoles.BuildingManager, ManagerUserId);
            using var response = await client.GetAsync("/Occupancies");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task SuperAdmin_OccupanciesIndex_ReturnsSuccess()
        {
            using var client = CreateAuthenticatedClient(AppRoles.SuperAdmin, AdminUserId);
            using var response = await client.GetAsync("/Occupancies");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Resident_OccupanciesCreate_ReturnsForbidden()
        {
            using var client = CreateAuthenticatedClient(AppRoles.Resident, ResidentUserId);
            using var response = await client.GetAsync("/Occupancies/Create");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task BuildingManager_OccupanciesCreate_ReturnsSuccess()
        {
            using var client = CreateAuthenticatedClient(AppRoles.BuildingManager, ManagerUserId);
            using var response = await client.GetAsync("/Occupancies/Create");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
