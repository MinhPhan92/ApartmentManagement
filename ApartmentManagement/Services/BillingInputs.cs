using ApartmentManagement.Models;

namespace ApartmentManagement.Services;

public sealed record FeeTariffInput(
    int BuildingId,
    FeeChargeType ChargeType,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    decimal MonthlyRatePerSquareMeter,
    IReadOnlyList<FeeTariffTierInput> Tiers);

public sealed record FeeTariffTierInput(
    int TierOrder,
    decimal? UpperConsumption,
    decimal UnitRate);

public sealed record MeterReadingInput(
    int ApartmentId,
    FeeChargeType UtilityType,
    int BillingYear,
    int BillingMonth,
    decimal PreviousReading,
    decimal CurrentReading);

public sealed record BillingOperationResult(bool Success, string? ErrorMessage, ApartmentInvoice? Invoice);

public sealed record InvoiceFilterCriteria(
    int? BuildingId = null,
    int? Year = null,
    int? Month = null,
    ApartmentInvoiceStatus? Status = null,
    bool? IsPaid = null,
    string? SearchTerm = null);

public sealed record BillingSummaryDto(
    decimal TotalInvoiced,
    decimal TotalCollected,
    decimal TotalReceivable,
    int TotalInvoices,
    int DraftCount,
    int IssuedCount,
    int PaidCount,
    int UnpaidCount,
    int VoidCount);
