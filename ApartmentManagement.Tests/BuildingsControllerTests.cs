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
    public class BuildingsControllerUnitTests
    {
        private readonly Mock<IBuildingService> _mockService;
        private readonly BuildingsController _controller;

        public BuildingsControllerUnitTests()
        {
            _mockService = new Mock<IBuildingService>();
            var httpContext = new DefaultHttpContext();
            var tempDataProvider = Mock.Of<ITempDataProvider>();

            _controller = new BuildingsController(_mockService.Object)
            {
                TempData = new TempDataDictionary(httpContext, tempDataProvider)
            };
        }

        #region Create Tests

        [Fact]
        public void Create_Get_ReturnsViewResult_WithDefaultBuildingModel()
        {
            var result = _controller.Create();

            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<Building>(viewResult.Model);
            Assert.Equal(1, model.NumberOfFloors);
        }

        [Fact]
        public async Task Create_Post_WhenModelStateInvalid_ReturnsViewResult_AndNeverCallsService()
        {
            _controller.ModelState.AddModelError("BuildingCode", "Mã tòa nhà là bắt buộc.");
            var building = new Building { BuildingCode = "", BuildingName = "Tòa 1" };

            var result = await _controller.Create(building);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(building, viewResult.Model);
            _mockService.Verify(s => s.CreateBuildingAsync(It.IsAny<Building>()), Times.Never);
        }

        [Fact]
        public async Task Create_Post_WhenServiceSucceeds_RedirectsToIndex_AndSetsSuccessTempData()
        {
            var building = new Building
            {
                BuildingId = 1,
                BuildingCode = "BLD-A",
                BuildingName = "Tòa Tháp A",
                NumberOfFloors = 10
            };

            _mockService.Setup(s => s.CreateBuildingAsync(It.IsAny<Building>()))
                .ReturnsAsync((true, null, building));

            var result = await _controller.Create(building);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(BuildingsController.Index), redirect.ActionName);
            Assert.NotNull(_controller.TempData["SuccessMessage"]);
            Assert.Contains("Tòa Tháp A", _controller.TempData["SuccessMessage"]!.ToString());
        }

        [Fact]
        public async Task Create_Post_WhenServiceFails_AddsModelError_ReturnsViewWithModel_AndDoesNotRedirect()
        {
            var building = new Building
            {
                BuildingCode = "BLD-A",
                BuildingName = "Tòa Tháp A",
                NumberOfFloors = 10
            };

            _mockService.Setup(s => s.CreateBuildingAsync(It.IsAny<Building>()))
                .ReturnsAsync((false, "Mã tòa nhà đã tồn tại trong hệ thống.", null));

            var result = await _controller.Create(building);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(building, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
            Assert.Null(_controller.TempData["SuccessMessage"]);
        }

        #endregion

        #region Edit Tests

        [Fact]
        public async Task Edit_Get_WhenIdIsNull_ReturnsNotFound()
        {
            var result = await _controller.Edit(null);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Edit_Get_WhenBuildingNotFound_ReturnsNotFound()
        {
            _mockService.Setup(s => s.GetBuildingByIdAsync(999, false))
                .ReturnsAsync((Building?)null);

            var result = await _controller.Edit(999);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Edit_Get_WhenBuildingExists_ReturnsViewResult_WithBuildingModel()
        {
            var building = new Building { BuildingId = 5, BuildingCode = "T05", BuildingName = "Tòa 5" };
            _mockService.Setup(s => s.GetBuildingByIdAsync(5, false))
                .ReturnsAsync(building);

            var result = await _controller.Edit(5);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(building, viewResult.Model);
        }

        [Fact]
        public async Task Edit_Post_WhenIdMismatchesModelId_ReturnsNotFound()
        {
            var building = new Building { BuildingId = 6, BuildingCode = "T06" };

            var result = await _controller.Edit(5, building);

            Assert.IsType<NotFoundResult>(result);
            _mockService.Verify(s => s.UpdateBuildingAsync(It.IsAny<Building>()), Times.Never);
        }

        [Fact]
        public async Task Edit_Post_WhenModelStateInvalid_ReturnsViewResult_AndNeverCallsService()
        {
            _controller.ModelState.AddModelError("BuildingName", "Tên tòa nhà là bắt buộc.");
            var building = new Building { BuildingId = 5, BuildingCode = "T05" };

            var result = await _controller.Edit(5, building);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(building, viewResult.Model);
            _mockService.Verify(s => s.UpdateBuildingAsync(It.IsAny<Building>()), Times.Never);
        }

        [Fact]
        public async Task Edit_Post_WhenServiceSucceeds_RedirectsToIndex_AndSetsSuccessTempData()
        {
            var building = new Building { BuildingId = 5, BuildingCode = "T05", BuildingName = "Tòa 5 Cập Nhật" };
            _mockService.Setup(s => s.UpdateBuildingAsync(It.IsAny<Building>()))
                .ReturnsAsync((true, null));

            var result = await _controller.Edit(5, building);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(BuildingsController.Index), redirect.ActionName);
            Assert.NotNull(_controller.TempData["SuccessMessage"]);
            Assert.Contains("Tòa 5 Cập Nhật", _controller.TempData["SuccessMessage"]!.ToString());
        }

        [Fact]
        public async Task Edit_Post_WhenServiceReturnsNotFound_ReturnsNotFound()
        {
            var building = new Building { BuildingId = 5, BuildingCode = "T05", BuildingName = "Tòa 5" };
            _mockService.Setup(s => s.UpdateBuildingAsync(It.IsAny<Building>()))
                .ReturnsAsync((false, "Không tìm thấy tòa nhà cần cập nhật."));

            var result = await _controller.Edit(5, building);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Edit_Post_WhenServiceFailsWithBusinessError_AddsModelError_ReturnsViewWithModel()
        {
            var building = new Building { BuildingId = 5, BuildingCode = "T05", BuildingName = "Tòa 5" };
            _mockService.Setup(s => s.UpdateBuildingAsync(It.IsAny<Building>()))
                .ReturnsAsync((false, "Không thể cập nhật số tầng vì đang có căn hộ ở tầng 15."));

            var result = await _controller.Edit(5, building);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(building, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
            Assert.Null(_controller.TempData["SuccessMessage"]);
        }

        #endregion

        #region Details Tests

        [Fact]
        public async Task Details_Get_WhenIdIsNull_ReturnsNotFound()
        {
            var result = await _controller.Details(null);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Details_Get_WhenBuildingNotFound_ReturnsNotFound()
        {
            _mockService.Setup(s => s.GetBuildingByIdAsync(100, true))
                .ReturnsAsync((Building?)null);

            var result = await _controller.Details(100);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Details_Get_WhenBuildingExists_ReturnsViewResult_WithBuildingModel()
        {
            var building = new Building { BuildingId = 1, BuildingCode = "T01", BuildingName = "Tòa 1" };
            _mockService.Setup(s => s.GetBuildingByIdAsync(1, true))
                .ReturnsAsync(building);

            var result = await _controller.Details(1);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(building, viewResult.Model);
        }

        #endregion

        #region Delete Tests

        [Fact]
        public async Task Delete_Get_WhenIdIsNull_ReturnsNotFound()
        {
            var result = await _controller.Delete(null);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Delete_Get_WhenBuildingNotFound_ReturnsNotFound()
        {
            _mockService.Setup(s => s.GetBuildingByIdAsync(50, true))
                .ReturnsAsync((Building?)null);

            var result = await _controller.Delete(50);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Delete_Get_WhenBuildingExists_ReturnsViewResult_WithApartmentCountInViewBag()
        {
            var building = new Building
            {
                BuildingId = 1,
                BuildingCode = "T01",
                Apartments = new List<Apartment>
                {
                    new() { ApartmentId = 1, ApartmentCode = "A1" },
                    new() { ApartmentId = 2, ApartmentCode = "A2" }
                }
            };
            _mockService.Setup(s => s.GetBuildingByIdAsync(1, true))
                .ReturnsAsync(building);

            var result = await _controller.Delete(1);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(building, viewResult.Model);
            Assert.Equal(2, _controller.ViewBag.ApartmentCount);
        }

        [Fact]
        public async Task DeleteConfirmed_Post_WhenIdIsInvalid_ReturnsNotFound()
        {
            var result = await _controller.DeleteConfirmed(0);

            Assert.IsType<NotFoundResult>(result);
            _mockService.Verify(s => s.DeleteBuildingAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task DeleteConfirmed_Post_WhenBuildingNotFound_ReturnsNotFound()
        {
            _mockService.Setup(s => s.DeleteBuildingAsync(99))
                .ReturnsAsync((false, "Không tìm thấy tòa nhà cần xóa."));

            var result = await _controller.DeleteConfirmed(99);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteConfirmed_Post_WhenServiceSucceeds_RedirectsToIndex_AndSetsSuccessTempData()
        {
            _mockService.Setup(s => s.DeleteBuildingAsync(1))
                .ReturnsAsync((true, null));

            var result = await _controller.DeleteConfirmed(1);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(BuildingsController.Index), redirect.ActionName);
            Assert.Equal("Đã xóa tòa nhà thành công.", _controller.TempData["SuccessMessage"]);
            Assert.Null(_controller.TempData["ErrorMessage"]);
        }

        [Fact]
        public async Task DeleteConfirmed_Post_WhenServiceFailsDueToLinkedData_SetsErrorMessage_AndRedirectsToIndex()
        {
            const string error = "Không thể xóa tòa nhà 'Tòa A' vì đang có 3 căn hộ trực thuộc.";
            _mockService.Setup(s => s.DeleteBuildingAsync(1))
                .ReturnsAsync((false, error));

            var result = await _controller.DeleteConfirmed(1);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(BuildingsController.Index), redirect.ActionName);
            Assert.Equal(error, _controller.TempData["ErrorMessage"]);
            Assert.Null(_controller.TempData["SuccessMessage"]);
        }

        #endregion

        #region Remote / Ajax Check Code Exists

        [Fact]
        public async Task CheckCodeExists_ReturnsJsonResult_WithExistsFlag()
        {
            _mockService.Setup(s => s.BuildingCodeExistsAsync("BLD-A", 1))
                .ReturnsAsync(true);

            var result = await _controller.CheckCodeExists("BLD-A", 1);

            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.NotNull(jsonResult.Value);
        }

        #endregion

        #region Authorization & Security Metadata Attributes

        [Fact]
        public void Controller_HasAuthorizeAttribute_WithRequireManagementPolicy()
        {
            var authAttribute = typeof(BuildingsController)
                .GetCustomAttribute<AuthorizeAttribute>();

            Assert.NotNull(authAttribute);
            Assert.Equal(AppPolicies.RequireManagement, authAttribute.Policy);
        }

        [Fact]
        public void PostActions_HaveValidateAntiForgeryTokenAttribute()
        {
            var methods = typeof(BuildingsController)
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

    public class BuildingsControllerSecurityIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private const string AdminUserId = "dashboard-admin";
        private const string ManagerUserId = "dashboard-manager";
        private const string ResidentUserId = "dashboard-resident-1";

        private readonly CustomWebApplicationFactory _factory;

        public BuildingsControllerSecurityIntegrationTests(CustomWebApplicationFactory factory)
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
        public async Task Anonymous_BuildingsIndex_RedirectsToLogin()
        {
            using var client = CreateClient();
            using var response = await client.GetAsync("/Buildings");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Identity/Account/Login", response.Headers.Location?.AbsolutePath);
        }

        [Fact]
        public async Task Resident_BuildingsIndex_ReturnsForbidden()
        {
            using var client = CreateAuthenticatedClient(AppRoles.Resident, ResidentUserId);
            using var response = await client.GetAsync("/Buildings");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task BuildingManager_BuildingsIndex_ReturnsSuccess()
        {
            using var client = CreateAuthenticatedClient(AppRoles.BuildingManager, ManagerUserId);
            using var response = await client.GetAsync("/Buildings");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task SuperAdmin_BuildingsIndex_ReturnsSuccess()
        {
            using var client = CreateAuthenticatedClient(AppRoles.SuperAdmin, AdminUserId);
            using var response = await client.GetAsync("/Buildings");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Resident_BuildingsCreate_ReturnsForbidden()
        {
            using var client = CreateAuthenticatedClient(AppRoles.Resident, ResidentUserId);
            using var response = await client.GetAsync("/Buildings/Create");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task BuildingManager_BuildingsCreate_ReturnsSuccess()
        {
            using var client = CreateAuthenticatedClient(AppRoles.BuildingManager, ManagerUserId);
            using var response = await client.GetAsync("/Buildings/Create");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
