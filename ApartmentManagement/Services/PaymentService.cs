using ApartmentManagement.Data;
using ApartmentManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Services;

public sealed class PaymentService : IPaymentService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PaymentService> _logger;
    private readonly INotificationService? _notificationService;

    public PaymentService(
        ApplicationDbContext context,
        ILogger<PaymentService> logger,
        INotificationService? notificationService = null)
    {
        _context = context;
        _logger = logger;
        _notificationService = notificationService;
    }

    public Task<List<PaymentTransaction>> GetTransactionsAsync(PaymentTransactionStatus? status = null, string? searchTerm = null)
    {
        var query = PaymentQuery();
        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(x =>
                x.Reference.ToLower().Contains(term) ||
                x.Invoice.InvoiceCode.ToLower().Contains(term) ||
                x.Invoice.Apartment.ApartmentCode.ToLower().Contains(term) ||
                (x.Invoice.Resident.User != null && x.Invoice.Resident.User.FullName != null && x.Invoice.Resident.User.FullName.ToLower().Contains(term)));
        }

        return query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.PaymentTransactionId)
            .ToListAsync();
    }

    public Task<PaymentTransaction?> GetResidentPaymentAsync(string userId, int paymentTransactionId) =>
        PaymentQuery().FirstOrDefaultAsync(x =>
            x.PaymentTransactionId == paymentTransactionId &&
            x.Invoice.Resident.UserId == userId &&
            x.Invoice.Status == ApartmentInvoiceStatus.Issued);

    public Task<PaymentTransaction?> GetResidentPaymentForInvoiceAsync(string userId, int invoiceId) =>
        PaymentQuery()
            .Where(x =>
                x.ApartmentInvoiceId == invoiceId &&
                x.Invoice.Resident.UserId == userId &&
                x.Invoice.Status == ApartmentInvoiceStatus.Issued)
            .OrderByDescending(x => x.Status == PaymentTransactionStatus.Confirmed)
            .ThenByDescending(x => x.Status == PaymentTransactionStatus.Pending)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.PaymentTransactionId)
            .FirstOrDefaultAsync();

    public async Task<PaymentTransaction?> GetOrCreateResidentPaymentAsync(string userId, int invoiceId)
    {
        var isOwner = await _context.ApartmentInvoices
            .AnyAsync(x =>
                x.ApartmentInvoiceId == invoiceId &&
                x.Resident.UserId == userId &&
                x.Status == ApartmentInvoiceStatus.Issued);
        if (!isOwner) return null;

        return await GetOrCreatePaymentForInvoiceAsync(invoiceId);
    }

    public async Task<PaymentTransaction?> GetOrCreatePaymentForInvoiceAsync(int invoiceId)
    {
        var invoice = await _context.ApartmentInvoices
            .Include(x => x.Resident)
            .FirstOrDefaultAsync(x =>
                x.ApartmentInvoiceId == invoiceId &&
                x.Status == ApartmentInvoiceStatus.Issued);
        if (invoice == null || invoice.TotalAmount <= 0 ||
            invoice.TotalAmount > VietQrCodeGenerator.MaximumVndAmount)
            return null;

        var existingActive = await PaymentQuery()
            .Where(x => x.ApartmentInvoiceId == invoiceId &&
                        (x.Status == PaymentTransactionStatus.Confirmed || x.Status == PaymentTransactionStatus.Pending))
            .OrderByDescending(x => x.Status == PaymentTransactionStatus.Confirmed)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.PaymentTransactionId)
            .FirstOrDefaultAsync();
        if (existingActive != null) return existingActive;

        var attempt = await CreatePaymentAttemptInternalAsync(invoice);
        if (attempt.Success) return attempt.Payment;

        return await PaymentQuery()
            .Where(x => x.ApartmentInvoiceId == invoiceId &&
                        (x.Status == PaymentTransactionStatus.Confirmed || x.Status == PaymentTransactionStatus.Pending))
            .OrderByDescending(x => x.Status == PaymentTransactionStatus.Confirmed)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.PaymentTransactionId)
            .FirstOrDefaultAsync();
    }

    public async Task<PaymentOperationResult> CreateResidentPaymentAttemptAsync(string userId, int invoiceId)
    {
        if (string.IsNullOrWhiteSpace(userId) || invoiceId <= 0)
            return Failure("Yêu cầu tạo giao dịch không hợp lệ.");

        var invoice = await _context.ApartmentInvoices
            .Include(x => x.Resident)
            .Include(x => x.PaymentTransactions)
            .FirstOrDefaultAsync(x =>
                x.ApartmentInvoiceId == invoiceId &&
                x.Resident.UserId == userId);
        if (invoice == null)
            return Failure("Không tìm thấy hóa đơn cần thanh toán.");

        return await CreatePaymentAttemptInternalAsync(invoice);
    }

    public async Task<PaymentOperationResult> CreatePaymentAttemptForInvoiceAsync(int invoiceId)
    {
        if (invoiceId <= 0)
            return Failure("Không tìm thấy hóa đơn cần thanh toán.");

        var invoice = await _context.ApartmentInvoices
            .Include(x => x.Resident)
            .Include(x => x.PaymentTransactions)
            .FirstOrDefaultAsync(x => x.ApartmentInvoiceId == invoiceId);
        if (invoice == null)
            return Failure("Không tìm thấy hóa đơn cần thanh toán.");

        return await CreatePaymentAttemptInternalAsync(invoice);
    }

    private async Task<PaymentOperationResult> CreatePaymentAttemptInternalAsync(ApartmentInvoice invoice)
    {
        if (invoice.Status != ApartmentInvoiceStatus.Issued)
            return Failure("Chỉ được tạo giao dịch thanh toán cho hóa đơn đã phát hành.");

        if (invoice.TotalAmount <= 0 || invoice.TotalAmount > VietQrCodeGenerator.MaximumVndAmount)
            return Failure("Số tiền hóa đơn không nằm trong phạm vi hỗ trợ tạo mã VietQR.");

        await using var transaction = _context.Database.IsRelational() && _context.Database.CurrentTransaction == null
            ? await _context.Database.BeginTransactionAsync()
            : null;

        try
        {
            var confirmedAmount = await _context.PaymentTransactions
                .Where(x => x.ApartmentInvoiceId == invoice.ApartmentInvoiceId &&
                            x.Status == PaymentTransactionStatus.Confirmed)
                .SumAsync(x => (decimal?)x.Amount) ?? 0m;

            var remainingAmount = invoice.TotalAmount - confirmedAmount;
            if (remainingAmount <= 0)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }

                return Failure("Hóa đơn đã được thanh toán đủ. Không thể tạo thêm giao dịch mới.");
            }

            // Bước 1 trong transaction: Cập nhật có kiểm soát ApartmentInvoice để SQL Server kiểm tra RowVersion
            // (UPDATE [ApartmentInvoices] SET [Status] = @p0 WHERE [ApartmentInvoiceId] = @p1 AND [RowVersion] = @p2)
            // và giữ khóa dòng (X lock) trên hóa đơn trước khi chèn bản ghi PaymentTransaction.
            var invoiceEntry = _context.Entry(invoice);
            invoiceEntry.Property(x => x.Status).IsModified = true;
            if (invoice.RowVersion.Length > 0)
            {
                invoiceEntry.Property(x => x.RowVersion).OriginalValue = invoice.RowVersion.ToArray();
            }

            await _context.SaveChangesAsync();

            // Bước 2 trong cùng transaction: Chèn bản ghi giao dịch Pending mới.
            var payment = new PaymentTransaction
            {
                ApartmentInvoiceId = invoice.ApartmentInvoiceId,
                Invoice = invoice,
                Reference = BuildReference(invoice.ApartmentInvoiceId),
                Amount = remainingAmount,
                Status = PaymentTransactionStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow
            };
            _context.PaymentTransactions.Add(payment);

            await _context.SaveChangesAsync();
            if (transaction != null)
            {
                await transaction.CommitAsync();
            }

            return new PaymentOperationResult(true, null, payment);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync();
            }

            _context.ChangeTracker.Clear();
            _logger.LogInformation(exception, "Xung đột đồng thời khi tạo giao dịch cho hóa đơn {InvoiceId}", invoice.ApartmentInvoiceId);
            return Failure("Hóa đơn đã thay đổi đồng thời. Tải lại dữ liệu trước khi thử lại.");
        }
        catch (DbUpdateException exception)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync();
            }

            _context.ChangeTracker.Clear();
            _logger.LogError(exception, "Không thể khởi tạo giao dịch thanh toán cho hóa đơn {InvoiceId}", invoice.ApartmentInvoiceId);
            return Failure("Không thể khởi tạo giao dịch thanh toán. Vui lòng thử lại.");
        }
    }

    public async Task<PaymentOperationResult> ConfirmSimulatedPaymentAsync(
        int paymentTransactionId,
        string actorUserId,
        byte[] rowVersion)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
            return Failure("Người thực hiện không hợp lệ.");

        var payment = await _context.PaymentTransactions
            .Include(x => x.Invoice)
                .ThenInclude(i => i.PaymentTransactions)
            .FirstOrDefaultAsync(x => x.PaymentTransactionId == paymentTransactionId);
        if (payment == null) return Failure("Không tìm thấy giao dịch.");
        if (payment.Status == PaymentTransactionStatus.Confirmed)
            return new PaymentOperationResult(true, null, payment);
        if (payment.Status == PaymentTransactionStatus.Rejected)
            return Failure("Giao dịch đã bị từ chối, không thể xác nhận.");
        if (payment.Invoice.Status != ApartmentInvoiceStatus.Issued)
            return Failure("Chỉ được xác nhận giao dịch của hóa đơn đã phát hành.");

        var confirmedAmount = payment.Invoice.PaymentTransactions
            .Where(x => x.Status == PaymentTransactionStatus.Confirmed &&
                        x.PaymentTransactionId != payment.PaymentTransactionId)
            .Sum(x => x.Amount);
        if (confirmedAmount >= payment.Invoice.TotalAmount ||
            confirmedAmount + payment.Amount > payment.Invoice.TotalAmount)
        {
            return Failure("Hóa đơn đã được thanh toán đủ hoặc số tiền xác nhận vượt quá số tiền phải trả.");
        }

        if (_context.Database.IsRelational() && rowVersion.Length == 0)
            return Failure("Giao dịch đã thay đổi đồng thời. Tải lại dữ liệu trước khi thử lại.");
        if (rowVersion.Length > 0 && payment.RowVersion.Length > 0 &&
            !payment.RowVersion.AsSpan().SequenceEqual(rowVersion))
            return Failure("Giao dịch đã thay đổi đồng thời. Tải lại dữ liệu trước khi thử lại.");

        var nowUtc = DateTime.UtcNow;
        payment.Status = PaymentTransactionStatus.Confirmed;
        payment.ConfirmedAtUtc = nowUtc;
        payment.ConfirmedByUserId = actorUserId;
        if (rowVersion.Length > 0)
        {
            _context.Entry(payment).Property(x => x.RowVersion).OriginalValue = rowVersion;
        }

        // Cập nhật có kiểm soát ApartmentInvoice trong cùng đơn vị giao dịch để SQL Server
        // kiểm tra và tăng RowVersion của hóa đơn, chặn race condition giữa Confirm, VoidAsync và CreatePaymentAttempt.
        var invoiceEntry = _context.Entry(payment.Invoice);
        invoiceEntry.Property(x => x.Status).IsModified = true;
        if (payment.Invoice.RowVersion.Length > 0)
        {
            invoiceEntry.Property(x => x.RowVersion).OriginalValue = payment.Invoice.RowVersion.ToArray();
        }

        // Phương án A: Khi hóa đơn được thanh toán đủ, tự động chuyển các giao dịch Pending còn lại sang Rejected.
        if (confirmedAmount + payment.Amount >= payment.Invoice.TotalAmount)
        {
            var autoCloseReason = $"Hóa đơn đã được thanh toán đủ qua giao dịch {payment.Reference}";
            var remainingPendings = payment.Invoice.PaymentTransactions
                .Where(x => x.PaymentTransactionId != payment.PaymentTransactionId &&
                            x.Status == PaymentTransactionStatus.Pending)
                .ToList();

            foreach (var pending in remainingPendings)
            {
                pending.Status = PaymentTransactionStatus.Rejected;
                pending.RejectedAtUtc = nowUtc;
                pending.RejectedByUserId = actorUserId;
                pending.RejectionReason = autoCloseReason;
                if (pending.RowVersion.Length > 0)
                {
                    _context.Entry(pending).Property(x => x.RowVersion).OriginalValue = pending.RowVersion.ToArray();
                }
            }
        }

        await using var transaction = _context.Database.IsRelational() && _context.Database.CurrentTransaction == null
            ? await _context.Database.BeginTransactionAsync()
            : null;

        try
        {
            await _context.SaveChangesAsync();
            if (transaction != null)
            {
                await transaction.CommitAsync();
            }

            if (_notificationService != null)
            {
                var residentUserId = await _context.Residents
                    .Where(r => r.ResidentId == payment.Invoice.ResidentId)
                    .Select(r => r.UserId)
                    .FirstOrDefaultAsync();

                if (!string.IsNullOrEmpty(residentUserId))
                {
                    _ = _notificationService.CreateNotificationAsync(
                        residentUserId,
                        "Xác nhận thanh toán thành công",
                        $"Thanh toán số tiền {payment.Amount:N0} đ cho hóa đơn {payment.Invoice.InvoiceCode} đã được xác nhận thành công.",
                        "Thanh toán",
                        $"/MyInvoices/Details/{payment.ApartmentInvoiceId}");
                }
            }

            return new PaymentOperationResult(true, null, payment);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync();
            }

            _logger.LogInformation(exception, "Xung đột xác nhận giao dịch {PaymentId}", paymentTransactionId);
            _context.ChangeTracker.Clear();
            return Failure("Giao dịch đã thay đổi đồng thời. Tải lại dữ liệu trước khi thử lại.");
        }
        catch (DbUpdateException exception)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync();
            }

            _context.ChangeTracker.Clear();
            _logger.LogError(exception, "Không thể xác nhận giao dịch {PaymentId}", paymentTransactionId);
            return Failure("Không thể xác nhận giao dịch. Hóa đơn có thể đã được thanh toán hoặc thay đổi đồng thời.");
        }
    }

    public async Task<PaymentOperationResult> RejectPaymentAsync(
        int paymentTransactionId,
        string actorUserId,
        string reason,
        byte[] rowVersion)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
            return Failure("Người thực hiện không hợp lệ.");

        var trimmedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedReason))
            return Failure("Vui lòng nhập lý do từ chối giao dịch.");
        if (trimmedReason.Length > 500)
            return Failure("Lý do từ chối không được vượt quá 500 ký tự.");

        var payment = await _context.PaymentTransactions
            .Include(x => x.Invoice)
            .FirstOrDefaultAsync(x => x.PaymentTransactionId == paymentTransactionId);
        if (payment == null) return Failure("Không tìm thấy giao dịch.");
        if (payment.Status == PaymentTransactionStatus.Confirmed)
            return Failure("Không thể từ chối giao dịch đã được xác nhận.");
        if (payment.Status == PaymentTransactionStatus.Rejected)
            return Failure("Giao dịch đã ở trạng thái từ chối.");

        if (_context.Database.IsRelational() && rowVersion.Length == 0)
            return Failure("Giao dịch đã thay đổi đồng thời. Tải lại dữ liệu trước khi thử lại.");
        if (rowVersion.Length > 0 && payment.RowVersion.Length > 0 &&
            !payment.RowVersion.AsSpan().SequenceEqual(rowVersion))
            return Failure("Giao dịch đã thay đổi đồng thời. Tải lại dữ liệu trước khi thử lại.");

        payment.Status = PaymentTransactionStatus.Rejected;
        payment.RejectedAtUtc = DateTime.UtcNow;
        payment.RejectedByUserId = actorUserId;
        payment.RejectionReason = trimmedReason;
        if (rowVersion.Length > 0)
        {
            _context.Entry(payment).Property(x => x.RowVersion).OriginalValue = rowVersion;
        }

        var invoiceEntry = _context.Entry(payment.Invoice);
        invoiceEntry.Property(x => x.Status).IsModified = true;
        if (payment.Invoice.RowVersion.Length > 0)
        {
            invoiceEntry.Property(x => x.RowVersion).OriginalValue = payment.Invoice.RowVersion.ToArray();
        }

        await using var transaction = _context.Database.IsRelational() && _context.Database.CurrentTransaction == null
            ? await _context.Database.BeginTransactionAsync()
            : null;

        try
        {
            await _context.SaveChangesAsync();
            if (transaction != null)
            {
                await transaction.CommitAsync();
            }

            if (_notificationService != null)
            {
                var residentUserId = await _context.Residents
                    .Where(r => r.ResidentId == payment.Invoice.ResidentId)
                    .Select(r => r.UserId)
                    .FirstOrDefaultAsync();

                if (!string.IsNullOrEmpty(residentUserId))
                {
                    _ = _notificationService.CreateNotificationAsync(
                        residentUserId,
                        "Giao dịch thanh toán bị từ chối",
                        $"Giao dịch thanh toán số tiền {payment.Amount:N0} đ cho hóa đơn {payment.Invoice.InvoiceCode} bị từ chối: {trimmedReason}.",
                        "Thanh toán",
                        $"/MyInvoices/Details/{payment.ApartmentInvoiceId}");
                }
            }

            return new PaymentOperationResult(true, null, payment);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync();
            }

            _context.ChangeTracker.Clear();
            _logger.LogInformation(exception, "Xung đột từ chối giao dịch {PaymentId}", paymentTransactionId);
            return Failure("Giao dịch đã thay đổi đồng thời. Tải lại dữ liệu trước khi thử lại.");
        }
        catch (DbUpdateException exception)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync();
            }

            _context.ChangeTracker.Clear();
            _logger.LogError(exception, "Không thể từ chối giao dịch {PaymentId}", paymentTransactionId);
            return Failure("Không thể từ chối giao dịch. Vui lòng thử lại.");
        }
    }

    private static string BuildReference(int invoiceId) =>
        $"AH{invoiceId:D5}{Guid.NewGuid():N}"[..25].ToUpperInvariant();

    private IQueryable<PaymentTransaction> PaymentQuery() =>
        _context.PaymentTransactions.AsNoTracking()
            .Include(x => x.Invoice).ThenInclude(x => x.Apartment).ThenInclude(x => x.Building)
            .Include(x => x.Invoice).ThenInclude(x => x.Resident).ThenInclude(x => x.User)
            .Include(x => x.ConfirmedByUser)
            .Include(x => x.RejectedByUser);

    private static PaymentOperationResult Failure(string message) => new(false, message, null);
}
