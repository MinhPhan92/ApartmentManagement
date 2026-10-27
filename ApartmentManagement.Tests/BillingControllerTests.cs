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

public sealed class BillingControllerTests
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

    #region Security & Policy Attribute Tests

    [Fact]
    public void BillingController_HasRequireAccountantOrManagerPolicy()
    {
        var authAttr = typeof(BillingController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.Equal(AppPolicies.RequireAccountantOrManager, authAttr.Policy);
    }

    [Theory]
    [InlineData(nameof(BillingController.CreateTariff), typeof(FeeTariffEditViewModel))]
    [InlineData(nameof(BillingController.RecordReading), typeof(MeterReadingEditViewModel))]
    [InlineData(nameof(BillingController.CreateInvoice), typeof(GenerateInvoiceViewModel))]
    [InlineData(nameof(BillingController.Issue), typeof(IssueInvoiceViewModel))]
    [InlineData(nameof(BillingController.Void), typeof(VoidInvoiceViewModel))]
    [InlineData(nameof(BillingController.CloseTariff), typeof(CloseTariffViewModel))]
    public void BillingController_ModifyingActions_HaveValidateAntiForgeryTokenAndHttpPost(
        string actionName,
        params Type[] parameterTypes)
    {
        var method = typeof(BillingController).GetMethod(actionName, parameterTypes);
        Assert.NotNull(method);
        Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    #endregion

    #region Index Tests

    [Fact]
    public async Task Index_ReturnsViewResult_WithBillingIndexViewModel()
    {
        var (controller, mockBilling, _, _) = CreateTestHarness();
        mockBilling.Setup(s => s.GetInvoicesAsync(It.IsAny<InvoiceFilterCriteria>())).ReturnsAsync([]);
        mockBilling.Setup(s => s.GetTariffsAsync(null)).ReturnsAsync([]);
        mockBilling.Setup(s => s.GetBillingSummaryAsync(It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new BillingSummaryDto(0, 0, 0, 0, 0, 0, 0, 0, 0));

        var result = await controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<BillingIndexViewModel>(viewResult.Model);
        Assert.NotNull(model.Invoices);
        Assert.NotNull(model.Tariffs);
    }


    #endregion

    #region CreateTariff Tests

    [Fact]
    public async Task CreateTariff_Get_ReturnsViewResult_WithBuildingOptions()
    {
        var (controller, _, mockBuilding, _) = CreateTestHarness();

        var result = await controller.CreateTariff();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<FeeTariffEditViewModel>(viewResult.Model);
        Assert.NotEmpty(model.BuildingOptions);
    }

    [Fact]
    public async Task CreateTariff_Post_ReturnsBadRequest_WhenModelIsNull()
    {
        var (controller, _, _, _) = CreateTestHarness();

        var result = await controller.CreateTariff(null!);

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task CreateTariff_Post_ReturnsForbid_WhenUserUnauthenticated()
    {
        var (controller, _, _, _) = CreateTestHarness(userId: null);

        var result = await controller.CreateTariff(new FeeTariffEditViewModel());

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task CreateTariff_Post_ReturnsViewResult_WhenTierParsingFailsForUtility()
    {
        var (controller, _, _, _) = CreateTestHarness();
        var model = new FeeTariffEditViewModel
        {
            BuildingId = 1,
            ChargeType = FeeChargeType.Electricity,
            TierDefinitions = "invalid-tier-format"
        };

        var result = await controller.CreateTariff(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey(nameof(model.TierDefinitions)));
        Assert.NotEmpty(model.BuildingOptions);
    }

    [Fact]
    public async Task CreateTariff_Post_ReturnsViewResult_WhenServiceFails()
    {
        var (controller, mockBilling, _, _) = CreateTestHarness();
        mockBilling.Setup(s => s.CreateTariffAsync(It.IsAny<FeeTariffInput>(), "accountant-user"))
            .ReturnsAsync(new BillingOperationResult(false, "Thời hạn biểu phí bị trùng với cấu hình hiện có.", null));

        var model = new FeeTariffEditViewModel
        {
            BuildingId = 1,
            ChargeType = FeeChargeType.Management,
            MonthlyRatePerSquareMeter = 1000m
        };

        var result = await controller.CreateTariff(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.NotEmpty(model.BuildingOptions);
    }

    [Fact]
    public async Task CreateTariff_Post_RedirectsToIndex_WhenSuccessful()
    {
        var (controller, mockBilling, _, _) = CreateTestHarness();
        mockBilling.Setup(s => s.CreateTariffAsync(It.IsAny<FeeTariffInput>(), "accountant-user"))
            .ReturnsAsync(new BillingOperationResult(true, null, null));

        var model = new FeeTariffEditViewModel
        {
            BuildingId = 1,
            ChargeType = FeeChargeType.Management,
            MonthlyRatePerSquareMeter = 1000m
        };

        var result = await controller.CreateTariff(model);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(BillingController.Index), redirect.ActionName);
        Assert.Equal("Đã thêm biểu phí.", controller.TempData["SuccessMessage"]);
    }

    #endregion

    #region RecordReading Tests

    [Fact]
    public async Task RecordReading_Get_ReturnsViewResult_WithApartmentOptions()
    {
        var (controller, _, _, _) = CreateTestHarness();

        var result = await controller.RecordReading();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MeterReadingEditViewModel>(viewResult.Model);
        Assert.NotEmpty(model.ApartmentOptions);
    }

    [Fact]
    public async Task RecordReading_Post_ReturnsBadRequest_WhenModelIsNull()
    {
        var (controller, _, _, _) = CreateTestHarness();

        var result = await controller.RecordReading(null!);

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task RecordReading_Post_ReturnsForbid_WhenUserUnauthenticated()
    {
        var (controller, _, _, _) = CreateTestHarness(userId: null);

        var result = await controller.RecordReading(new MeterReadingEditViewModel());

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task RecordReading_Post_ReturnsViewResult_WhenServiceFails()
    {
        var (controller, mockBilling, _, _) = CreateTestHarness();
        mockBilling.Setup(s => s.RecordReadingAsync(It.IsAny<MeterReadingInput>(), "accountant-user"))
            .ReturnsAsync(new BillingOperationResult(false, "Chỉ số đầu kỳ phải khớp với chỉ số cuối kỳ gần nhất đã ghi.", null));

        var model = new MeterReadingEditViewModel
        {
            ApartmentId = 1,
            UtilityType = FeeChargeType.Electricity,
            BillingYear = 2026,
            BillingMonth = 9,
            PreviousReading = 60,
            CurrentReading = 80
        };

        var result = await controller.RecordReading(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.NotEmpty(model.ApartmentOptions);
    }

    [Fact]
    public async Task RecordReading_Post_RedirectsToIndex_WhenSuccessful()
    {
        var (controller, mockBilling, _, _) = CreateTestHarness();
        mockBilling.Setup(s => s.RecordReadingAsync(It.IsAny<MeterReadingInput>(), "accountant-user"))
            .ReturnsAsync(new BillingOperationResult(true, null, null));

        var model = new MeterReadingEditViewModel
        {
            ApartmentId = 1,
            UtilityType = FeeChargeType.Electricity,
            BillingYear = 2026,
            BillingMonth = 9,
            PreviousReading = 60,
            CurrentReading = 80
        };

        var result = await controller.RecordReading(model);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(BillingController.Index), redirect.ActionName);
        Assert.Equal("Đã lưu chỉ số công tơ.", controller.TempData["SuccessMessage"]);
    }

    #endregion

    #region CreateInvoice Tests

    [Fact]
    public async Task CreateInvoice_Get_ReturnsViewResult_WithApartmentOptions()
    {
        var (controller, _, _, _) = CreateTestHarness();

        var result = await controller.CreateInvoice();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<GenerateInvoiceViewModel>(viewResult.Model);
        Assert.NotEmpty(model.ApartmentOptions);
    }

    [Fact]
    public async Task CreateInvoice_Post_ReturnsBadRequest_WhenModelIsNull()
    {
        var (controller, _, _, _) = CreateTestHarness();

        var result = await controller.CreateInvoice(null!);

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task CreateInvoice_Post_ReturnsForbid_WhenUserUnauthenticated()
    {
        var (controller, _, _, _) = CreateTestHarness(userId: null);

        var result = await controller.CreateInvoice(new GenerateInvoiceViewModel());

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task CreateInvoice_Post_ReturnsViewResult_WhenServiceFails()
    {
        var (controller, mockBilling, _, _) = CreateTestHarness();
        mockBilling.Setup(s => s.GenerateDraftAsync(1, 2026, 8, It.IsAny<DateTime>(), "accountant-user"))
            .ReturnsAsync(new BillingOperationResult(false, "Căn hộ đã có hóa đơn trong kỳ này.", null));

        var model = new GenerateInvoiceViewModel
        {
            ApartmentId = 1,
            BillingYear = 2026,
            BillingMonth = 8,
            DueDate = new DateTime(2026, 9, 10)
        };

        var result = await controller.CreateInvoice(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.NotEmpty(model.ApartmentOptions);
    }

    [Fact]
    public async Task CreateInvoice_Post_RedirectsToDetails_WhenSuccessful()
    {
        var (controller, mockBilling, _, _) = CreateTestHarness();
        var invoice = new ApartmentInvoice { ApartmentInvoiceId = 42 };
        mockBilling.Setup(s => s.GenerateDraftAsync(1, 2026, 8, It.IsAny<DateTime>(), "accountant-user"))
            .ReturnsAsync(new BillingOperationResult(true, null, invoice));

        var model = new GenerateInvoiceViewModel
        {
            ApartmentId = 1,
            BillingYear = 2026,
            BillingMonth = 8,
            DueDate = new DateTime(2026, 9, 10)
        };

        var result = await controller.CreateInvoice(model);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(BillingController.Details), redirect.ActionName);
        Assert.Equal(42, redirect.RouteValues?["id"]);
    }

    #endregion

    #region Details Tests

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Details_ReturnsNotFound_WhenIdIsZeroOrNegative(int id)
    {
        var (controller, mockBilling, _, _) = CreateTestHarness();

        var result = await controller.Details(id);

        Assert.IsType<NotFoundResult>(result);
        mockBilling.Verify(s => s.GetInvoiceAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Details_ReturnsNotFound_WhenInvoiceDoesNotExist()
    {
        var (controller, mockBilling, _, _) = CreateTestHarness();
        mockBilling.Setup(s => s.GetInvoiceAsync(999)).ReturnsAsync((ApartmentInvoice?)null);

        var result = await controller.Details(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Details_ReturnsViewResult_WhenInvoiceExists()
    {
        var (controller, mockBilling, _, _) = CreateTestHarness();
        var invoice = new ApartmentInvoice { ApartmentInvoiceId = 1, InvoiceCode = "INV-001" };
        mockBilling.Setup(s => s.GetInvoiceAsync(1)).ReturnsAsync(invoice);

        var result = await controller.Details(1);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ApartmentInvoice>(viewResult.Model);
        Assert.Equal(1, model.ApartmentInvoiceId);
    }

    #endregion

    #region Issue Tests

    [Fact]
    public async Task Issue_Post_ReturnsNotFound_WhenModelIsNull()
    {
        var (controller, _, _, _) = CreateTestHarness();

        var result = await controller.Issue(null!);

        Assert.IsType<NotFoundResult>(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Issue_Post_ReturnsNotFound_WhenInvoiceIdIsZeroOrNegative(int id)
    {
        var (controller, mockBilling, _, _) = CreateTestHarness();

        var result = await controller.Issue(new IssueInvoiceViewModel { InvoiceId = id, RowVersionToken = "AQID" });

        Assert.IsType<NotFoundResult>(result);
        mockBilling.Verify(s => s.IssueAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<byte[]>()), Times.Never);
    }

    [Fact]
    public async Task Issue_Post_ReturnsForbid_WhenUserUnauthenticated()
    {
        var (controller, _, _, _) = CreateTestHarness(userId: null);

        var result = await controller.Issue(new IssueInvoiceViewModel { InvoiceId = 1, RowVersionToken = "AQID" });

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Issue_Post_RedirectsToDetails_WithErrorMessage_WhenRowVersionInvalid()
    {
        var (controller, _, _, _) = CreateTestHarness();

        var result = await controller.Issue(new IssueInvoiceViewModel { InvoiceId = 1, RowVersionToken = "not-valid-base64!" });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(BillingController.Details), redirect.ActionName);
        Assert.Equal(1, redirect.RouteValues?["id"]);
        Assert.Equal("Phiên bản hóa đơn không hợp lệ. Vui lòng tải lại dữ liệu.", controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task Issue_Post_ReturnsNotFound_WhenServiceReportsInvoiceNotFound()
    {
        var (controller, mockBilling, _, _) = CreateTestHarness();
        mockBilling.Setup(s => s.IssueAsync(999, "accountant-user", It.IsAny<byte[]>()))
            .ReturnsAsync(new BillingOperationResult(false, "Không tìm thấy hóa đơn.", null));

        var result = await controller.Issue(new IssueInvoiceViewModel { InvoiceId = 999, RowVersionToken = "AQID" });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Issue_Post_RedirectsToDetails_WithErrorMessage_WhenServiceFails()
    {
        var (controller, mockBilling, _, _) = CreateTestHarness();
        mockBilling.Setup(s => s.IssueAsync(1, "accountant-user", It.IsAny<byte[]>()))
            .ReturnsAsync(new BillingOperationResult(false, "Chỉ có thể phát hành hóa đơn ở trạng thái nháp.", null));

        var result = await controller.Issue(new IssueInvoiceViewModel { InvoiceId = 1, RowVersionToken = "AQID" });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(BillingController.Details), redirect.ActionName);
        Assert.Equal(1, redirect.RouteValues?["id"]);
        Assert.Equal("Chỉ có thể phát hành hóa đơn ở trạng thái nháp.", controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task Issue_Post_RedirectsToDetails_WithSuccessMessage_WhenSuccessful()
    {
        var (controller, mockBilling, _, _) = CreateTestHarness();
        var invoice = new ApartmentInvoice { ApartmentInvoiceId = 1, Status = ApartmentInvoiceStatus.Issued };
        mockBilling.Setup(s => s.IssueAsync(1, "accountant-user", It.IsAny<byte[]>()))
            .ReturnsAsync(new BillingOperationResult(true, null, invoice));

        var result = await controller.Issue(new IssueInvoiceViewModel { InvoiceId = 1, RowVersionToken = "AQID" });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(BillingController.Details), redirect.ActionName);
        Assert.Equal(1, redirect.RouteValues?["id"]);
        Assert.Equal("Đã phát hành hóa đơn cho cư dân.", controller.TempData["SuccessMessage"]);
    }

    #endregion

    #region Void Tests

    [Fact]
    public async Task Void_Post_ReturnsNotFound_WhenModelNullOrIdNonPositive()
    {
        var (controller, _, _, _) = CreateTestHarness();

        var nullResult = await controller.Void(null!);
        var invalidIdResult = await controller.Void(new VoidInvoiceViewModel { InvoiceId = 0, RowVersionToken = "AQID" });

        Assert.IsType<NotFoundResult>(nullResult);
        Assert.IsType<NotFoundResult>(invalidIdResult);
    }

    [Fact]
    public async Task Void_Post_ReturnsForbid_WhenUserNotAuthenticated()
    {
        var (controller, _, _, _) = CreateTestHarness(userId: null);

        var result = await controller.Void(new VoidInvoiceViewModel { InvoiceId = 1, RowVersionToken = "AQID" });

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Void_Post_RedirectsToDetails_WithErrorMessage_WhenRowVersionInvalid()
    {
        var (controller, _, _, _) = CreateTestHarness();

        var result = await controller.Void(new VoidInvoiceViewModel { InvoiceId = 1, RowVersionToken = "not-base-64!" });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(BillingController.Details), redirect.ActionName);
        Assert.Equal(1, redirect.RouteValues?["id"]);
        Assert.Equal("Phiên bản hóa đơn không hợp lệ. Vui lòng tải lại dữ liệu.", controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task Void_Post_ReturnsNotFound_WhenServiceReportsInvoiceNotFound()
    {
        var (controller, mockBilling, _, _) = CreateTestHarness();
        mockBilling.Setup(s => s.VoidAsync(999, "accountant-user", It.IsAny<byte[]>(), It.IsAny<string?>()))
            .ReturnsAsync(new BillingOperationResult(false, "Không tìm thấy hóa đơn.", null));

        var result = await controller.Void(new VoidInvoiceViewModel { InvoiceId = 999, RowVersionToken = "AQID" });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Void_Post_RedirectsToDetails_WithErrorMessage_WhenServiceFails()
    {
        var (controller, mockBilling, _, _) = CreateTestHarness();
        mockBilling.Setup(s => s.VoidAsync(1, "accountant-user", It.IsAny<byte[]>(), It.IsAny<string?>()))
            .ReturnsAsync(new BillingOperationResult(false, "Hóa đơn đã có giao dịch thanh toán được xác nhận. Không thể hủy hóa đơn.", null));

        var result = await controller.Void(new VoidInvoiceViewModel { InvoiceId = 1, RowVersionToken = "AQID" });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(BillingController.Details), redirect.ActionName);
        Assert.Equal(1, redirect.RouteValues?["id"]);
        Assert.Equal("Hóa đơn đã có giao dịch thanh toán được xác nhận. Không thể hủy hóa đơn.", controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task Void_Post_RedirectsToDetails_WithSuccessMessage_WhenSuccessful()
    {
        var (controller, mockBilling, _, _) = CreateTestHarness();
        var invoice = new ApartmentInvoice { ApartmentInvoiceId = 1, Status = ApartmentInvoiceStatus.Void };
        mockBilling.Setup(s => s.VoidAsync(1, "accountant-user", It.IsAny<byte[]>(), It.IsAny<string?>()))
            .ReturnsAsync(new BillingOperationResult(true, null, invoice));

        var result = await controller.Void(new VoidInvoiceViewModel { InvoiceId = 1, RowVersionToken = "AQID" });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(BillingController.Details), redirect.ActionName);
        Assert.Equal(1, redirect.RouteValues?["id"]);
        Assert.Equal("Đã hủy hóa đơn thành công.", controller.TempData["SuccessMessage"]);
    }

    #endregion

    #region TariffDetails Tests

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task TariffDetails_ReturnsNotFound_WhenIdNonPositive(int id)
    {
        var (controller, _, _, _) = CreateTestHarness();

        var result = await controller.TariffDetails(id);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task TariffDetails_ReturnsNotFound_WhenTariffNotFound()
    {
        var (controller, mockBilling, _, _) = CreateTestHarness();
        mockBilling.Setup(s => s.GetTariffAsync(999)).ReturnsAsync((FeeTariff?)null);

        var result = await controller.TariffDetails(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task TariffDetails_ReturnsViewResult_WithDetailsModel()
    {
        var (controller, mockBilling, _, _) = CreateTestHarness();
        var tariff = new FeeTariff
        {
            FeeTariffId = 1,
            Building = new Building { BuildingName = "Tòa B1" },
            ChargeType = FeeChargeType.Management,
            EffectiveFrom = DateTime.Today.AddDays(-10),
            InvoiceLines = []
        };
        mockBilling.Setup(s => s.GetTariffAsync(1)).ReturnsAsync(tariff);

        var result = await controller.TariffDetails(1);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<FeeTariffDetailsViewModel>(viewResult.Model);
        Assert.Equal(1, model.Tariff.FeeTariffId);
        Assert.True(model.IsActive);
        Assert.True(model.CanClose);
    }

    #endregion

    #region CloseTariff Tests

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CloseTariff_Get_ReturnsNotFound_WhenIdNonPositive(int id)
    {
        var (controller, _, _, _) = CreateTestHarness();

        var result = await controller.CloseTariff(id);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CloseTariff_Get_ReturnsNotFound_WhenTariffNotFound()
    {
        var (controller, mockBilling, _, _) = CreateTestHarness();
        mockBilling.Setup(s => s.GetTariffAsync(999)).ReturnsAsync((FeeTariff?)null);

        var result = await controller.CloseTariff(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CloseTariff_Get_ReturnsView_WhenTariffFound()
    {
        var (controller, mockBilling, _, _) = CreateTestHarness();
        var tariff = new FeeTariff
        {
            FeeTariffId = 1,
            Building = new Building { BuildingName = "Tòa B1" },
            ChargeType = FeeChargeType.Management,
            EffectiveFrom = DateTime.Today.AddDays(-10)
        };
        mockBilling.Setup(s => s.GetTariffAsync(1)).ReturnsAsync(tariff);

        var result = await controller.CloseTariff(1);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CloseTariffViewModel>(viewResult.Model);
        Assert.Equal(1, model.FeeTariffId);
        Assert.Equal("Tòa B1", model.BuildingName);
    }

    [Fact]
    public async Task CloseTariff_Post_ReturnsNotFound_WhenModelNullOrIdNonPositive()
    {
        var (controller, _, _, _) = CreateTestHarness();

        var nullResult = await controller.CloseTariff((CloseTariffViewModel)null!);
        var invalidIdResult = await controller.CloseTariff(new CloseTariffViewModel { FeeTariffId = 0 });

        Assert.IsType<NotFoundResult>(nullResult);
        Assert.IsType<NotFoundResult>(invalidIdResult);
    }

    [Fact]
    public async Task CloseTariff_Post_ReturnsForbid_WhenUserNotAuthenticated()
    {
        var (controller, _, _, _) = CreateTestHarness(userId: null);

        var result = await controller.CloseTariff(new CloseTariffViewModel { FeeTariffId = 1, EffectiveTo = DateTime.Today });

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task CloseTariff_Post_ReturnsView_WhenModelStateInvalid()
    {
        var (controller, _, _, _) = CreateTestHarness();
        controller.ModelState.AddModelError("EffectiveTo", "Lỗi ngày kết thúc");

        var model = new CloseTariffViewModel { FeeTariffId = 1, EffectiveTo = DateTime.Today };
        var result = await controller.CloseTariff(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal(model, viewResult.Model);
    }

    [Fact]
    public async Task CloseTariff_Post_ReturnsView_WithModelError_WhenServiceFails()
    {
        var (controller, mockBilling, _, _) = CreateTestHarness();
        mockBilling.Setup(s => s.CloseTariffAsync(1, It.IsAny<DateTime>(), "accountant-user"))
            .ReturnsAsync(new BillingOperationResult(false, "Ngày kết thúc phải từ ngày bắt đầu trở đi.", null));

        var model = new CloseTariffViewModel { FeeTariffId = 1, EffectiveTo = DateTime.Today };
        var result = await controller.CloseTariff(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains("Ngày kết thúc phải từ ngày bắt đầu trở đi.", controller.ModelState[string.Empty]!.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task CloseTariff_Post_RedirectsToTariffDetails_WhenSuccessful()
    {
        var (controller, mockBilling, _, _) = CreateTestHarness();
        mockBilling.Setup(s => s.CloseTariffAsync(1, It.IsAny<DateTime>(), "accountant-user"))
            .ReturnsAsync(new BillingOperationResult(true, null, null));

        var model = new CloseTariffViewModel { FeeTariffId = 1, EffectiveTo = DateTime.Today };
        var result = await controller.CloseTariff(model);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(BillingController.TariffDetails), redirect.ActionName);
        Assert.Equal(1, redirect.RouteValues?["id"]);
        Assert.Equal("Đã cập nhật thời hạn kết thúc biểu phí.", controller.TempData["SuccessMessage"]);
    }

    #endregion
}
