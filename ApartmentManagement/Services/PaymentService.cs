using ApartmentManagement.Data;
using ApartmentManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Services;

public sealed class PaymentService : IPaymentService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(ApplicationDbContext context, ILogger<PaymentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task<List<PaymentTransaction>> GetTransactionsAsync() =>
        PaymentQuery().OrderByDescending(x => x.CreatedAtUtc).ToListAsync();

    public Task<PaymentTransaction?> GetResidentPaymentAsync(string userId, int paymentTransactionId) =>
        PaymentQuery().FirstOrDefaultAsync(x =>
            x.PaymentTransactionId == paymentTransactionId &&
            x.Invoice.Resident.UserId == userId &&
            x.Invoice.Status == ApartmentInvoiceStatus.Issued);

    public Task<PaymentTransaction?> GetResidentPaymentForInvoiceAsync(string userId, int invoiceId) =>
        PaymentQuery().FirstOrDefaultAsync(x =>
            x.ApartmentInvoiceId == invoiceId &&
            x.Invoice.Resident.UserId == userId &&
            x.Invoice.Status == ApartmentInvoiceStatus.Issued);

    public async Task<PaymentTransaction?> GetOrCreateResidentPaymentAsync(string userId, int invoiceId)
    {
        var invoice = await _context.ApartmentInvoices
            .Include(x => x.Resident)
            .FirstOrDefaultAsync(x =>
                x.ApartmentInvoiceId == invoiceId &&
                x.Resident.UserId == userId &&
                x.Status == ApartmentInvoiceStatus.Issued);
        if (invoice == null || invoice.TotalAmount <= 0 ||
            invoice.TotalAmount > VietQrCodeGenerator.MaximumVndAmount)
            return null;

        var existing = await PaymentQuery()
            .FirstOrDefaultAsync(x => x.ApartmentInvoiceId == invoiceId);
        if (existing != null) return existing;

        var payment = new PaymentTransaction
        {
            ApartmentInvoiceId = invoiceId,
            Invoice = invoice,
            Reference = $"AH{Guid.NewGuid():N}"[..25].ToUpperInvariant(),
            Amount = invoice.TotalAmount,
            Status = PaymentTransactionStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.PaymentTransactions.Add(payment);

        try
        {
            await _context.SaveChangesAsync();
            return payment;
        }
        catch (DbUpdateException exception)
        {
            _context.Entry(payment).State = EntityState.Detached;
            var concurrentPayment = await PaymentQuery()
                .FirstOrDefaultAsync(x => x.ApartmentInvoiceId == invoiceId);
            if (concurrentPayment != null) return concurrentPayment;

            _logger.LogError(exception, "Không thể tạo giao dịch giả lập cho hóa đơn {InvoiceId}", invoiceId);
            return null;
        }
    }

    public async Task<PaymentOperationResult> ConfirmSimulatedPaymentAsync(
        int paymentTransactionId,
        string actorUserId,
        byte[] rowVersion)
    {
        var payment = await _context.PaymentTransactions
            .Include(x => x.Invoice)
            .FirstOrDefaultAsync(x => x.PaymentTransactionId == paymentTransactionId);
        if (payment == null) return Failure("Không tìm thấy giao dịch.");
        if (payment.Status == PaymentTransactionStatus.Confirmed)
            return new PaymentOperationResult(true, null, payment);
        if (payment.Invoice.Status != ApartmentInvoiceStatus.Issued)
            return Failure("Chỉ được xác nhận giao dịch của hóa đơn đã phát hành.");
        if (_context.Database.IsRelational() &&
            (rowVersion.Length == 0 || !payment.RowVersion.AsSpan().SequenceEqual(rowVersion)))
            return Failure("Giao dịch đã thay đổi đồng thời. Tải lại dữ liệu trước khi thử lại.");

        payment.Status = PaymentTransactionStatus.Confirmed;
        payment.ConfirmedAtUtc = DateTime.UtcNow;
        payment.ConfirmedByUserId = actorUserId;
        if (_context.Database.IsRelational())
            _context.Entry(payment).Property(x => x.RowVersion).OriginalValue = rowVersion;

        try
        {
            await _context.SaveChangesAsync();
            return new PaymentOperationResult(true, null, payment);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            _logger.LogInformation(exception, "Xung đột xác nhận giao dịch {PaymentId}", paymentTransactionId);
            _context.Entry(payment).State = EntityState.Detached;
            var current = await PaymentQuery()
                .FirstOrDefaultAsync(x => x.PaymentTransactionId == paymentTransactionId);
            return current?.Status == PaymentTransactionStatus.Confirmed
                ? new PaymentOperationResult(true, null, current)
                : Failure("Giao dịch đã thay đổi đồng thời. Tải lại dữ liệu trước khi thử lại.");
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Không thể xác nhận giao dịch {PaymentId}", paymentTransactionId);
            return Failure("Không thể xác nhận giao dịch giả lập.");
        }
    }

    private IQueryable<PaymentTransaction> PaymentQuery() =>
        _context.PaymentTransactions.AsNoTracking()
            .Include(x => x.Invoice).ThenInclude(x => x.Apartment).ThenInclude(x => x.Building)
            .Include(x => x.Invoice).ThenInclude(x => x.Resident).ThenInclude(x => x.User)
            .Include(x => x.ConfirmedByUser);

    private static PaymentOperationResult Failure(string message) => new(false, message, null);
}
