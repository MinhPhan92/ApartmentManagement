using System.Security.Claims;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ApartmentManagement.Controllers;

[Authorize(Policy = AppPolicies.RequireResident)]
public sealed class MyInvoicesController : Controller
{
    private readonly IBillingService _billingService;
    private readonly IPaymentService _paymentService;
    private readonly IVietQrCodeGenerator _qrCodeGenerator;
    private readonly VietQrOptions _vietQrOptions;

    public MyInvoicesController(
        IBillingService billingService,
        IPaymentService paymentService,
        IVietQrCodeGenerator qrCodeGenerator,
        IOptions<VietQrOptions> vietQrOptions)
    {
        _billingService = billingService;
        _paymentService = paymentService;
        _qrCodeGenerator = qrCodeGenerator;
        _vietQrOptions = vietQrOptions.Value;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = CurrentUserId();
        if (userId == null) return Forbid();
        return View(new ResidentInvoiceListViewModel
        {
            Invoices = await _billingService.GetResidentInvoicesAsync(userId)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        if (id <= 0) return NotFound();
        var userId = CurrentUserId();
        if (userId == null) return Forbid();
        var invoice = await _billingService.GetResidentInvoiceAsync(userId, id);
        if (invoice == null) return NotFound();

        var transactions = (invoice.PaymentTransactions ?? [])
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.PaymentTransactionId)
            .ToList();

        var payment = await _paymentService.GetResidentPaymentForInvoiceAsync(userId, id)
            ?? transactions
                .OrderByDescending(x => x.Status == PaymentTransactionStatus.Confirmed)
                .ThenByDescending(x => x.Status == PaymentTransactionStatus.Pending)
                .ThenByDescending(x => x.CreatedAtUtc)
                .ThenByDescending(x => x.PaymentTransactionId)
                .FirstOrDefault();

        var confirmedAmount = transactions
            .Where(x => x.Status == PaymentTransactionStatus.Confirmed)
            .Sum(x => x.Amount);
        var remainingAmount = invoice.Status == ApartmentInvoiceStatus.Void
            ? 0m
            : Math.Max(0m, invoice.TotalAmount - confirmedAmount);
        var vietQrAmountSupported = invoice.TotalAmount is > 0 and <= VietQrCodeGenerator.MaximumVndAmount &&
                                    (remainingAmount <= 0 || remainingAmount <= VietQrCodeGenerator.MaximumVndAmount);
        var canCreatePaymentAttempt = invoice.Status == ApartmentInvoiceStatus.Issued &&
                                      remainingAmount > 0 &&
                                      _vietQrOptions.IsConfigured &&
                                      vietQrAmountSupported;
        var hasAutoClosedRejections = transactions.Any(x =>
            x.Status == PaymentTransactionStatus.Rejected &&
            PaymentDisplayHelper.IsAutoClosedRejectionReason(x.RejectionReason));

        return View(new ResidentInvoiceDetailsViewModel
        {
            Invoice = invoice,
            Payment = payment,
            Transactions = transactions,
            ConfirmedAmount = confirmedAmount,
            RemainingAmount = remainingAmount,
            VietQrConfigured = _vietQrOptions.IsConfigured,
            VietQrAmountSupported = vietQrAmountSupported,
            CanCreatePaymentAttempt = canCreatePaymentAttempt,
            VietQrOptions = _vietQrOptions,
            HasAutoClosedRejections = hasAutoClosedRejections
        });
    }

    [HttpGet]
    public async Task<IActionResult> TransactionDetails(int id)
    {
        if (id <= 0) return NotFound();
        var userId = CurrentUserId();
        if (userId == null) return Forbid();

        var payment = await _paymentService.GetResidentPaymentAsync(userId, id);
        if (payment == null) return NotFound();

        var invoice = payment.Invoice ?? await _billingService.GetResidentInvoiceAsync(userId, payment.ApartmentInvoiceId);
        if (invoice == null) return NotFound();

        var amountSupported = payment.Amount is > 0 and <= VietQrCodeGenerator.MaximumVndAmount;
        var canShowVietQr = payment.Status == PaymentTransactionStatus.Pending &&
                            invoice.Status == ApartmentInvoiceStatus.Issued &&
                            _vietQrOptions.IsConfigured &&
                            amountSupported;

        return View(new ResidentPaymentTransactionDetailsViewModel
        {
            Transaction = payment,
            Invoice = invoice,
            VietQrConfigured = _vietQrOptions.IsConfigured,
            VietQrAmountSupported = amountSupported,
            CanShowVietQr = canShowVietQr,
            VietQrOptions = _vietQrOptions,
            IsAutoClosedRejection = payment.Status == PaymentTransactionStatus.Rejected &&
                                    PaymentDisplayHelper.IsAutoClosedRejectionReason(payment.RejectionReason)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSimulatedPayment(int id)
    {
        if (id <= 0) return NotFound();
        var userId = CurrentUserId();
        if (userId == null) return Forbid();
        if (!_vietQrOptions.IsConfigured)
        {
            TempData["ErrorMessage"] = "VietQR chưa khả dụng vì chưa cấu hình tài khoản nhận.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var result = await _paymentService.CreateResidentPaymentAttemptAsync(userId, id);
        if (!result.Success || result.Payment == null)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ??
                "Không thể tạo giao dịch. Hãy xác nhận hóa đơn còn hiệu lực và thuộc tài khoản của bạn.";
        }
        else
        {
            TempData["SuccessMessage"] = "Đã tạo lượt thanh toán VietQR mới. Vui lòng chuyển khoản đúng số tiền và nội dung tham chiếu.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> CreatePaymentAttempt(int id) => CreateSimulatedPayment(id);

    [HttpGet]
    public async Task<IActionResult> PaymentQr(int id)
    {
        if (id <= 0) return NotFound();
        var userId = CurrentUserId();
        if (userId == null) return Forbid();
        if (!_vietQrOptions.IsConfigured) return NotFound();

        var payment = await _paymentService.GetResidentPaymentAsync(userId, id);
        if (payment == null || payment.Status != PaymentTransactionStatus.Pending ||
            payment.Amount is <= 0 or > VietQrCodeGenerator.MaximumVndAmount)
            return NotFound();

        Response.Headers.CacheControl = "no-store";
        return File(_qrCodeGenerator.GeneratePng(payment.Reference, payment.Amount), "image/png");
    }

    private string? CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
}
