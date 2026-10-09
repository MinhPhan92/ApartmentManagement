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
        var userId = CurrentUserId();
        if (userId == null) return Forbid();
        var invoice = await _billingService.GetResidentInvoiceAsync(userId, id);
        if (invoice == null) return NotFound();

        return View(new ResidentInvoiceDetailsViewModel
        {
            Invoice = invoice,
            Payment = await _paymentService.GetResidentPaymentForInvoiceAsync(userId, id),
            VietQrConfigured = _vietQrOptions.IsConfigured,
            VietQrAmountSupported = invoice.TotalAmount is > 0 and <= VietQrCodeGenerator.MaximumVndAmount
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSimulatedPayment(int id)
    {
        var userId = CurrentUserId();
        if (userId == null) return Forbid();
        if (!_vietQrOptions.IsConfigured)
        {
            TempData["ErrorMessage"] = "VietQR chưa khả dụng vì chưa cấu hình tài khoản nhận.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var payment = await _paymentService.GetOrCreateResidentPaymentAsync(userId, id);
        if (payment == null)
        {
            TempData["ErrorMessage"] = "Không thể tạo giao dịch. Hãy xác nhận hóa đơn còn hiệu lực và thuộc tài khoản của bạn.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> PaymentQr(int id)
    {
        var userId = CurrentUserId();
        if (userId == null) return Forbid();
        if (!_vietQrOptions.IsConfigured) return NotFound();

        var payment = await _paymentService.GetResidentPaymentAsync(userId, id);
        if (payment == null || payment.Status != PaymentTransactionStatus.Pending ||
            payment.Amount > VietQrCodeGenerator.MaximumVndAmount)
            return NotFound();

        Response.Headers.CacheControl = "no-store";
        return File(_qrCodeGenerator.GeneratePng(payment.Reference, payment.Amount), "image/png");
    }

    private string? CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
}
