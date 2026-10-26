using System.Security.Claims;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApartmentManagement.Controllers;

[Authorize(Policy = AppPolicies.RequireAccountantOrManager)]
public sealed class PaymentsController : Controller
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService) =>
        _paymentService = paymentService;

    [HttpGet]
    public async Task<IActionResult> Index() =>
        View(new PaymentTransactionsViewModel
        {
            Transactions = await _paymentService.GetTransactionsAsync()
        });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(ConfirmPaymentViewModel model)
    {
        if (model == null || model.PaymentTransactionId <= 0) return NotFound();
        if (!TryDecodeRowVersion(model.RowVersionToken, out var rowVersion))
            return BadRequest();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Forbid();

        var result = await _paymentService.ConfirmSimulatedPaymentAsync(
            model.PaymentTransactionId,
            userId,
            rowVersion);
        if (!result.Success)
            TempData["ErrorMessage"] = result.ErrorMessage;
        else
            TempData["SuccessMessage"] = "Đã ghi nhận xác nhận thanh toán giả lập.";

        return RedirectToAction(nameof(Index));
    }

    private static bool TryDecodeRowVersion(string? token, out byte[] rowVersion)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            rowVersion = [];
            return false;
        }

        try
        {
            rowVersion = Convert.FromBase64String(token.Trim());
            return rowVersion.Length > 0;
        }
        catch (FormatException)
        {
            rowVersion = [];
            return false;
        }
    }
}
