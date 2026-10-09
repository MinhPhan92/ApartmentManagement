using ApartmentManagement.Models;

namespace ApartmentManagement.Services;

public static class ProgressiveTariffCalculator
{
    public static decimal Calculate(
        decimal consumption,
        IReadOnlyCollection<FeeTariffTier> tiers)
    {
        if (consumption < 0) throw new ArgumentOutOfRangeException(nameof(consumption));
        if (tiers.Count == 0) throw new ArgumentException("Cần cấu hình ít nhất một bậc giá.", nameof(tiers));

        var orderedTiers = tiers.OrderBy(x => x.TierOrder).ToArray();
        decimal previousLimit = 0;
        decimal total = 0;

        for (var index = 0; index < orderedTiers.Length; index++)
        {
            var tier = orderedTiers[index];
            if (tier.TierOrder != index + 1 || tier.UnitRate < 0)
                throw new ArgumentException("Cấu hình bậc giá không liên tục hoặc có đơn giá âm.", nameof(tiers));

            if (tier.UpperConsumption is decimal upperLimit)
            {
                if (upperLimit <= previousLimit)
                    throw new ArgumentException("Ngưỡng tiêu thụ phải tăng dần.", nameof(tiers));
                var tierQuantity = Math.Max(0, Math.Min(consumption, upperLimit) - previousLimit);
                total += tierQuantity * tier.UnitRate;
                previousLimit = upperLimit;
                if (index == orderedTiers.Length - 1)
                    throw new ArgumentException("Bậc cuối phải không giới hạn.", nameof(tiers));
            }
            else
            {
                if (index != orderedTiers.Length - 1)
                    throw new ArgumentException("Chỉ bậc cuối được không giới hạn.", nameof(tiers));
                total += Math.Max(0, consumption - previousLimit) * tier.UnitRate;
            }
        }

        return decimal.Round(total, 0, MidpointRounding.AwayFromZero);
    }
}
