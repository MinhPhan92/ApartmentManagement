namespace ApartmentManagement.Models;

public sealed class ApartmentInvoiceLine
{
    public int ApartmentInvoiceLineId { get; set; }
    public int ApartmentInvoiceId { get; set; }
    public FeeChargeType ChargeType { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitRate { get; set; }
    public decimal Amount { get; set; }
    public decimal? PreviousReading { get; set; }
    public decimal? CurrentReading { get; set; }
    public string? RateBreakdown { get; set; }
    public int? SourceTariffId { get; set; }
    public int? SourceMeterReadingId { get; set; }

    public ApartmentInvoice Invoice { get; set; } = null!;
    public FeeTariff? SourceTariff { get; set; }
    public UtilityMeterReading? SourceMeterReading { get; set; }
}
