namespace ApartmentManagement.Models;

public enum ApartmentInvoiceStatus
{
    Draft,
    Issued,
    Void
}

public sealed class ApartmentInvoice
{
    public int ApartmentInvoiceId { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public int ApartmentId { get; set; }
    public int ResidentId { get; set; }
    public int BillingYear { get; set; }
    public int BillingMonth { get; set; }
    public DateTime DueDate { get; set; }
    public decimal ApartmentArea { get; set; }
    public decimal TotalAmount { get; set; }
    public ApartmentInvoiceStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime? IssuedAtUtc { get; set; }
    public string? IssuedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public Apartment Apartment { get; set; } = null!;
    public Resident Resident { get; set; } = null!;
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public ApplicationUser? IssuedByUser { get; set; }
    public ICollection<ApartmentInvoiceLine> Lines { get; set; } = new List<ApartmentInvoiceLine>();
    public ICollection<PaymentTransaction> PaymentTransactions { get; set; } = new List<PaymentTransaction>();
}
