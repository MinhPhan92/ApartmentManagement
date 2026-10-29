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

public sealed class BatchMeterReadingControllerTests
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
    public void RecordBatchReadings_Post_HasValidateAntiForgeryTokenAndHttpPost()
    {
        var method = typeof(BillingController).GetMethod(
            nameof(BillingController.RecordBatchReadings),
            BindingFlags.Public | BindingFlags.Instance,
            [typeof(BatchMeterReadingViewModel)]);

        Assert.NotNull(method);
        Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public async Task RecordBatchReadings_Get_ReturnsViewWithBuildingOptions()
    {
        var (controller, mockBilling, _, _) = CreateTestHarness();

        mockBilling.Setup(b => b.GetBatchMeterReadingSheetAsync(1, 2026, 8))
            .ReturnsAsync(new BatchMeterReadingViewModel
            {
                BuildingId = 1,
                BillingYear = 2026,
                BillingMonth = 8,
                Rows = [new BatchMeterReadingRowItem { ApartmentId = 101, ApartmentCode = "A-101" }]
            });

        var result = await controller.RecordBatchReadings(buildingId: 1, year: 2026, month: 8);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<BatchMeterReadingViewModel>(viewResult.Model);
        Assert.Equal(1, model.BuildingId);
        Assert.Equal(2026, model.BillingYear);
        Assert.Equal(8, model.BillingMonth);
        Assert.Single(model.Rows);
        Assert.NotEmpty(model.BuildingOptions);
    }

    [Fact]
    public async Task RecordBatchReadings_Post_WhenUserIdNull_ReturnsForbid()
    {
        var (controller, _, _, _) = CreateTestHarness(userId: null);
        var model = new BatchMeterReadingViewModel { BuildingId = 1 };

        var result = await controller.RecordBatchReadings(model);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task RecordBatchReadings_Post_WhenModelNull_ReturnsBadRequest()
    {
        var (controller, _, _, _) = CreateTestHarness();

        var result = await controller.RecordBatchReadings((BatchMeterReadingViewModel)null!);

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task RecordBatchReadings_Post_WhenModelStateInvalid_ReturnsViewWithBuildings()
    {
        var (controller, _, _, _) = CreateTestHarness();
        controller.ModelState.AddModelError("BuildingId", "Vui lòng chọn tòa nhà.");
        var model = new BatchMeterReadingViewModel { BuildingId = 0 };

        var result = await controller.RecordBatchReadings(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Same(model, viewResult.Model);
        Assert.NotEmpty(model.BuildingOptions);
    }

    [Fact]
    public async Task RecordBatchReadings_Post_WhenServiceFails_AddsModelErrorAndReturnsView()
    {
        var (controller, mockBilling, _, _) = CreateTestHarness();
        var model = new BatchMeterReadingViewModel
        {
            BuildingId = 1,
            BillingYear = 2026,
            BillingMonth = 8,
            Rows = [new BatchMeterReadingRowItem { ApartmentId = 101, ApartmentCode = "A-101", ElectricityNewReading = 150 }]
        };

        mockBilling.Setup(b => b.SaveBatchMeterReadingsAsync(It.IsAny<BatchMeterReadingSaveInput>(), "accountant-user"))
            .ReturnsAsync(new BatchMeterReadingOperationResult(false, "Không thể lưu chỉ số.", 1, 0, 0, 1, []));

        var result = await controller.RecordBatchReadings(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Same(model, viewResult.Model);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal("Không thể lưu chỉ số.", controller.ModelState[string.Empty]?.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task RecordBatchReadings_Post_WhenServiceSucceeds_PopulatesResultAndTempData()
    {
        var (controller, mockBilling, _, _) = CreateTestHarness();
        var model = new BatchMeterReadingViewModel
        {
            BuildingId = 1,
            BillingYear = 2026,
            BillingMonth = 8,
            Rows = [new BatchMeterReadingRowItem { ApartmentId = 101, ApartmentCode = "A-101", ElectricityNewReading = 150 }]
        };

        var batchResult = new BatchMeterReadingOperationResult(
            Success: true,
            ErrorMessage: null,
            TotalProcessed: 1,
            SavedCount: 1,
            SkippedCount: 0,
            FailedCount: 0,
            Items: [new BatchMeterReadingItemResult(101, "A-101", true, "Đã lưu")]);

        mockBilling.Setup(b => b.SaveBatchMeterReadingsAsync(It.IsAny<BatchMeterReadingSaveInput>(), "accountant-user"))
            .ReturnsAsync(batchResult);

        mockBilling.Setup(b => b.GetBatchMeterReadingSheetAsync(1, 2026, 8))
            .ReturnsAsync(new BatchMeterReadingViewModel
            {
                BuildingId = 1,
                BillingYear = 2026,
                BillingMonth = 8,
                BuildingName = "Tòa B1",
                Rows = [new BatchMeterReadingRowItem { ApartmentId = 101, ApartmentCode = "A-101", ElectricityOldReading = 150, HasExistingReading = true }]
            });

        var result = await controller.RecordBatchReadings(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Same(model, viewResult.Model);
        Assert.Same(batchResult, model.Result);
        Assert.NotNull(controller.TempData["SuccessMessage"]);
        Assert.Contains("1 căn hộ", (string)controller.TempData["SuccessMessage"]!);
    }
}

