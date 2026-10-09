namespace ApartmentManagement.Models;

public enum PaymentTransactionStatus
{
    Pending,
    Confirmed
}

public sealed class PaymentTransaction
{
    public int PaymentTransactionId { get; set; }
    public int ApartmentInvoiceId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentTransactionStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public string? ConfirmedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ApartmentInvoice Invoice { get; set; } = null!;
    public ApplicationUser? ConfirmedByUser { get; set; }
}
