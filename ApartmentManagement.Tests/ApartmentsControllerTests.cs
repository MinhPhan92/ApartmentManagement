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
    public class ApartmentsControllerUnitTests
    {
        private readonly Mock<IApartmentService> _mockApartmentService;
        private readonly Mock<IBuildingService> _mockBuildingService;
        private readonly ApartmentsController _controller;

        public ApartmentsControllerUnitTests()
        {
            _mockApartmentService = new Mock<IApartmentService>();
            _mockBuildingService = new Mock<IBuildingService>();

            var httpContext = new DefaultHttpContext();
            var tempDataProvider = Mock.Of<ITempDataProvider>();

            _controller = new ApartmentsController(
                _mockApartmentService.Object,
                _mockBuildingService.Object)
            {
                TempData = new TempDataDictionary(httpContext, tempDataProvider)
            };
        }

        #region Index Tests

        [Fact]
        public async Task Index_ReturnsViewResult_WithApartmentList_AndPopulatesViewBag()
        {
            var apartments = new List<Apartment>
            {
                new() { ApartmentId = 1, ApartmentCode = "A101", Floor = 1, Area = 60m }
            };
            var buildings = new List<Building>
            {
                new() { BuildingId = 1, BuildingCode = "BLD-A", BuildingName = "Tòa A" }
            };

            _mockApartmentService.Setup(s => s.GetAllApartmentsAsync(1, "A101"))
                .ReturnsAsync(apartments);
            _mockBuildingService.Setup(s => s.GetAllBuildingsAsync(null))
                .ReturnsAsync(buildings);

            var result = await _controller.Index(1, "A101");

            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<List<Apartment>>(viewResult.Model);
            Assert.Single(model);
            Assert.Equal(1, _controller.ViewBag.SelectedBuildingId);
            Assert.Equal("A101", _controller.ViewBag.SearchTerm);
        }

        #endregion

        #region Details Tests

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task Details_WhenIdInvalidOrZero_ReturnsNotFound(int id)
        {
            var result = await _controller.Details(id);

            Assert.IsType<NotFoundResult>(result);
            _mockApartmentService.Verify(s => s.GetApartmentByIdAsync(It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task Details_WhenApartmentNotFound_ReturnsNotFound()
        {
            _mockApartmentService.Setup(s => s.GetApartmentByIdAsync(999, true))
                .ReturnsAsync((Apartment?)null);

            var result = await _controller.Details(999);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Details_WhenApartmentExists_ReturnsViewResult_WithModel()
        {
            var apt = new Apartment { ApartmentId = 5, ApartmentCode = "A-501", Floor = 5, Area = 70m };
            _mockApartmentService.Setup(s => s.GetApartmentByIdAsync(5, true))
                .ReturnsAsync(apt);

            var result = await _controller.Details(5);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(apt, viewResult.Model);
        }

        #endregion

        #region Create Tests

        [Fact]
        public async Task Create_Get_ReturnsViewResult_WithDefaultApartmentModel_AndBuildingList()
        {
            var buildings = new List<Building>
            {
                new() { BuildingId = 2, BuildingCode = "BLD-B", BuildingName = "Tòa B" }
            };
            _mockBuildingService.Setup(s => s.GetAllBuildingsAsync(null))
                .ReturnsAsync(buildings);

            var result = await _controller.Create(2);

            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<Apartment>(viewResult.Model);
            Assert.Equal(2, model.BuildingId);
            Assert.Equal(1, model.Floor);
            Assert.Equal(ApartmentStatus.Vacant, model.Status);
            Assert.NotNull(_controller.ViewBag.Buildings);
        }

        [Fact]
        public async Task Create_Post_WhenModelStateInvalid_ReturnsViewResult_AndNeverCallsService()
        {
            _controller.ModelState.AddModelError("ApartmentCode", "Mã căn hộ là bắt buộc.");
            var apt = new Apartment { BuildingId = 1, ApartmentCode = "" };
            _mockBuildingService.Setup(s => s.GetAllBuildingsAsync(null))
                .ReturnsAsync(new List<Building>());

            var result = await _controller.Create(apt);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(apt, viewResult.Model);
            _mockApartmentService.Verify(s => s.CreateApartmentAsync(It.IsAny<Apartment>()), Times.Never);
            Assert.NotNull(_controller.ViewBag.Buildings);
        }

        [Fact]
        public async Task Create_Post_WhenServiceSucceeds_RedirectsToIndex_AndSetsSuccessTempData()
        {
            var apt = new Apartment { BuildingId = 1, ApartmentCode = "A-101", Floor = 1, Area = 60m };
            _mockApartmentService.Setup(s => s.CreateApartmentAsync(It.IsAny<Apartment>()))
                .ReturnsAsync((true, null));

            var result = await _controller.Create(apt);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(ApartmentsController.Index), redirect.ActionName);
            Assert.Equal("Thêm căn hộ thành công.", _controller.TempData["SuccessMessage"]);
        }

        [Fact]
        public async Task Create_Post_WhenServiceFails_AddsModelError_ReturnsViewWithModel_AndReloadsBuildings()
        {
            var apt = new Apartment { BuildingId = 1, ApartmentCode = "A-101", Floor = 1, Area = 60m };
            _mockApartmentService.Setup(s => s.CreateApartmentAsync(It.IsAny<Apartment>()))
                .ReturnsAsync((false, "Mã căn hộ đã tồn tại trong tòa nhà."));
            _mockBuildingService.Setup(s => s.GetAllBuildingsAsync(null))
                .ReturnsAsync(new List<Building>());

            var result = await _controller.Create(apt);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(apt, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
            Assert.NotNull(_controller.ViewBag.Buildings);
            Assert.Null(_controller.TempData["SuccessMessage"]);
        }

        #endregion

        #region Edit Tests

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public async Task Edit_Get_WhenIdInvalidOrZero_ReturnsNotFound(int id)
        {
            var result = await _controller.Edit(id);

            Assert.IsType<NotFoundResult>(result);
            _mockApartmentService.Verify(s => s.GetApartmentByIdAsync(It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task Edit_Get_WhenApartmentNotFound_ReturnsNotFound()
        {
            _mockApartmentService.Setup(s => s.GetApartmentByIdAsync(999, false))
                .ReturnsAsync((Apartment?)null);

            var result = await _controller.Edit(999);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Edit_Get_WhenApartmentExists_ReturnsViewResult_WithModel_AndReloadsBuildings()
        {
            var apt = new Apartment { ApartmentId = 10, BuildingId = 1, ApartmentCode = "A-1001", Floor = 10, Area = 80m };
            _mockApartmentService.Setup(s => s.GetApartmentByIdAsync(10, false))
                .ReturnsAsync(apt);
            _mockBuildingService.Setup(s => s.GetAllBuildingsAsync(null))
                .ReturnsAsync(new List<Building>());

            var result = await _controller.Edit(10);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(apt, viewResult.Model);
            Assert.NotNull(_controller.ViewBag.Buildings);
        }

        [Fact]
        public async Task Edit_Post_WhenIdMismatchesModelId_ReturnsNotFound()
        {
            var apt = new Apartment { ApartmentId = 20, ApartmentCode = "A-20" };

            var result = await _controller.Edit(10, apt);

            Assert.IsType<NotFoundResult>(result);
            _mockApartmentService.Verify(s => s.UpdateApartmentAsync(It.IsAny<Apartment>()), Times.Never);
        }

        [Fact]
        public async Task Edit_Post_WhenIdIsZeroOrNegative_ReturnsNotFound()
        {
            var apt = new Apartment { ApartmentId = 0, ApartmentCode = "A-0" };

            var result = await _controller.Edit(0, apt);

            Assert.IsType<NotFoundResult>(result);
            _mockApartmentService.Verify(s => s.UpdateApartmentAsync(It.IsAny<Apartment>()), Times.Never);
        }

        [Fact]
        public async Task Edit_Post_WhenModelStateInvalid_ReturnsViewResult_AndNeverCallsService()
        {
            _controller.ModelState.AddModelError("Floor", "Tầng phải từ 1 đến 200.");
            var apt = new Apartment { ApartmentId = 5, Floor = 0 };
            _mockBuildingService.Setup(s => s.GetAllBuildingsAsync(null))
                .ReturnsAsync(new List<Building>());

            var result = await _controller.Edit(5, apt);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(apt, viewResult.Model);
            _mockApartmentService.Verify(s => s.UpdateApartmentAsync(It.IsAny<Apartment>()), Times.Never);
            Assert.NotNull(_controller.ViewBag.Buildings);
        }

        [Fact]
        public async Task Edit_Post_WhenServiceSucceeds_RedirectsToIndex_AndSetsSuccessTempData()
        {
            var apt = new Apartment { ApartmentId = 5, BuildingId = 1, ApartmentCode = "A-501", Floor = 5, Area = 65m };
            _mockApartmentService.Setup(s => s.UpdateApartmentAsync(It.IsAny<Apartment>()))
                .ReturnsAsync((true, null));

            var result = await _controller.Edit(5, apt);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(ApartmentsController.Index), redirect.ActionName);
            Assert.Equal("Cập nhật căn hộ thành công.", _controller.TempData["SuccessMessage"]);
        }

        [Fact]
        public async Task Edit_Post_WhenServiceReturnsNotFound_ReturnsNotFound()
        {
            var apt = new Apartment { ApartmentId = 5, BuildingId = 1, ApartmentCode = "A-501", Floor = 5, Area = 65m };
            _mockApartmentService.Setup(s => s.UpdateApartmentAsync(It.IsAny<Apartment>()))
                .ReturnsAsync((false, "Không tìm thấy căn hộ."));

            var result = await _controller.Edit(5, apt);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Edit_Post_WhenServiceFailsWithBusinessError_AddsModelError_ReturnsViewWithModel()
        {
            var apt = new Apartment { ApartmentId = 5, BuildingId = 1, ApartmentCode = "A-501", Floor = 5, Area = 65m };
            _mockApartmentService.Setup(s => s.UpdateApartmentAsync(It.IsAny<Apartment>()))
                .ReturnsAsync((false, "Tầng căn hộ không được vượt quá 3."));
            _mockBuildingService.Setup(s => s.GetAllBuildingsAsync(null))
                .ReturnsAsync(new List<Building>());

            var result = await _controller.Edit(5, apt);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(apt, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
            Assert.NotNull(_controller.ViewBag.Buildings);
            Assert.Null(_controller.TempData["SuccessMessage"]);
        }

        #endregion

        #region Delete Tests

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task Delete_Get_WhenIdInvalidOrZero_ReturnsNotFound(int id)
        {
            var result = await _controller.Delete(id);

            Assert.IsType<NotFoundResult>(result);
            _mockApartmentService.Verify(s => s.GetApartmentByIdAsync(It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task Delete_Get_WhenApartmentNotFound_ReturnsNotFound()
        {
            _mockApartmentService.Setup(s => s.GetApartmentByIdAsync(888, true))
                .ReturnsAsync((Apartment?)null);

            var result = await _controller.Delete(888);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Delete_Get_WhenApartmentExists_ReturnsViewResult_WithModel()
        {
            var apt = new Apartment { ApartmentId = 8, ApartmentCode = "A-801", Floor = 8, Area = 70m };
            _mockApartmentService.Setup(s => s.GetApartmentByIdAsync(8, true))
                .ReturnsAsync(apt);

            var result = await _controller.Delete(8);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(apt, viewResult.Model);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-3)]
        public async Task DeleteConfirmed_Post_WhenIdIsInvalid_ReturnsNotFound(int id)
        {
            var result = await _controller.DeleteConfirmed(id);

            Assert.IsType<NotFoundResult>(result);
            _mockApartmentService.Verify(s => s.DeleteApartmentAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task DeleteConfirmed_Post_WhenApartmentNotFound_ReturnsNotFound()
        {
            _mockApartmentService.Setup(s => s.DeleteApartmentAsync(99))
                .ReturnsAsync((false, "Không tìm thấy căn hộ."));

            var result = await _controller.DeleteConfirmed(99);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteConfirmed_Post_WhenServiceSucceeds_RedirectsToIndex_AndSetsSuccessTempData()
        {
            _mockApartmentService.Setup(s => s.DeleteApartmentAsync(1))
                .ReturnsAsync((true, null));

            var result = await _controller.DeleteConfirmed(1);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(ApartmentsController.Index), redirect.ActionName);
            Assert.Equal("Xóa căn hộ thành công.", _controller.TempData["SuccessMessage"]);
            Assert.Null(_controller.TempData["ErrorMessage"]);
        }

        [Fact]
        public async Task DeleteConfirmed_Post_WhenServiceFailsDueToLinkedData_SetsErrorMessage_AndRedirectsToDelete()
        {
            const string error = "Không thể xóa căn hộ đang có cư dân lưu trú.";
            _mockApartmentService.Setup(s => s.DeleteApartmentAsync(1))
                .ReturnsAsync((false, error));

            var result = await _controller.DeleteConfirmed(1);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(ApartmentsController.Delete), redirect.ActionName);
            Assert.Equal(1, redirect.RouteValues?["id"]);
            Assert.Equal(error, _controller.TempData["ErrorMessage"]);
            Assert.Null(_controller.TempData["SuccessMessage"]);
        }

        #endregion

        #region Deactivate Tests

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task Deactivate_Post_WhenIdIsInvalid_ReturnsNotFound(int id)
        {
            var result = await _controller.Deactivate(id);

            Assert.IsType<NotFoundResult>(result);
            _mockApartmentService.Verify(s => s.DeactivateApartmentAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Deactivate_Post_WhenApartmentNotFound_ReturnsNotFound()
        {
            _mockApartmentService.Setup(s => s.DeactivateApartmentAsync(100))
                .ReturnsAsync((false, "Không tìm thấy căn hộ."));

            var result = await _controller.Deactivate(100);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Deactivate_Post_WhenServiceSucceeds_RedirectsToDetails_AndSetsSuccessTempData()
        {
            _mockApartmentService.Setup(s => s.DeactivateApartmentAsync(1))
                .ReturnsAsync((true, null));

            var result = await _controller.Deactivate(1);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(ApartmentsController.Details), redirect.ActionName);
            Assert.Equal(1, redirect.RouteValues?["id"]);
            Assert.Equal("Đã chuyển trạng thái căn hộ sang 'Không hoạt động'.", _controller.TempData["SuccessMessage"]);
        }

        [Fact]
        public async Task Deactivate_Post_WhenServiceFails_SetsErrorMessage_AndRedirectsToDetails()
        {
            const string error = "Không thể ngừng sử dụng căn hộ đang có cư dân lưu trú.";
            _mockApartmentService.Setup(s => s.DeactivateApartmentAsync(1))
                .ReturnsAsync((false, error));

            var result = await _controller.Deactivate(1);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(ApartmentsController.Details), redirect.ActionName);
            Assert.Equal(1, redirect.RouteValues?["id"]);
            Assert.Equal(error, _controller.TempData["ErrorMessage"]);
            Assert.Null(_controller.TempData["SuccessMessage"]);
        }

        #endregion

        #region Remote / Ajax Check Code Exists

        [Fact]
        public async Task CheckCodeExists_ReturnsJsonResult_WithExistsFlag()
        {
            _mockApartmentService.Setup(s => s.ApartmentCodeExistsAsync(1, "A-101", 5))
                .ReturnsAsync(true);

            var result = await _controller.CheckCodeExists(1, "A-101", 5);

            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.NotNull(jsonResult.Value);
        }

        #endregion

        #region Authorization & Security Metadata Attributes

        [Fact]
        public void Controller_HasAuthorizeAttribute_WithRequireManagementPolicy()
        {
            var authAttribute = typeof(ApartmentsController)
                .GetCustomAttribute<AuthorizeAttribute>();

            Assert.NotNull(authAttribute);
            Assert.Equal(AppPolicies.RequireManagement, authAttribute.Policy);
        }

        [Fact]
        public void PostActions_HaveValidateAntiForgeryTokenAttribute()
        {
            var methods = typeof(ApartmentsController)
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

    public class ApartmentsControllerSecurityIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private const string AdminUserId = "dashboard-admin";
        private const string ManagerUserId = "dashboard-manager";
        private const string ResidentUserId = "dashboard-resident-1";

        private readonly CustomWebApplicationFactory _factory;

        public ApartmentsControllerSecurityIntegrationTests(CustomWebApplicationFactory factory)
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
        public async Task Anonymous_ApartmentsIndex_RedirectsToLogin()
        {
            using var client = CreateClient();
            using var response = await client.GetAsync("/Apartments");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Identity/Account/Login", response.Headers.Location?.AbsolutePath);
        }

        [Fact]
        public async Task Resident_ApartmentsIndex_ReturnsForbidden()
        {
            using var client = CreateAuthenticatedClient(AppRoles.Resident, ResidentUserId);
            using var response = await client.GetAsync("/Apartments");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task BuildingManager_ApartmentsIndex_ReturnsSuccess()
        {
            using var client = CreateAuthenticatedClient(AppRoles.BuildingManager, ManagerUserId);
            using var response = await client.GetAsync("/Apartments");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task SuperAdmin_ApartmentsIndex_ReturnsSuccess()
        {
            using var client = CreateAuthenticatedClient(AppRoles.SuperAdmin, AdminUserId);
            using var response = await client.GetAsync("/Apartments");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Resident_ApartmentsCreate_ReturnsForbidden()
        {
            using var client = CreateAuthenticatedClient(AppRoles.Resident, ResidentUserId);
            using var response = await client.GetAsync("/Apartments/Create");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task BuildingManager_ApartmentsCreate_ReturnsSuccess()
        {
            using var client = CreateAuthenticatedClient(AppRoles.BuildingManager, ManagerUserId);
            using var response = await client.GetAsync("/Apartments/Create");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
