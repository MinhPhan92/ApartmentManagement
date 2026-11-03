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
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ApartmentManagement.Tests;

public sealed class PaymentControllersTests
{
    private static ControllerContext CreateControllerContext(string? userId, string role)
    {
        var httpContext = new DefaultHttpContext();
        if (userId != null)
        {
            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, role)
            ], "TestAuth");
            httpContext.User = new ClaimsPrincipal(identity);
        }
        else
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        }

        return new ControllerContext { HttpContext = httpContext };
    }

    private static (MyInvoicesController controller, Mock<IBillingService> mockBilling, Mock<IPaymentService> mockPayment, Mock<IVietQrCodeGenerator> mockQr)
        CreateResidentHarness(string? userId = "resident-1", bool vietQrConfigured = true)
    {
        var mockBilling = new Mock<IBillingService>();
        var mockPayment = new Mock<IPaymentService>();
        var mockQr = new Mock<IVietQrCodeGenerator>();
        var options = Options.Create(vietQrConfigured
            ? new VietQrOptions
            {
                BankBin = "970415",
                AccountNumber = "102800123456",
                AccountName = "BAN QUAN LY CHUNG CU"
            }
            : new VietQrOptions());

        var controller = new MyInvoicesController(
            mockBilling.Object,
            mockPayment.Object,
            mockQr.Object,
            options)
        {
            ControllerContext = CreateControllerContext(userId, AppRoles.Resident),
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        return (controller, mockBilling, mockPayment, mockQr);
    }

    private static (PaymentsController controller, Mock<IPaymentService> mockPayment)
        CreateStaffHarness(string? userId = "accountant-1")
    {
        var mockPayment = new Mock<IPaymentService>();
        var controller = new PaymentsController(mockPayment.Object)
        {
            ControllerContext = CreateControllerContext(userId, AppRoles.Accountant),
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        return (controller, mockPayment);
    }

    [Fact]
    public void Controllers_HaveExpectedAuthorizePoliciesAndPostAttributes()
    {
        var residentAuth = typeof(MyInvoicesController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(residentAuth);
        Assert.Equal(AppPolicies.RequireResident, residentAuth.Policy);

        var staffAuth = typeof(PaymentsController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(staffAuth);
        Assert.Equal(AppPolicies.RequireAccountantOrManager, staffAuth.Policy);

        foreach (var actionName in new[] { nameof(MyInvoicesController.CreateSimulatedPayment), nameof(MyInvoicesController.CreatePaymentAttempt) })
        {
            var method = typeof(MyInvoicesController).GetMethod(actionName, [typeof(int)]);
            Assert.NotNull(method);
            Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
            Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        }

        var confirmMethod = typeof(PaymentsController).GetMethod(nameof(PaymentsController.Confirm), [typeof(ConfirmPaymentViewModel)]);
        Assert.NotNull(confirmMethod);
        Assert.NotNull(confirmMethod.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(confirmMethod.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());

        var rejectMethod = typeof(PaymentsController).GetMethod(nameof(PaymentsController.Reject), [typeof(RejectPaymentViewModel)]);
        Assert.NotNull(rejectMethod);
        Assert.NotNull(rejectMethod.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(rejectMethod.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public async Task MyInvoices_Details_Get_DoesNotCreatePaymentTransaction_AndReturnsFullHistory()
    {
        var (controller, mockBilling, mockPayment, _) = CreateResidentHarness("resident-1");

        var rejectedTx = new PaymentTransaction
        {
            PaymentTransactionId = 1,
            ApartmentInvoiceId = 10,
            Reference = "AH00010ATTEMPT00000000001",
            Amount = 1200000m,
            Status = PaymentTransactionStatus.Rejected,
            CreatedAtUtc = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc),
            RejectedAtUtc = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc),
            RejectedByUserId = "staff-1",
            RejectionReason = "Hóa đơn đã được thanh toán đủ qua giao dịch AH00010ATTEMPT00000000002"
        };
        var confirmedTx = new PaymentTransaction
        {
            PaymentTransactionId = 2,
            ApartmentInvoiceId = 10,
            Reference = "AH00010ATTEMPT00000000002",
            Amount = 1200000m,
            Status = PaymentTransactionStatus.Confirmed,
            CreatedAtUtc = new DateTime(2026, 10, 2, 8, 0, 0, DateTimeKind.Utc),
            ConfirmedAtUtc = new DateTime(2026, 10, 2, 10, 0, 0, DateTimeKind.Utc),
            ConfirmedByUserId = "staff-1"
        };
        var invoice = new ApartmentInvoice
        {
            ApartmentInvoiceId = 10,
            InvoiceCode = "INV-10",
            Status = ApartmentInvoiceStatus.Issued,
            TotalAmount = 1200000m,
            PaymentTransactions = [rejectedTx, confirmedTx]
        };

        mockBilling.Setup(s => s.GetResidentInvoiceAsync("resident-1", 10)).ReturnsAsync(invoice);
        mockPayment.Setup(s => s.GetResidentPaymentForInvoiceAsync("resident-1", 10)).ReturnsAsync(confirmedTx);

        var result = await controller.Details(10);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ResidentInvoiceDetailsViewModel>(viewResult.Model);
        Assert.Equal(2, model.Transactions.Count);
        Assert.Equal(2, model.Transactions[0].PaymentTransactionId);
        Assert.Equal(1, model.Transactions[1].PaymentTransactionId);
        Assert.Equal(1200000m, model.ConfirmedAmount);
        Assert.Equal(0m, model.RemainingAmount);
        Assert.False(model.CanCreatePaymentAttempt);
        Assert.True(model.HasAutoClosedRejections);
        Assert.Contains("không đồng nghĩa với việc ngân hàng chắc chắn chưa nhận tiền", model.ReconciliationWarningMessage);

        mockPayment.Verify(s => s.GetOrCreateResidentPaymentAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        mockPayment.Verify(s => s.CreateResidentPaymentAttemptAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task MyInvoices_CreateSimulatedPayment_Post_CallsCreateResidentPaymentAttemptAsync()
    {
        var (controller, _, mockPayment, _) = CreateResidentHarness("resident-1");
        var newPayment = new PaymentTransaction
        {
            PaymentTransactionId = 7,
            ApartmentInvoiceId = 10,
            Reference = "AH00010NEWATTEMPT00000007",
            Amount = 950000m,
            Status = PaymentTransactionStatus.Pending
        };
        mockPayment.Setup(s => s.CreateResidentPaymentAttemptAsync("resident-1", 10))
            .ReturnsAsync(new PaymentOperationResult(true, null, newPayment));

        var result = await controller.CreateSimulatedPayment(10);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(MyInvoicesController.Details), redirect.ActionName);
        Assert.Equal(10, redirect.RouteValues?["id"]);
        Assert.NotNull(controller.TempData["SuccessMessage"]);
        mockPayment.Verify(s => s.CreateResidentPaymentAttemptAsync("resident-1", 10), Times.Once);
    }

    [Theory]
    [InlineData("Hóa đơn đã được thanh toán đủ. Không thể tạo thêm giao dịch mới.")]
    [InlineData("Chỉ được tạo giao dịch thanh toán cho hóa đơn đã phát hành.")]
    [InlineData("Hóa đơn đã thay đổi đồng thời. Tải lại dữ liệu trước khi thử lại.")]
    public async Task MyInvoices_CreateSimulatedPayment_Post_WhenPaidOrVoidOrConflict_SetsErrorMessage(string serviceError)
    {
        var (controller, _, mockPayment, _) = CreateResidentHarness("resident-1");
        mockPayment.Setup(s => s.CreateResidentPaymentAttemptAsync("resident-1", 10))
            .ReturnsAsync(new PaymentOperationResult(false, serviceError, null));

        var result = await controller.CreateSimulatedPayment(10);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(MyInvoicesController.Details), redirect.ActionName);
        Assert.Equal(serviceError, controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task MyInvoices_TransactionDetails_ResidentCannotAccessOtherResidentsTransaction()
    {
        var (controller, _, mockPayment, _) = CreateResidentHarness("resident-1");
        mockPayment.Setup(s => s.GetResidentPaymentAsync("resident-1", 99)).ReturnsAsync((PaymentTransaction?)null);

        var result = await controller.TransactionDetails(99);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task MyInvoices_TransactionDetails_ReturnsFullDetailsForOwnTransaction()
    {
        var (controller, _, mockPayment, _) = CreateResidentHarness("resident-1");
        var invoice = new ApartmentInvoice
        {
            ApartmentInvoiceId = 10,
            InvoiceCode = "INV-10",
            Status = ApartmentInvoiceStatus.Issued,
            TotalAmount = 800000m
        };
        var tx = new PaymentTransaction
        {
            PaymentTransactionId = 55,
            ApartmentInvoiceId = 10,
            Invoice = invoice,
            Reference = "AH00010TX5500000000000055",
            Amount = 800000m,
            Status = PaymentTransactionStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };
        mockPayment.Setup(s => s.GetResidentPaymentAsync("resident-1", 55)).ReturnsAsync(tx);

        var result = await controller.TransactionDetails(55);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ResidentPaymentTransactionDetailsViewModel>(viewResult.Model);
        Assert.Equal("AH00010TX5500000000000055", model.Transaction.Reference);
        Assert.True(model.CanShowVietQr);
        Assert.Contains("không phải bằng chứng thanh toán", model.QrDisclaimerMessage);
    }

    [Fact]
    public async Task Payments_Index_SupportsFilteringByStatusReferenceApartmentResidentDateRangeAndPagination()
    {
        var (controller, mockPayment) = CreateStaffHarness("accountant-1");

        var list = Enumerable.Range(1, 15).Select(i => new PaymentTransaction
        {
            PaymentTransactionId = i,
            ApartmentInvoiceId = 100 + i,
            Reference = $"AH00100REF{i:D15}",
            Amount = 500000m + i * 1000m,
            Status = i % 3 == 0
                ? PaymentTransactionStatus.Rejected
                : i % 2 == 0
                    ? PaymentTransactionStatus.Confirmed
                    : PaymentTransactionStatus.Pending,
            RejectionReason = i % 3 == 0 ? "Hóa đơn đã bị hủy bởi BQL: Sai chỉ số" : null,
            CreatedAtUtc = new DateTime(2026, 10, i, 10, 0, 0, DateTimeKind.Utc),
            Invoice = new ApartmentInvoice
            {
                ApartmentInvoiceId = 100 + i,
                InvoiceCode = $"INV-{i:D3}",
                Apartment = new Apartment
                {
                    ApartmentCode = i <= 8 ? "A-101" : "B-202",
                    Building = new Building { BuildingCode = "BLD-A", BuildingName = "Tòa A" }
                },
                Resident = new Resident
                {
                    User = new ApplicationUser { FullName = i <= 8 ? "Nguyễn Văn An" : "Trần Thị Bình" }
                }
            }
        }).ToList();

        mockPayment.Setup(s => s.GetTransactionsAsync(null, null)).ReturnsAsync(list);
        mockPayment.Setup(s => s.GetTransactionsAsync(null, null)).ReturnsAsync(list);

        var result = await controller.Index(
            status: null,
            searchTerm: null,
            reference: "AH00100",
            apartment: "A-101",
            resident: "Nguyễn Văn An",
            fromDate: new DateTime(2026, 10, 2),
            toDate: new DateTime(2026, 10, 7),
            page: 2,
            pageSize: 4);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PaymentTransactionsViewModel>(viewResult.Model);

        // Các ngày từ 2..7 trong nhóm A-101 gồm 6 bản ghi (i = 2, 3, 4, 5, 6, 7); trang 2 với pageSize=4 trả về 2 bản ghi
        Assert.Equal(6, model.TotalFilteredCount);
        Assert.Equal(2, model.TotalPages);
        Assert.Equal(2, model.CurrentPage);
        Assert.Equal(2, model.Transactions.Count);
        Assert.Equal(15, model.TotalCount);
        Assert.Equal(5, model.RejectedCount);
        Assert.True(model.HasAutoClosedRejections);
        Assert.Contains(model.StatusOptions, o => o.Value == PaymentTransactionStatus.Rejected.ToString());
    }

    [Fact]
    public async Task Payments_Confirm_ValidPendingTransaction_CallsServiceAndSetsSuccessMessage()
    {
        var (controller, mockPayment) = CreateStaffHarness("accountant-1");
        var rv = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var token = Convert.ToBase64String(rv);

        var confirmed = new PaymentTransaction
        {
            PaymentTransactionId = 12,
            Status = PaymentTransactionStatus.Confirmed
        };
        mockPayment.Setup(s => s.ConfirmSimulatedPaymentAsync(12, "accountant-1", It.Is<byte[]>(b => b.SequenceEqual(rv))))
            .ReturnsAsync(new PaymentOperationResult(true, null, confirmed));

        var result = await controller.Confirm(new ConfirmPaymentViewModel
        {
            PaymentTransactionId = 12,
            RowVersionToken = token
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PaymentsController.Index), redirect.ActionName);
        Assert.NotNull(controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task Payments_Reject_MissingOrBlankReason_IsRejectedWithoutCallingService()
    {
        var (controller, mockPayment) = CreateStaffHarness("accountant-1");

        var result = await controller.Reject(new RejectPaymentViewModel
        {
            PaymentTransactionId = 12,
            RowVersionToken = "AQIDBA==",
            Reason = "   "
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PaymentsController.Index), redirect.ActionName);
        Assert.Equal("Vui lòng nhập lý do từ chối giao dịch.", controller.TempData["ErrorMessage"]);
        mockPayment.Verify(s => s.RejectPaymentAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>()), Times.Never);
    }

    [Fact]
    public async Task Payments_Reject_ValidRequest_CallsServiceAndRedirects()
    {
        var (controller, mockPayment) = CreateStaffHarness("accountant-1");
        var rv = new byte[] { 1, 2, 3, 4 };
        var token = Convert.ToBase64String(rv);

        var rejectedTx = new PaymentTransaction
        {
            PaymentTransactionId = 15,
            Status = PaymentTransactionStatus.Rejected,
            RejectionReason = "Sai nội dung và số tiền chuyển khoản"
        };
        mockPayment.Setup(s => s.RejectPaymentAsync(
                15,
                "accountant-1",
                "Sai nội dung và số tiền chuyển khoản",
                It.Is<byte[]>(b => b.SequenceEqual(rv))))
            .ReturnsAsync(new PaymentOperationResult(true, null, rejectedTx));

        var result = await controller.Reject(new RejectPaymentViewModel
        {
            PaymentTransactionId = 15,
            RowVersionToken = token,
            Reason = "  Sai nội dung và số tiền chuyển khoản  ",
            ReturnToDetails = true
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PaymentsController.Details), redirect.ActionName);
        Assert.Equal(15, redirect.RouteValues?["id"]);
        Assert.NotNull(controller.TempData["SuccessMessage"]);
    }

    [Theory]
    [InlineData(PaymentTransactionStatus.Confirmed, "Giao dịch đã được xác nhận trước đó, không thể xác nhận lại.")]
    [InlineData(PaymentTransactionStatus.Rejected, "Giao dịch đã bị từ chối, không thể xác nhận.")]
    public async Task Payments_Confirm_AlreadyConfirmedOrRejectedTransaction_BlocksAndSetsErrorMessage(
        PaymentTransactionStatus existingStatus,
        string expectedMessage)
    {
        var (controller, mockPayment) = CreateStaffHarness("accountant-1");
        mockPayment.Setup(s => s.GetTransactionsAsync(null, null)).ReturnsAsync([
            new PaymentTransaction
            {
                PaymentTransactionId = 20,
                Status = existingStatus,
                RowVersion = [1, 2, 3, 4]
            }
        ]);

        var result = await controller.Confirm(new ConfirmPaymentViewModel
        {
            PaymentTransactionId = 20,
            RowVersionToken = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 })
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PaymentsController.Index), redirect.ActionName);
        Assert.Equal(expectedMessage, controller.TempData["ErrorMessage"]);
        mockPayment.Verify(s => s.ConfirmSimulatedPaymentAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<byte[]>()), Times.Never);
    }

    [Fact]
    public async Task Payments_ConfirmAndReject_WhenConcurrencyConflictOccurs_ReflectsFriendlyErrorMessage()
    {
        var (controller, mockPayment) = CreateStaffHarness("accountant-1");
        const string conflictMsg = "Giao dịch đã thay đổi đồng thời. Tải lại dữ liệu trước khi thử lại.";

        mockPayment.Setup(s => s.ConfirmSimulatedPaymentAsync(30, "accountant-1", It.IsAny<byte[]>()))
            .ReturnsAsync(new PaymentOperationResult(false, conflictMsg, null));
        mockPayment.Setup(s => s.RejectPaymentAsync(30, "accountant-1", "Lý do hợp lệ", It.IsAny<byte[]>()))
            .ReturnsAsync(new PaymentOperationResult(false, conflictMsg, null));

        var confirmResult = await controller.Confirm(new ConfirmPaymentViewModel
        {
            PaymentTransactionId = 30,
            RowVersionToken = "AQIDBA=="
        });
        Assert.IsType<RedirectToActionResult>(confirmResult);
        Assert.Equal(conflictMsg, controller.TempData["ErrorMessage"]);

        controller.TempData.Clear();

        var rejectResult = await controller.Reject(new RejectPaymentViewModel
        {
            PaymentTransactionId = 30,
            RowVersionToken = "AQIDBA==",
            Reason = "Lý do hợp lệ"
        });
        Assert.IsType<RedirectToActionResult>(rejectResult);
        Assert.Equal(conflictMsg, controller.TempData["ErrorMessage"]);
    }
}

