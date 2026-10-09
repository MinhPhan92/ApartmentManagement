namespace ApartmentManagement.Models;

public sealed class FeeTariff
{
    public int FeeTariffId { get; set; }
    public int BuildingId { get; set; }
    public FeeChargeType ChargeType { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public decimal MonthlyRatePerSquareMeter { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;

    public Building Building { get; set; } = null!;
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public ICollection<FeeTariffTier> Tiers { get; set; } = new List<FeeTariffTier>();
    public ICollection<ApartmentInvoiceLine> InvoiceLines { get; set; } = new List<ApartmentInvoiceLine>();
}
