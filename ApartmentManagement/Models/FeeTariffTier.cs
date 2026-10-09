namespace ApartmentManagement.Models;

public sealed class FeeTariffTier
{
    public int FeeTariffTierId { get; set; }
    public int FeeTariffId { get; set; }
    public int TierOrder { get; set; }
    public decimal? UpperConsumption { get; set; }
    public decimal UnitRate { get; set; }

    public FeeTariff FeeTariff { get; set; } = null!;
}
