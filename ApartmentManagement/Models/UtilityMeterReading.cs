namespace ApartmentManagement.Models;

public sealed class UtilityMeterReading
{
    public int UtilityMeterReadingId { get; set; }
    public int ApartmentId { get; set; }
    public FeeChargeType UtilityType { get; set; }
    public int BillingYear { get; set; }
    public int BillingMonth { get; set; }
    public decimal PreviousReading { get; set; }
    public decimal CurrentReading { get; set; }
    public DateTime ReadAtUtc { get; set; }
    public string RecordedByUserId { get; set; } = string.Empty;

    public Apartment Apartment { get; set; } = null!;
    public ApplicationUser RecordedByUser { get; set; } = null!;
    public ICollection<ApartmentInvoiceLine> InvoiceLines { get; set; } = new List<ApartmentInvoiceLine>();
}
