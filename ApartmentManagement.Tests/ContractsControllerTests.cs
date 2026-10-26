using System.Reflection;
using System.Security.Claims;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Controllers;
using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ApartmentManagement.Tests;

public sealed class ContractsControllerTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new ApplicationDbContext(options);

        var building = new Building
        {
            BuildingId = 1,
            BuildingCode = "B1",
            BuildingName = "Tòa B1",
            NumberOfFloors = 5
        };
        var apartment = new Apartment
        {
            ApartmentId = 1,
            ApartmentCode = "101",
            BuildingId = 1,
            Building = building,
            Floor = 1,
            Area = 50,
            Status = "Trống"
        };
        var user1 = new ApplicationUser
        {
            Id = "u1",
            FullName = "Nguyễn Văn A",
            IsActive = true
        };
        var user2 = new ApplicationUser
        {
            Id = "u2",
            FullName = "Trần Thị B",
            IsActive = true
        };
        var resident1 = new Resident
        {
            ResidentId = 1,
            UserId = "u1",
            User = user1,
            CitizenId = "000000000001"
        };
        var resident2 = new Resident
        {
            ResidentId = 2,
            UserId = "u2",
            User = user2,
            CitizenId = "000000000002"
        };

        context.AddRange(building, apartment, user1, user2, resident1, resident2);
        context.SaveChanges();
        return context;
    }

    private static ControllerContext CreateControllerContext(string? userId = "superadmin-user")
    {
        var httpContext = new DefaultHttpContext();
        if (userId != null)
        {
            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, AppRoles.SuperAdmin)
            ], "TestAuth");
            httpContext.User = new ClaimsPrincipal(identity);
        }
        else
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        }

        return new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    private static ApartmentContract CreateTestContract(
        int id = 1,
        string code = "HD-001",
        ApartmentContractStatus status = ApartmentContractStatus.Draft)
    {
        var contract = new ApartmentContract
        {
            ApartmentContractId = id,
            ApartmentId = 1,
            StartDate = new DateTime(2025, 1, 1),
            EndDate = new DateTime(2025, 12, 31),
            MonthlyRent = 10000000m,
            DepositAmount = 20000000m,
            Status = status,
            RowVersion = [1, 2, 3, 4],
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = "superadmin-user",
            Apartment = new Apartment
            {
                ApartmentId = 1,
                ApartmentCode = "101",
                Building = new Building { BuildingId = 1, BuildingName = "Tòa B1" }
            },
            CreatedByUser = new ApplicationUser { FullName = "Admin User" },
            Parties =
            [
                new ContractParty
                {
                    ApartmentContractId = id,
                    ResidentId = 1,
                    Role = ContractPartyRole.Lessor,
                    Resident = new Resident
                    {
                        ResidentId = 1,
                        CitizenId = "000000000001",
                        User = new ApplicationUser { FullName = "Nguyễn Văn A" }
                    }
                },
                new ContractParty
                {
                    ApartmentContractId = id,
                    ResidentId = 2,
                    Role = ContractPartyRole.Lessee,
                    Resident = new Resident
                    {
                        ResidentId = 2,
                        CitizenId = "000000000002",
                        User = new ApplicationUser { FullName = "Trần Thị B" }
                    }
                }
            ],
            StatusHistory =
            [
                new ContractStatusHistory
                {
                    PreviousStatus = null,
                    NewStatus = ApartmentContractStatus.Draft,
                    ChangedAtUtc = DateTime.UtcNow,
                    Reason = "Khởi tạo",
                    ChangedByUser = new ApplicationUser { FullName = "Admin User" }
                }
            ]
        };

        typeof(ApartmentContract).GetProperty(nameof(ApartmentContract.ContractCode))!
            .SetValue(contract, code);
        return contract;
    }

    #region Security & Policy Attribute Tests

    [Fact]
    public void ContractsController_HasRequireSuperAdminPolicy()
    {
        var authAttr = typeof(ContractsController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.Equal(AppPolicies.RequireSuperAdmin, authAttr.Policy);
    }

    [Theory]
    [InlineData(nameof(ContractsController.Create), typeof(ContractEditViewModel))]
    [InlineData(nameof(ContractsController.Edit), typeof(int), typeof(ContractEditViewModel))]
    [InlineData(nameof(ContractsController.Activate), typeof(int), typeof(ContractActionViewModel))]
    [InlineData(nameof(ContractsController.Cancel), typeof(int), typeof(ContractActionViewModel))]
    [InlineData(nameof(ContractsController.Complete), typeof(int), typeof(ContractActionViewModel))]
    [InlineData(nameof(ContractsController.Terminate), typeof(int), typeof(ContractActionViewModel))]
    public void ContractsController_ModifyingActions_HaveValidateAntiForgeryTokenAndHttpPost(
        string actionName,
        params Type[] parameterTypes)
    {
        var method = typeof(ContractsController).GetMethod(actionName, parameterTypes);
        Assert.NotNull(method);
        Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public void MyContractsController_HasRequireResidentPolicy()
    {
        var authAttr = typeof(MyContractsController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.Equal(AppPolicies.RequireResident, authAttr.Policy);
    }

    #endregion

    #region ContractsController.Index Tests

    [Fact]
    public async Task Index_ReturnsViewResult_WithContractListViewModel()
    {
        using var context = CreateInMemoryDbContext();
        var mockService = new Mock<IContractService>();
        mockService.Setup(s => s.GetPageAsync(It.IsAny<string>(), It.IsAny<ApartmentContractStatus?>(), 1, 10))
            .ReturnsAsync(new ContractPageResult([], 1, 10, 0));

        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext()
        };

        var result = await controller.Index(null, null, 1);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ContractListViewModel>(viewResult.Model);
        Assert.NotNull(model.PageResult);
        Assert.NotNull(model.StatusOptions);
    }

    #endregion

    #region ContractsController.Details Tests

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Details_ReturnsNotFound_WhenIdIsZeroOrNegative(int id)
    {
        using var context = CreateInMemoryDbContext();
        var mockService = new Mock<IContractService>();
        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext()
        };

        var result = await controller.Details(id);

        Assert.IsType<NotFoundResult>(result);
        mockService.Verify(s => s.GetByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Details_ReturnsNotFound_WhenContractDoesNotExist()
    {
        using var context = CreateInMemoryDbContext();
        var mockService = new Mock<IContractService>();
        mockService.Setup(s => s.GetByIdAsync(999)).ReturnsAsync((ApartmentContract?)null);

        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext()
        };

        var result = await controller.Details(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Details_ReturnsViewResult_WhenContractExists()
    {
        using var context = CreateInMemoryDbContext();
        var contract = CreateTestContract(1, "HD-001");
        var mockService = new Mock<IContractService>();
        mockService.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(contract);

        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext()
        };

        var result = await controller.Details(1);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ContractDetailsViewModel>(viewResult.Model);
        Assert.Equal(1, model.Id);
        Assert.Equal("HD-001", model.ContractCode);
        Assert.Equal(2, model.Parties.Count);
    }

    #endregion

    #region ContractsController.Create Tests

    [Fact]
    public async Task Create_Get_ReturnsViewResult_WithPopulatedOptions()
    {
        using var context = CreateInMemoryDbContext();
        var mockService = new Mock<IContractService>();
        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext()
        };

        var result = await controller.Create();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Edit", viewResult.ViewName);
        var model = Assert.IsType<ContractEditViewModel>(viewResult.Model);
        Assert.NotEmpty(model.ApartmentOptions);
        Assert.NotEmpty(model.ResidentOptions);
    }

    [Fact]
    public async Task Create_Post_ReturnsBadRequest_WhenModelIsNull()
    {
        using var context = CreateInMemoryDbContext();
        var mockService = new Mock<IContractService>();
        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext()
        };

        var result = await controller.Create(null!);

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task Create_Post_ReturnsForbid_WhenUserUnauthenticated()
    {
        using var context = CreateInMemoryDbContext();
        var mockService = new Mock<IContractService>();
        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext(userId: null)
        };

        var model = new ContractEditViewModel { ContractCode = "HD-NEW" };
        var result = await controller.Create(model);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Create_Post_ReturnsViewResult_WhenLessorAndLesseeAreSame()
    {
        using var context = CreateInMemoryDbContext();
        var mockService = new Mock<IContractService>();
        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext()
        };

        var model = new ContractEditViewModel
        {
            ContractCode = "HD-NEW",
            ApartmentId = 1,
            LessorResidentId = 1,
            LesseeResidentId = 1,
            StartDate = DateTime.UtcNow,
            MonthlyRent = 1000m
        };

        var result = await controller.Create(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Edit", viewResult.ViewName);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey(nameof(model.LesseeResidentId)));
        Assert.NotEmpty(model.ApartmentOptions);
    }

    [Fact]
    public async Task Create_Post_ReturnsViewResult_WhenServiceFails()
    {
        using var context = CreateInMemoryDbContext();
        var mockService = new Mock<IContractService>();
        mockService.Setup(s => s.CreateDraftAsync(It.IsAny<ContractDraftInput>(), "superadmin-user"))
            .ReturnsAsync(new ContractServiceResult(false, "Mã hợp đồng đã tồn tại.", null));

        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext()
        };

        var model = new ContractEditViewModel
        {
            ContractCode = "HD-DUP",
            ApartmentId = 1,
            LessorResidentId = 1,
            LesseeResidentId = 2,
            StartDate = DateTime.UtcNow,
            MonthlyRent = 1000m
        };

        var result = await controller.Create(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Edit", viewResult.ViewName);
        Assert.False(controller.ModelState.IsValid);
        Assert.NotEmpty(model.ApartmentOptions);
    }

    [Fact]
    public async Task Create_Post_RedirectsToDetails_WhenSuccessful()
    {
        using var context = CreateInMemoryDbContext();
        var contract = CreateTestContract(10, "HD-10");
        var mockService = new Mock<IContractService>();
        mockService.Setup(s => s.CreateDraftAsync(It.IsAny<ContractDraftInput>(), "superadmin-user"))
            .ReturnsAsync(new ContractServiceResult(true, null, contract));

        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext(),
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        var model = new ContractEditViewModel
        {
            ContractCode = "HD-10",
            ApartmentId = 1,
            LessorResidentId = 1,
            LesseeResidentId = 2,
            StartDate = DateTime.UtcNow,
            MonthlyRent = 1000m
        };

        var result = await controller.Create(model);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ContractsController.Details), redirect.ActionName);
        Assert.Equal(10, redirect.RouteValues?["id"]);
        Assert.Equal("Đã tạo hợp đồng ở trạng thái nháp.", controller.TempData["SuccessMessage"]);
    }

    #endregion

    #region ContractsController.Edit Tests

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Edit_Get_ReturnsNotFound_WhenIdIsZeroOrNegative(int id)
    {
        using var context = CreateInMemoryDbContext();
        var mockService = new Mock<IContractService>();
        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext()
        };

        var result = await controller.Edit(id);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_Get_ReturnsNotFound_WhenContractDoesNotExist()
    {
        using var context = CreateInMemoryDbContext();
        var mockService = new Mock<IContractService>();
        mockService.Setup(s => s.GetByIdAsync(999)).ReturnsAsync((ApartmentContract?)null);

        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext()
        };

        var result = await controller.Edit(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_Get_RedirectsToDetails_WhenContractIsNotDraft()
    {
        using var context = CreateInMemoryDbContext();
        var contract = CreateTestContract(1, "HD-001", ApartmentContractStatus.Active);
        var mockService = new Mock<IContractService>();
        mockService.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(contract);

        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext(),
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        var result = await controller.Edit(1);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ContractsController.Details), redirect.ActionName);
        Assert.Equal("Chỉ có thể chỉnh sửa hợp đồng ở trạng thái nháp.", controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task Edit_Get_ReturnsViewResult_WhenContractIsDraft()
    {
        using var context = CreateInMemoryDbContext();
        var contract = CreateTestContract(1, "HD-001", ApartmentContractStatus.Draft);
        var mockService = new Mock<IContractService>();
        mockService.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(contract);

        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext()
        };

        var result = await controller.Edit(1);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ContractEditViewModel>(viewResult.Model);
        Assert.Equal("HD-001", model.ContractCode);
        Assert.NotEmpty(model.ApartmentOptions);
        Assert.NotEmpty(model.ResidentOptions);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Edit_Post_ReturnsNotFound_WhenIdIsZeroOrNegative(int id)
    {
        using var context = CreateInMemoryDbContext();
        var mockService = new Mock<IContractService>();
        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext()
        };

        var result = await controller.Edit(id, new ContractEditViewModel());

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_Post_ReturnsNotFound_WhenModelIsNull()
    {
        using var context = CreateInMemoryDbContext();
        var mockService = new Mock<IContractService>();
        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext()
        };

        var result = await controller.Edit(1, null!);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_Post_ReturnsForbid_WhenUserUnauthenticated()
    {
        using var context = CreateInMemoryDbContext();
        var mockService = new Mock<IContractService>();
        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext(userId: null)
        };

        var result = await controller.Edit(1, new ContractEditViewModel());

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Edit_Post_ReturnsViewResult_WhenRowVersionTokenIsInvalid()
    {
        using var context = CreateInMemoryDbContext();
        var mockService = new Mock<IContractService>();
        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext()
        };

        var model = new ContractEditViewModel
        {
            ContractCode = "HD-001",
            ApartmentId = 1,
            LessorResidentId = 1,
            LesseeResidentId = 2,
            StartDate = DateTime.UtcNow,
            MonthlyRent = 1000m,
            RowVersionToken = "not-valid-base64!!"
        };

        var result = await controller.Edit(1, model);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey(nameof(model.RowVersionToken)));
        Assert.NotEmpty(model.ApartmentOptions);
    }

    [Fact]
    public async Task Edit_Post_ReturnsNotFound_WhenServiceReportsContractNotFound()
    {
        using var context = CreateInMemoryDbContext();
        var mockService = new Mock<IContractService>();
        mockService.Setup(s => s.UpdateDraftAsync(1, It.IsAny<ContractDraftInput>(), "superadmin-user", It.IsAny<byte[]>()))
            .ReturnsAsync(new ContractServiceResult(false, "Không tìm thấy hợp đồng.", null));

        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext()
        };

        var model = new ContractEditViewModel
        {
            ContractCode = "HD-001",
            ApartmentId = 1,
            LessorResidentId = 1,
            LesseeResidentId = 2,
            StartDate = DateTime.UtcNow,
            MonthlyRent = 1000m,
            RowVersionToken = Convert.ToBase64String(new byte[] { 1, 2, 3 })
        };

        var result = await controller.Edit(1, model);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_Post_ReturnsViewResult_WhenServiceFailsWithOtherError()
    {
        using var context = CreateInMemoryDbContext();
        var mockService = new Mock<IContractService>();
        mockService.Setup(s => s.UpdateDraftAsync(1, It.IsAny<ContractDraftInput>(), "superadmin-user", It.IsAny<byte[]>()))
            .ReturnsAsync(new ContractServiceResult(false, "Mã hợp đồng đã tồn tại.", null));

        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext()
        };

        var model = new ContractEditViewModel
        {
            ContractCode = "HD-001",
            ApartmentId = 1,
            LessorResidentId = 1,
            LesseeResidentId = 2,
            StartDate = DateTime.UtcNow,
            MonthlyRent = 1000m,
            RowVersionToken = Convert.ToBase64String(new byte[] { 1, 2, 3 })
        };

        var result = await controller.Edit(1, model);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.NotEmpty(model.ApartmentOptions);
    }

    [Fact]
    public async Task Edit_Post_RedirectsToDetails_WhenSuccessful()
    {
        using var context = CreateInMemoryDbContext();
        var contract = CreateTestContract(1, "HD-001");
        var mockService = new Mock<IContractService>();
        mockService.Setup(s => s.UpdateDraftAsync(1, It.IsAny<ContractDraftInput>(), "superadmin-user", It.IsAny<byte[]>()))
            .ReturnsAsync(new ContractServiceResult(true, null, contract));

        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext(),
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        var model = new ContractEditViewModel
        {
            ContractCode = "HD-001",
            ApartmentId = 1,
            LessorResidentId = 1,
            LesseeResidentId = 2,
            StartDate = DateTime.UtcNow,
            MonthlyRent = 1000m,
            RowVersionToken = Convert.ToBase64String(new byte[] { 1, 2, 3 })
        };

        var result = await controller.Edit(1, model);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ContractsController.Details), redirect.ActionName);
        Assert.Equal("Đã cập nhật hợp đồng nháp.", controller.TempData["SuccessMessage"]);
    }

    #endregion

    #region ContractsController Status Transition Tests

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Activate_ReturnsNotFound_WhenIdIsZeroOrNegative(int id)
    {
        using var context = CreateInMemoryDbContext();
        var mockService = new Mock<IContractService>();
        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext()
        };

        var result = await controller.Activate(id, new ContractActionViewModel { RowVersionToken = "AQID" });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Activate_ReturnsNotFound_WhenContractDoesNotExist()
    {
        using var context = CreateInMemoryDbContext();
        var mockService = new Mock<IContractService>();
        mockService.Setup(s => s.ActivateAsync(999, "superadmin-user", It.IsAny<byte[]>()))
            .ReturnsAsync(new ContractServiceResult(false, "Không tìm thấy hợp đồng.", null));

        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext(),
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        var result = await controller.Activate(999, new ContractActionViewModel { RowVersionToken = "AQID" });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Activate_RedirectsToDetails_WithErrorMessage_WhenRowVersionInvalid()
    {
        using var context = CreateInMemoryDbContext();
        var mockService = new Mock<IContractService>();
        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext(),
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        var result = await controller.Activate(1, new ContractActionViewModel { RowVersionToken = "invalid!!" });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ContractsController.Details), redirect.ActionName);
        Assert.Equal("Phiên bản hợp đồng không hợp lệ. Vui lòng tải lại dữ liệu.", controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task Activate_RedirectsToDetails_WithSuccessMessage_WhenSuccessful()
    {
        using var context = CreateInMemoryDbContext();
        var contract = CreateTestContract(1, "HD-001", ApartmentContractStatus.Active);
        var mockService = new Mock<IContractService>();
        mockService.Setup(s => s.ActivateAsync(1, "superadmin-user", It.IsAny<byte[]>()))
            .ReturnsAsync(new ContractServiceResult(true, null, contract));

        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext(),
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        var result = await controller.Activate(1, new ContractActionViewModel { RowVersionToken = "AQID" });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ContractsController.Details), redirect.ActionName);
        Assert.Equal("Đã cập nhật trạng thái hợp đồng.", controller.TempData["SuccessMessage"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Cancel_ReturnsNotFound_WhenIdIsZeroOrNegative(int id)
    {
        using var context = CreateInMemoryDbContext();
        var mockService = new Mock<IContractService>();
        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext()
        };

        var result = await controller.Cancel(id, new ContractActionViewModel { Reason = "Lý do", RowVersionToken = "AQID" });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Cancel_ReturnsDetailsViewWithActionErrors_WhenReasonIsEmpty()
    {
        using var context = CreateInMemoryDbContext();
        var contract = CreateTestContract(1, "HD-001");
        var mockService = new Mock<IContractService>();
        mockService.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(contract);

        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext()
        };

        var model = new ContractActionViewModel { Reason = "", RowVersionToken = "AQID" };
        var result = await controller.Cancel(1, model);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.NotNull(viewResult.ViewData["ActionErrors"]);
        Assert.Same(model, viewResult.ViewData["ActionModel"]);
    }

    [Fact]
    public async Task Cancel_ReturnsNotFound_WhenReasonIsEmptyAndContractDoesNotExist()
    {
        using var context = CreateInMemoryDbContext();
        var mockService = new Mock<IContractService>();
        mockService.Setup(s => s.GetByIdAsync(999)).ReturnsAsync((ApartmentContract?)null);

        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext()
        };

        var result = await controller.Cancel(999, new ContractActionViewModel { Reason = "", RowVersionToken = "AQID" });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Cancel_RedirectsToDetails_WithSuccessMessage_WhenSuccessful()
    {
        using var context = CreateInMemoryDbContext();
        var contract = CreateTestContract(1, "HD-001", ApartmentContractStatus.Cancelled);
        var mockService = new Mock<IContractService>();
        mockService.Setup(s => s.CancelAsync(1, "superadmin-user", It.IsAny<byte[]>(), "Khách đổi ý"))
            .ReturnsAsync(new ContractServiceResult(true, null, contract));

        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext(),
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        var result = await controller.Cancel(1, new ContractActionViewModel { Reason = "Khách đổi ý", RowVersionToken = "AQID" });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ContractsController.Details), redirect.ActionName);
        Assert.Equal("Đã cập nhật trạng thái hợp đồng.", controller.TempData["SuccessMessage"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Complete_ReturnsNotFound_WhenIdIsZeroOrNegative(int id)
    {
        using var context = CreateInMemoryDbContext();
        var mockService = new Mock<IContractService>();
        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext()
        };

        var result = await controller.Complete(id, new ContractActionViewModel { RowVersionToken = "AQID" });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Complete_RedirectsToDetails_WithSuccessMessage_WhenSuccessful()
    {
        using var context = CreateInMemoryDbContext();
        var contract = CreateTestContract(1, "HD-001", ApartmentContractStatus.Completed);
        var mockService = new Mock<IContractService>();
        mockService.Setup(s => s.CompleteAsync(1, "superadmin-user", It.IsAny<byte[]>(), null))
            .ReturnsAsync(new ContractServiceResult(true, null, contract));

        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext(),
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        var result = await controller.Complete(1, new ContractActionViewModel { RowVersionToken = "AQID" });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ContractsController.Details), redirect.ActionName);
        Assert.Equal("Đã cập nhật trạng thái hợp đồng.", controller.TempData["SuccessMessage"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Terminate_ReturnsNotFound_WhenIdIsZeroOrNegative(int id)
    {
        using var context = CreateInMemoryDbContext();
        var mockService = new Mock<IContractService>();
        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext()
        };

        var result = await controller.Terminate(id, new ContractActionViewModel { Reason = "Lý do", RowVersionToken = "AQID" });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Terminate_ReturnsDetailsViewWithActionErrors_WhenReasonIsEmpty()
    {
        using var context = CreateInMemoryDbContext();
        var contract = CreateTestContract(1, "HD-001", ApartmentContractStatus.Active);
        var mockService = new Mock<IContractService>();
        mockService.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(contract);

        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext()
        };

        var model = new ContractActionViewModel { Reason = "", RowVersionToken = "AQID" };
        var result = await controller.Terminate(1, model);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.NotNull(viewResult.ViewData["ActionErrors"]);
        Assert.Same(model, viewResult.ViewData["ActionModel"]);
    }

    [Fact]
    public async Task Terminate_RedirectsToDetails_WithSuccessMessage_WhenSuccessful()
    {
        using var context = CreateInMemoryDbContext();
        var contract = CreateTestContract(1, "HD-001", ApartmentContractStatus.Terminated);
        var mockService = new Mock<IContractService>();
        mockService.Setup(s => s.TerminateAsync(1, "superadmin-user", It.IsAny<byte[]>(), "Vi phạm quy chế"))
            .ReturnsAsync(new ContractServiceResult(true, null, contract));

        var controller = new ContractsController(mockService.Object, context)
        {
            ControllerContext = CreateControllerContext(),
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        var result = await controller.Terminate(1, new ContractActionViewModel { Reason = "Vi phạm quy chế", RowVersionToken = "AQID" });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ContractsController.Details), redirect.ActionName);
        Assert.Equal("Đã cập nhật trạng thái hợp đồng.", controller.TempData["SuccessMessage"]);
    }

    #endregion

    #region MyContractsController Tests

    [Fact]
    public async Task MyContracts_Index_ReturnsForbid_WhenUserUnauthenticated()
    {
        var mockService = new Mock<IContractService>();
        var controller = new MyContractsController(mockService.Object)
        {
            ControllerContext = CreateControllerContext(userId: null)
        };

        var result = await controller.Index();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task MyContracts_Index_ReturnsViewResult_WithResidentContracts()
    {
        var contract = CreateTestContract(1, "HD-RES-1");
        var mockService = new Mock<IContractService>();
        mockService.Setup(s => s.GetForResidentUserAsync("resident-user"))
            .ReturnsAsync([contract]);

        var controller = new MyContractsController(mockService.Object)
        {
            ControllerContext = CreateControllerContext(userId: "resident-user")
        };

        var result = await controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ResidentContractListViewModel>(viewResult.Model);
        Assert.Single(model.Contracts);
        Assert.Equal("HD-RES-1", model.Contracts[0].ContractCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task MyContracts_Details_ReturnsNotFound_WhenIdIsZeroOrNegative(int id)
    {
        var mockService = new Mock<IContractService>();
        var controller = new MyContractsController(mockService.Object)
        {
            ControllerContext = CreateControllerContext(userId: "resident-user")
        };

        var result = await controller.Details(id);

        Assert.IsType<NotFoundResult>(result);
        mockService.Verify(s => s.GetForResidentUserAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task MyContracts_Details_ReturnsForbid_WhenUserUnauthenticated()
    {
        var mockService = new Mock<IContractService>();
        var controller = new MyContractsController(mockService.Object)
        {
            ControllerContext = CreateControllerContext(userId: null)
        };

        var result = await controller.Details(1);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task MyContracts_Details_ReturnsNotFound_WhenContractNotBelongingToResident()
    {
        var contract = CreateTestContract(1, "HD-RES-1");
        var mockService = new Mock<IContractService>();
        mockService.Setup(s => s.GetForResidentUserAsync("resident-user"))
            .ReturnsAsync([contract]);

        var controller = new MyContractsController(mockService.Object)
        {
            ControllerContext = CreateControllerContext(userId: "resident-user")
        };

        // User asks for contract 999 which does not belong to them
        var result = await controller.Details(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task MyContracts_Details_ReturnsViewResult_WhenContractBelongsToResident()
    {
        var contract = CreateTestContract(1, "HD-RES-1");
        var mockService = new Mock<IContractService>();
        mockService.Setup(s => s.GetForResidentUserAsync("resident-user"))
            .ReturnsAsync([contract]);

        var controller = new MyContractsController(mockService.Object)
        {
            ControllerContext = CreateControllerContext(userId: "resident-user")
        };

        var result = await controller.Details(1);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ResidentContractDetailsViewModel>(viewResult.Model);
        Assert.Equal("HD-RES-1", model.ContractCode);
    }

    #endregion
}
