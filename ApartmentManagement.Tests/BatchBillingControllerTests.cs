using System.Reflection;
using System.Security.Claims;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Controllers;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace ApartmentManagement.Tests;

public sealed class BatchBillingControllerTests
{
    private static ControllerContext CreateControllerContext(string? userId = "accountant-user")
    {
        var httpContext = new DefaultHttpContext();
        if (userId != null)
        {
            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, AppRoles.Accountant)
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

    private static (BillingController controller, Mock<IBillingService> mockBilling, Mock<IBuildingService> mockBuilding, Mock<IApartmentService> mockApartment)
        CreateTestHarness(string? userId = "accountant-user")
    {
        var mockBilling = new Mock<IBillingService>();
        var mockBuilding = new Mock<IBuildingService>();
        var mockApartment = new Mock<IApartmentService>();

        mockBuilding.Setup(b => b.GetAllBuildingsAsync(null))
            .ReturnsAsync([new Building { BuildingId = 1, BuildingCode = "B1", BuildingName = "Tòa B1" }]);
        mockApartment.Setup(a => a.GetAllApartmentsAsync(null, null))
            .ReturnsAsync([new Apartment { ApartmentId = 1, ApartmentCode = "101", Building = new Building { BuildingName = "Tòa B1" } }]);

        var controller = new BillingController(mockBilling.Object, mockBuilding.Object, mockApartment.Object)
        {
            ControllerContext = CreateControllerContext(userId),
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        return (controller, mockBilling, mockBuilding, mockApartment);
    }

    [Fact]
    public void BatchCreateInvoices_Post_HasValidateAntiForgeryTokenAndHttpPost()
    {
        var method = typeof(BillingController).GetMethod(
            nameof(BillingController.BatchCreateInvoices),
            BindingFlags.Public | BindingFlags.Instance,
            [typeof(BatchGenerateInvoicesViewModel)]);

        Assert.NotNull(method);
        Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public async Task BatchCreateInvoices_Get_ReturnsViewWithBuildingOptions()
    {
        var (controller, _, _, _) = CreateTestHarness();

        var result = await controller.BatchCreateInvoices(buildingId: 1, year: 2026, month: 8);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<BatchGenerateInvoicesViewModel>(viewResult.Model);
        Assert.Equal(1, model.BuildingId);
        Assert.Equal(2026, model.BillingYear);
        Assert.Equal(8, model.BillingMonth);
        Assert.NotEmpty(model.BuildingOptions);
    }

    [Fact]
    public async Task BatchCreateInvoices_Post_WhenUserIdNull_ReturnsForbid()
    {
        var (controller, _, _, _) = CreateTestHarness(userId: null);
        var model = new BatchGenerateInvoicesViewModel { BuildingId = 1 };

        var result = await controller.BatchCreateInvoices(model);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task BatchCreateInvoices_Post_WhenModelNull_ReturnsBadRequest()
    {
        var (controller, _, _, _) = CreateTestHarness();

        var result = await controller.BatchCreateInvoices((BatchGenerateInvoicesViewModel)null!);

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task BatchCreateInvoices_Post_WhenModelStateInvalid_ReturnsViewWithBuildings()
    {
        var (controller, _, _, _) = CreateTestHarness();
        controller.ModelState.AddModelError("BuildingId", "Vui lòng chọn tòa nhà.");
        var model = new BatchGenerateInvoicesViewModel { BuildingId = 0 };

        var result = await controller.BatchCreateInvoices(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Same(model, viewResult.Model);
        Assert.NotEmpty(model.BuildingOptions);
    }

    [Fact]
    public async Task BatchCreateInvoices_Post_WhenServiceFails_AddsModelErrorAndReturnsView()
    {
        var (controller, mockBilling, _, _) = CreateTestHarness();
        var model = new BatchGenerateInvoicesViewModel
        {
            BuildingId = 1,
            BillingYear = 2026,
            BillingMonth = 8,
            DueDate = new DateTime(2026, 9, 15)
        };

        mockBilling.Setup(b => b.GenerateBuildingDraftsAsync(1, 2026, 8, model.DueDate, "accountant-user"))
            .ReturnsAsync(new BatchBillingOperationResult(false, "Cần có biểu phí quản lý, điện và nước.", 0, 0, 0, 0, 0, []));

        var result = await controller.BatchCreateInvoices(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Same(model, viewResult.Model);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal("Cần có biểu phí quản lý, điện và nước.", controller.ModelState[string.Empty]?.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task BatchCreateInvoices_Post_WhenServiceSucceeds_PopulatesResultAndTempData()
    {
        var (controller, mockBilling, _, _) = CreateTestHarness();
        var model = new BatchGenerateInvoicesViewModel
        {
            BuildingId = 1,
            BillingYear = 2026,
            BillingMonth = 8,
            DueDate = new DateTime(2026, 9, 15)
        };

        var batchResult = new BatchBillingOperationResult(
            Success: true,
            ErrorMessage: null,
            TotalScanned: 2,
            CreatedCount: 2,
            AlreadyExistsCount: 0,
            SkippedCount: 0,
            FailedCount: 0,
            Items:
            [
                new BatchBillingItemResult(1, "A-101", BatchBillingItemStatus.Created, "Đã tạo", 101, "INV-01", 100000),
                new BatchBillingItemResult(2, "A-102", BatchBillingItemStatus.Created, "Đã tạo", 102, "INV-02", 200000)
            ]);

        mockBilling.Setup(b => b.GenerateBuildingDraftsAsync(1, 2026, 8, model.DueDate, "accountant-user"))
            .ReturnsAsync(batchResult);

        var result = await controller.BatchCreateInvoices(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Same(model, viewResult.Model);
        Assert.Same(batchResult, model.Result);
        Assert.NotNull(controller.TempData["SuccessMessage"]);
        Assert.Contains("2 tạo mới", (string)controller.TempData["SuccessMessage"]!);
    }
}

