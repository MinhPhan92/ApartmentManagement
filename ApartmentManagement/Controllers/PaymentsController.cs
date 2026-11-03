using System.Security.Claims;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ApartmentManagement.Controllers;

[Authorize(Policy = AppPolicies.RequireAccountantOrManager)]
public sealed class PaymentsController : Controller
{
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 100;
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService) =>
        _paymentService = paymentService;

    [HttpGet]
    public async Task<IActionResult> Index(
        PaymentTransactionStatus? status = null,
        string? searchTerm = null,
        string? reference = null,
        string? apartment = null,
        string? resident = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int page = 1,
        int pageSize = DefaultPageSize)
    {
        var allTransactions = await _paymentService.GetTransactionsAsync() ?? [];
        var filteredTransactions = await _paymentService.GetTransactionsAsync(status, searchTerm) ?? [];

        IEnumerable<PaymentTransaction> query = filteredTransactions;

        if (!string.IsNullOrWhiteSpace(reference))
        {
            var refTerm = reference.Trim();
            query = query.Where(x => x.Reference.Contains(refTerm, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(apartment))
        {
            var aptTerm = apartment.Trim();
            query = query.Where(x =>
                (x.Invoice?.Apartment?.ApartmentCode?.Contains(aptTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.Invoice?.Apartment?.Building?.BuildingName?.Contains(aptTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.Invoice?.Apartment?.Building?.BuildingCode?.Contains(aptTerm, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (!string.IsNullOrWhiteSpace(resident))
        {
            var resTerm = resident.Trim();
            query = query.Where(x =>
                x.Invoice?.Resident?.User?.FullName?.Contains(resTerm, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        if (fromDate.HasValue)
        {
            var startUtc = DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Utc);
            query = query.Where(x => x.CreatedAtUtc >= startUtc);
        }

        if (toDate.HasValue)
        {
            var endExclusiveUtc = DateTime.SpecifyKind(toDate.Value.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(x => x.CreatedAtUtc < endExclusiveUtc);
        }

        var finalFilteredList = query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.PaymentTransactionId)
            .ToList();

        var effectivePageSize = pageSize is < 1 or > MaxPageSize ? DefaultPageSize : pageSize;
        var totalFilteredCount = finalFilteredList.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalFilteredCount / (double)effectivePageSize));
        var currentPage = Math.Clamp(page < 1 ? 1 : page, 1, totalPages);

        var pagedTransactions = finalFilteredList
            .Skip((currentPage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .ToList();

        var statusOptions = new List<SelectListItem>
        {
            new("Tất cả trạng thái", ""),
            new("Chờ xác nhận", PaymentTransactionStatus.Pending.ToString(), status == PaymentTransactionStatus.Pending),
            new("Đã xác nhận", PaymentTransactionStatus.Confirmed.ToString(), status == PaymentTransactionStatus.Confirmed),
            new("Đã từ chối", PaymentTransactionStatus.Rejected.ToString(), status == PaymentTransactionStatus.Rejected)
        };

        var totalCount = allTransactions.Count;
        var pendingCount = allTransactions.Count(x => x.Status == PaymentTransactionStatus.Pending);
        var confirmedCount = allTransactions.Count(x => x.Status == PaymentTransactionStatus.Confirmed);
        var rejectedCount = allTransactions.Count(x => x.Status == PaymentTransactionStatus.Rejected);
        var totalConfirmedAmount = allTransactions
            .Where(x => x.Status == PaymentTransactionStatus.Confirmed)
            .Sum(x => x.Amount);
        var hasAutoClosedRejections = finalFilteredList.Any(x =>
            x.Status == PaymentTransactionStatus.Rejected &&
            PaymentDisplayHelper.IsAutoClosedRejectionReason(x.RejectionReason));

        return View(new PaymentTransactionsViewModel
        {
            Transactions = pagedTransactions,
            Status = status,
            SearchTerm = searchTerm,
            Reference = reference,
            Apartment = apartment,
            Resident = resident,
            FromDate = fromDate,
            ToDate = toDate,
            StatusOptions = statusOptions,
            CurrentPage = currentPage,
            PageSize = effectivePageSize,
            TotalFilteredCount = totalFilteredCount,
            TotalPages = totalPages,
            TotalCount = totalCount,
            PendingCount = pendingCount,
            ConfirmedCount = confirmedCount,
            RejectedCount = rejectedCount,
            TotalConfirmedAmount = totalConfirmedAmount,
            HasAutoClosedRejections = hasAutoClosedRejections
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        if (id <= 0) return NotFound();

        var allTransactions = await _paymentService.GetTransactionsAsync() ?? [];
        var transaction = allTransactions.FirstOrDefault(x => x.PaymentTransactionId == id);
        if (transaction == null) return NotFound();

        var invoiceTransactions = allTransactions
            .Where(x => x.ApartmentInvoiceId == transaction.ApartmentInvoiceId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.PaymentTransactionId)
            .ToList();

        var hasConfirmedOnInvoice = invoiceTransactions.Any(x => x.Status == PaymentTransactionStatus.Confirmed);
        var canConfirm = transaction.Status == PaymentTransactionStatus.Pending &&
                         transaction.Invoice?.Status == ApartmentInvoiceStatus.Issued &&
                         !hasConfirmedOnInvoice;
        var canReject = transaction.Status == PaymentTransactionStatus.Pending;

        return View(new PaymentTransactionDetailsViewModel
        {
            Transaction = transaction,
            Invoice = transaction.Invoice!,
            InvoiceTransactions = invoiceTransactions,
            RowVersionToken = Convert.ToBase64String(transaction.RowVersion),
            CanConfirm = canConfirm,
            CanReject = canReject,
            IsAutoClosedRejection = transaction.Status == PaymentTransactionStatus.Rejected &&
                                    PaymentDisplayHelper.IsAutoClosedRejectionReason(transaction.RejectionReason)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(ConfirmPaymentViewModel model)
    {
        if (model == null || model.PaymentTransactionId <= 0) return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Forbid();

        if (!TryDecodeRowVersion(model.RowVersionToken, out var rowVersion))
        {
            TempData["ErrorMessage"] = "Phiên bản giao dịch không hợp lệ. Vui lòng tải lại dữ liệu.";
            return RedirectAfterAction(model.PaymentTransactionId, model.ReturnInvoiceId, model.ReturnToDetails);
        }

        var existingTransactions = await _paymentService.GetTransactionsAsync();
        var existing = existingTransactions?.FirstOrDefault(x => x.PaymentTransactionId == model.PaymentTransactionId);
        if (existing != null && existing.Status != PaymentTransactionStatus.Pending)
        {
            TempData["ErrorMessage"] = existing.Status == PaymentTransactionStatus.Confirmed
                ? "Giao dịch đã được xác nhận trước đó, không thể xác nhận lại."
                : "Giao dịch đã bị từ chối, không thể xác nhận.";
            return RedirectAfterAction(model.PaymentTransactionId, model.ReturnInvoiceId, model.ReturnToDetails);
        }

        var result = await _paymentService.ConfirmSimulatedPaymentAsync(
            model.PaymentTransactionId,
            userId,
            rowVersion);
        if (!result.Success)
        {
            if (result.ErrorMessage == "Không tìm thấy giao dịch.")
                return NotFound();

            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Đã ghi nhận xác nhận thanh toán thành công.";
        }

        return RedirectAfterAction(model.PaymentTransactionId, model.ReturnInvoiceId, model.ReturnToDetails);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(RejectPaymentViewModel model)
    {
        if (model == null || model.PaymentTransactionId <= 0) return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Forbid();

        if (string.IsNullOrWhiteSpace(model.Reason))
        {
            TempData["ErrorMessage"] = "Vui lòng nhập lý do từ chối giao dịch.";
            return RedirectAfterAction(model.PaymentTransactionId, model.ReturnInvoiceId, model.ReturnToDetails);
        }

        var trimmedReason = model.Reason.Trim();
        if (trimmedReason.Length > 500)
        {
            TempData["ErrorMessage"] = "Lý do từ chối không được vượt quá 500 ký tự.";
            return RedirectAfterAction(model.PaymentTransactionId, model.ReturnInvoiceId, model.ReturnToDetails);
        }

        if (!TryDecodeRowVersion(model.RowVersionToken, out var rowVersion))
        {
            TempData["ErrorMessage"] = "Phiên bản giao dịch không hợp lệ. Vui lòng tải lại dữ liệu.";
            return RedirectAfterAction(model.PaymentTransactionId, model.ReturnInvoiceId, model.ReturnToDetails);
        }

        var result = await _paymentService.RejectPaymentAsync(
            model.PaymentTransactionId,
            userId,
            trimmedReason,
            rowVersion);
        if (!result.Success)
        {
            if (result.ErrorMessage == "Không tìm thấy giao dịch.")
                return NotFound();

            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Đã từ chối giao dịch thanh toán và ghi nhận lý do.";
        }

        return RedirectAfterAction(model.PaymentTransactionId, model.ReturnInvoiceId, model.ReturnToDetails);
    }

    private IActionResult RedirectAfterAction(int paymentTransactionId, int? returnInvoiceId, bool returnToDetails)
    {
        if (returnInvoiceId is > 0)
            return RedirectToAction("Details", "Billing", new { id = returnInvoiceId.Value });

        if (returnToDetails)
            return RedirectToAction(nameof(Details), new { id = paymentTransactionId });

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
