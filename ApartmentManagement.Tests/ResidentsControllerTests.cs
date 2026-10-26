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
    public class ResidentsControllerUnitTests
    {
        private readonly Mock<IResidentService> _mockService;
        private readonly ResidentsController _controller;

        public ResidentsControllerUnitTests()
        {
            _mockService = new Mock<IResidentService>();
            var httpContext = new DefaultHttpContext();
            var tempDataProvider = Mock.Of<ITempDataProvider>();

            _controller = new ResidentsController(_mockService.Object)
            {
                TempData = new TempDataDictionary(httpContext, tempDataProvider)
            };
        }

        private static Resident CreateSampleResident(int id = 1)
        {
            return new Resident
            {
                ResidentId = id,
                CitizenId = "012345678901",
                DateOfBirth = new DateTime(1990, 5, 10),
                Gender = "Nam",
                Address = "P101, Tòa Sapphire",
                EmergencyContact = "0912345678",
                User = new ApplicationUser
                {
                    Id = "user-1",
                    FullName = "Nguyễn Văn A",
                    Email = "a@resident.local"
                }
            };
        }

        #region Index Tests

        [Fact]
        public async Task Index_ReturnsViewResultWithListOfResidents()
        {
            var residents = new List<Resident> { CreateSampleResident(1), CreateSampleResident(2) };
            _mockService.Setup(s => s.GetAllAsync(It.IsAny<string?>())).ReturnsAsync(residents);

            var result = await _controller.Index("term");

            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<Resident>>(viewResult.Model);
            Assert.Equal(2, model.Count());
            Assert.Equal("term", _controller.ViewBag.CurrentSearch);
        }

        #endregion

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
            _mockService.Setup(s => s.GetByIdAsync(99)).ReturnsAsync((Resident?)null);

            var result = await _controller.Details(99);
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Details_WhenFound_ReturnsViewResultWithResident()
        {
            var resident = CreateSampleResident(1);
            _mockService.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(resident);

            var result = await _controller.Details(1);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(resident, viewResult.Model);
        }

        #endregion

        #region Create Tests

        [Fact]
        public void Create_Get_ReturnsViewResultWithEmptyModel()
        {
            var result = _controller.Create();
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.IsType<ResidentCreateViewModel>(viewResult.Model);
        }

        [Fact]
        public async Task Create_Post_WhenModelStateInvalid_ReturnsViewAndDoesNotCallService()
        {
            _controller.ModelState.AddModelError("Email", "Email bắt buộc");
            var model = new ResidentCreateViewModel();

            var result = await _controller.Create(model);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(model, viewResult.Model);
            _mockService.Verify(s => s.CreateAsync(It.IsAny<ResidentCreateViewModel>()), Times.Never);
        }

        [Fact]
        public async Task Create_Post_WhenServiceFails_AddsModelErrorAndReturnsView()
        {
            _mockService.Setup(s => s.CreateAsync(It.IsAny<ResidentCreateViewModel>()))
                .ReturnsAsync((false, "Số CCCD/CMND đã tồn tại.", null));

            var model = new ResidentCreateViewModel { CitizenId = "012345678901" };

            var result = await _controller.Create(model);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(model, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
        }

        [Fact]
        public async Task Create_Post_WhenSuccess_RedirectsToDetails_AndSetsTempData()
        {
            var created = CreateSampleResident(5);
            _mockService.Setup(s => s.CreateAsync(It.IsAny<ResidentCreateViewModel>()))
                .ReturnsAsync((true, null, created));

            var model = new ResidentCreateViewModel { FullName = "Nguyễn Văn Mới" };

            var result = await _controller.Create(model);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(ResidentsController.Details), redirect.ActionName);
            Assert.Equal(5, redirect.RouteValues?["id"]);
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
            _mockService.Verify(s => s.GetByIdAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Edit_Get_WhenNotFound_ReturnsNotFound()
        {
            _mockService.Setup(s => s.GetByIdAsync(99)).ReturnsAsync((Resident?)null);

            var result = await _controller.Edit(99);
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Edit_Get_WhenFound_ReturnsViewWithViewModel()
        {
            var resident = CreateSampleResident(1);
            _mockService.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(resident);

            var result = await _controller.Edit(1);

            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<ResidentEditViewModel>(viewResult.Model);
            Assert.Equal(1, model.ResidentId);
            Assert.Equal("Nguyễn Văn A", model.FullName);
            Assert.Equal("012345678901", model.CitizenId);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task Edit_Post_WhenIdNonPositive_ReturnsNotFound(int invalidId)
        {
            var model = new ResidentEditViewModel { ResidentId = invalidId };

            var result = await _controller.Edit(invalidId, model);
            Assert.IsType<NotFoundResult>(result);
            _mockService.Verify(s => s.UpdateAsync(It.IsAny<ResidentEditViewModel>()), Times.Never);
        }

        [Fact]
        public async Task Edit_Post_WhenIdMismatch_ReturnsNotFound()
        {
            var model = new ResidentEditViewModel { ResidentId = 2 };

            var result = await _controller.Edit(1, model);
            Assert.IsType<NotFoundResult>(result);
            _mockService.Verify(s => s.UpdateAsync(It.IsAny<ResidentEditViewModel>()), Times.Never);
        }

        [Fact]
        public async Task Edit_Post_WhenModelStateInvalid_ReturnsViewAndDoesNotCallService()
        {
            _controller.ModelState.AddModelError("FullName", "Họ tên bắt buộc");
            var model = new ResidentEditViewModel { ResidentId = 1 };

            var result = await _controller.Edit(1, model);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(model, viewResult.Model);
            _mockService.Verify(s => s.UpdateAsync(It.IsAny<ResidentEditViewModel>()), Times.Never);
        }

        [Fact]
        public async Task Edit_Post_WhenServiceReportsNotFound_ReturnsNotFound()
        {
            _mockService.Setup(s => s.UpdateAsync(It.IsAny<ResidentEditViewModel>()))
                .ReturnsAsync((false, "Không tìm thấy cư dân."));

            var model = new ResidentEditViewModel { ResidentId = 1, FullName = "Tên Mới" };

            var result = await _controller.Edit(1, model);
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Edit_Post_WhenServiceFails_AddsModelErrorAndReturnsView()
        {
            _mockService.Setup(s => s.UpdateAsync(It.IsAny<ResidentEditViewModel>()))
                .ReturnsAsync((false, "Số CCCD/CMND đã tồn tại."));

            var model = new ResidentEditViewModel { ResidentId = 1, FullName = "Tên Mới" };

            var result = await _controller.Edit(1, model);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(model, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
        }

        [Fact]
        public async Task Edit_Post_WhenSuccess_RedirectsToDetails_AndSetsTempData()
        {
            _mockService.Setup(s => s.UpdateAsync(It.IsAny<ResidentEditViewModel>()))
                .ReturnsAsync((true, null));

            var model = new ResidentEditViewModel { ResidentId = 1, FullName = "Tên Mới" };

            var result = await _controller.Edit(1, model);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(ResidentsController.Details), redirect.ActionName);
            Assert.Equal(1, redirect.RouteValues?["id"]);
            Assert.NotNull(_controller.TempData["SuccessMessage"]);
        }

        #endregion

        #region Delete Tests

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task Delete_Get_WhenIdNonPositive_ReturnsNotFound(int invalidId)
        {
            var result = await _controller.Delete(invalidId);
            Assert.IsType<NotFoundResult>(result);
            _mockService.Verify(s => s.GetByIdAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Delete_Get_WhenNotFound_ReturnsNotFound()
        {
            _mockService.Setup(s => s.GetByIdAsync(99)).ReturnsAsync((Resident?)null);

            var result = await _controller.Delete(99);
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Delete_Get_WhenFound_ReturnsViewWithModel()
        {
            var resident = CreateSampleResident(1);
            _mockService.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(resident);

            var result = await _controller.Delete(1);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(resident, viewResult.Model);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task DeleteConfirmed_Post_WhenIdNonPositive_ReturnsNotFound(int invalidId)
        {
            var result = await _controller.DeleteConfirmed(invalidId);
            Assert.IsType<NotFoundResult>(result);
            _mockService.Verify(s => s.DeleteAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task DeleteConfirmed_Post_WhenNotFound_ReturnsNotFound()
        {
            _mockService.Setup(s => s.DeleteAsync(99))
                .ReturnsAsync((false, "Không tìm thấy cư dân."));

            var result = await _controller.DeleteConfirmed(99);
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteConfirmed_Post_WhenServiceFails_SetsTempDataErrorMessage_AndRedirectsToDelete()
        {
            _mockService.Setup(s => s.DeleteAsync(1))
                .ReturnsAsync((false, "Không thể xóa cư dân đã phát sinh dữ liệu liên kết."));

            var result = await _controller.DeleteConfirmed(1);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(ResidentsController.Delete), redirect.ActionName);
            Assert.Equal(1, redirect.RouteValues?["id"]);
            Assert.Equal("Không thể xóa cư dân đã phát sinh dữ liệu liên kết.", _controller.TempData["ErrorMessage"]);
        }

        [Fact]
        public async Task DeleteConfirmed_Post_WhenSuccess_SetsTempDataSuccessMessage_AndRedirectsToIndex()
        {
            _mockService.Setup(s => s.DeleteAsync(1))
                .ReturnsAsync((true, null));

            var result = await _controller.DeleteConfirmed(1);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(ResidentsController.Index), redirect.ActionName);
            Assert.NotNull(_controller.TempData["SuccessMessage"]);
        }

        #endregion

        #region Security & AntiForgery Attributes

        [Fact]
        public void Controller_HasAuthorizeAttribute_WithRequireManagementPolicy()
        {
            var authAttribute = typeof(ResidentsController)
                .GetCustomAttribute<AuthorizeAttribute>();

            Assert.NotNull(authAttribute);
            Assert.Equal(AppPolicies.RequireManagement, authAttribute.Policy);
        }

        [Fact]
        public void PostActions_HaveValidateAntiForgeryTokenAttribute()
        {
            var methods = typeof(ResidentsController)
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

    public class ResidentsControllerSecurityIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private const string AdminUserId = "dashboard-admin";
        private const string ManagerUserId = "dashboard-manager";
        private const string ResidentUserId = "dashboard-resident-1";

        private readonly CustomWebApplicationFactory _factory;

        public ResidentsControllerSecurityIntegrationTests(CustomWebApplicationFactory factory)
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
        public async Task Anonymous_ResidentsIndex_RedirectsToLogin()
        {
            using var client = CreateClient();
            using var response = await client.GetAsync("/Residents");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Identity/Account/Login", response.Headers.Location?.AbsolutePath);
        }

        [Fact]
        public async Task Resident_ResidentsIndex_ReturnsForbidden()
        {
            using var client = CreateAuthenticatedClient(AppRoles.Resident, ResidentUserId);
            using var response = await client.GetAsync("/Residents");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task BuildingManager_ResidentsIndex_ReturnsSuccess()
        {
            using var client = CreateAuthenticatedClient(AppRoles.BuildingManager, ManagerUserId);
            using var response = await client.GetAsync("/Residents");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task SuperAdmin_ResidentsIndex_ReturnsSuccess()
        {
            using var client = CreateAuthenticatedClient(AppRoles.SuperAdmin, AdminUserId);
            using var response = await client.GetAsync("/Residents");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Resident_ResidentsCreate_ReturnsForbidden()
        {
            using var client = CreateAuthenticatedClient(AppRoles.Resident, ResidentUserId);
            using var response = await client.GetAsync("/Residents/Create");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task BuildingManager_ResidentsCreate_ReturnsSuccess()
        {
            using var client = CreateAuthenticatedClient(AppRoles.BuildingManager, ManagerUserId);
            using var response = await client.GetAsync("/Residents/Create");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
