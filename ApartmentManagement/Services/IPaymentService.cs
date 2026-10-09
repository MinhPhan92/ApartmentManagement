using ApartmentManagement.Models;

namespace ApartmentManagement.Services;

public interface IPaymentService
{
    Task<List<PaymentTransaction>> GetTransactionsAsync();
    Task<PaymentTransaction?> GetResidentPaymentAsync(string userId, int paymentTransactionId);
    Task<PaymentTransaction?> GetResidentPaymentForInvoiceAsync(string userId, int invoiceId);
    Task<PaymentTransaction?> GetOrCreateResidentPaymentAsync(string userId, int invoiceId);
    Task<PaymentOperationResult> ConfirmSimulatedPaymentAsync(
        int paymentTransactionId,
        string actorUserId,
        byte[] rowVersion);
}

public sealed record PaymentOperationResult(bool Success, string? ErrorMessage, PaymentTransaction? Payment);
