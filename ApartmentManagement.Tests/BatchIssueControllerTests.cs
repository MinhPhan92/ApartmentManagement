using System.Reflection;
using System.Security.Claims;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Controllers;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace ApartmentManagement.Tests;

public sealed class BatchIssueControllerTests
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

        var controller = new BillingController(mockBilling.Object, mockBuilding.Object, mockApartment.Object)
        {
            ControllerContext = CreateControllerContext(userId),
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        return (controller, mockBilling, mockBuilding, mockApartment);
    }

    [Fact]
    public void BatchIssueInvoices_Post_HasValidateAntiForgeryTokenAndHttpPost()
    {
        var method = typeof(BillingController).GetMethod(
            nameof(BillingController.BatchIssueInvoices),
            BindingFlags.Public | BindingFlags.Instance,
            [typeof(BatchIssueInvoicesViewModel)]);

        Assert.NotNull(method);
        Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public async Task BatchIssueInvoices_Get_WhenBuildingSelected_LoadsDraftInvoices()
    {
        var (controller, mockBilling, _, _) = CreateTestHarness();

        mockBilling.Setup(s => s.GetDraftInvoicesForBatchIssueAsync(1, 2026, 8))
            .ReturnsAsync(
            [
                new DraftInvoiceDto(1, "INV-01", "101", "Resident 1", 200000, 100000, 50000, 350000, [1, 2], "AQI=")
            ]);

        var result = await controller.BatchIssueInvoices(buildingId: 1, year: 2026, month: 8);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<BatchIssueInvoicesViewModel>(viewResult.Model);

        Assert.Equal(1, model.BuildingId);
        Assert.Equal(2026, model.BillingYear);
        Assert.Equal(8, model.BillingMonth);
        Assert.Single(model.DraftInvoices);
        Assert.Equal("101", model.DraftInvoices[0].ApartmentCode);
        Assert.True(model.DraftInvoices[0].IsSelected);
    }

    [Fact]
    public async Task BatchIssueInvoices_Post_WhenUnauthenticated_ReturnsForbid()
    {
        var (controller, _, _, _) = CreateTestHarness(userId: null);
        var model = new BatchIssueInvoicesViewModel { BuildingId = 1 };

        var result = await controller.BatchIssueInvoices(model);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task BatchIssueInvoices_Post_WhenNoInvoicesSelected_ReturnsModelError()
    {
        var (controller, _, _, _) = CreateTestHarness();
        var model = new BatchIssueInvoicesViewModel
        {
            BuildingId = 1,
            BillingYear = 2026,
            BillingMonth = 8,
            DraftInvoices =
            [
                new BatchIssueItemViewModel { InvoiceId = 1, IsSelected = false }
            ]
        };

        var result = await controller.BatchIssueInvoices(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Same(model, viewResult.Model);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains("chọn ít nhất một hóa đơn", controller.ModelState[string.Empty]?.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task BatchIssueInvoices_Post_WhenSuccessful_PopulatesResultAndTempData()
    {
        var (controller, mockBilling, _, _) = CreateTestHarness();
        var model = new BatchIssueInvoicesViewModel
        {
            BuildingId = 1,
            BillingYear = 2026,
            BillingMonth = 8,
            DraftInvoices =
            [
                new BatchIssueItemViewModel
                {
                    InvoiceId = 10,
                    InvoiceCode = "INV-10",
                    ApartmentCode = "A-101",
                    IsSelected = true,
                    RowVersionToken = "AQID"
                }
            ]
        };

        var opResult = new BatchIssueOperationResult(
            Success: true,
            ErrorMessage: null,
            TotalSelected: 1,
            IssuedCount: 1,
            AlreadyIssuedCount: 0,
            FailedCount: 0,
            Items:
            [
                new BatchIssueItemResult(10, "INV-10", "A-101", BatchIssueItemStatus.Issued, "Thành công", 500000)
            ]);

        mockBilling.Setup(s => s.IssueBuildingInvoicesAsync(It.IsAny<BatchIssueInput>(), "accountant-user"))
            .ReturnsAsync(opResult);

        mockBilling.Setup(s => s.GetDraftInvoicesForBatchIssueAsync(1, 2026, 8))
            .ReturnsAsync([]);

        var result = await controller.BatchIssueInvoices(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Same(model, viewResult.Model);
        Assert.NotNull(controller.TempData["SuccessMessage"]);
        Assert.Contains("1 thành công", (string)controller.TempData["SuccessMessage"]!);
        Assert.Empty(model.DraftInvoices); // Đã reload danh sách rỗng sau khi phát hành
    }
}

