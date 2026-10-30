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

public enum BillingFailureReason
{
    None = 0,
    InvalidInput,
    AlreadyExists,
    MissingOrInvalidOccupancy,
    MissingTariff,
    MissingMeterReading,
    InvalidArea,
    TariffCalculationError,
    PersistenceError
}

public sealed record BillingOperationResult(
    bool Success,
    string? ErrorMessage,
    ApartmentInvoice? Invoice,
    BillingFailureReason FailureReason = BillingFailureReason.None)
{
    public void Deconstruct(out bool success, out string? errorMessage, out ApartmentInvoice? invoice)
    {
        success = Success;
        errorMessage = ErrorMessage;
        invoice = Invoice;
    }
}

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

public enum BatchBillingItemStatus
{
    Created,
    AlreadyExists,
    Skipped,
    Failed
}

public sealed record BatchBillingItemResult(
    int ApartmentId,
    string ApartmentCode,
    BatchBillingItemStatus Status,
    string Message,
    int? InvoiceId = null,
    string? InvoiceCode = null,
    decimal? TotalAmount = null);

public sealed record BatchBillingOperationResult(
    bool Success,
    string? ErrorMessage,
    int TotalScanned,
    int CreatedCount,
    int AlreadyExistsCount,
    int SkippedCount,
    int FailedCount,
    IReadOnlyList<BatchBillingItemResult> Items);

public sealed record BatchMeterReadingItemInput(
    int ApartmentId,
    string ApartmentCode,
    decimal ElectricityOldReading,
    decimal? ElectricityNewReading,
    decimal WaterOldReading,
    decimal? WaterNewReading);

public sealed record BatchMeterReadingSaveInput(
    int BuildingId,
    int BillingYear,
    int BillingMonth,
    IReadOnlyList<BatchMeterReadingItemInput> Items);

public sealed record BatchMeterReadingItemResult(
    int ApartmentId,
    string ApartmentCode,
    bool Success,
    string? Message);

public sealed record BatchMeterReadingOperationResult(
    bool Success,
    string? ErrorMessage,
    int TotalProcessed,
    int SavedCount,
    int SkippedCount,
    int FailedCount,
    IReadOnlyList<BatchMeterReadingItemResult> Items);

public sealed record BatchIssueItemInput(
    int InvoiceId,
    byte[] RowVersion);

public sealed record BatchIssueInput(
    int BuildingId,
    int BillingYear,
    int BillingMonth,
    IReadOnlyList<BatchIssueItemInput> Invoices);

public enum BatchIssueItemStatus
{
    Issued,
    AlreadyIssued,
    ConcurrencyConflict,
    InvalidStatus,
    Failed
}

public sealed record BatchIssueItemResult(
    int InvoiceId,
    string InvoiceCode,
    string ApartmentCode,
    BatchIssueItemStatus Status,
    string Message,
    decimal TotalAmount);

public sealed record BatchIssueOperationResult(
    bool Success,
    string? ErrorMessage,
    int TotalSelected,
    int IssuedCount,
    int AlreadyIssuedCount,
    int FailedCount,
    IReadOnlyList<BatchIssueItemResult> Items);

public sealed record DraftInvoiceDto(
    int InvoiceId,
    string InvoiceCode,
    string ApartmentCode,
    string ResidentName,
    decimal ManagementFee,
    decimal ElectricityFee,
    decimal WaterFee,
    decimal TotalAmount,
    byte[] RowVersion,
    string RowVersionToken);

public sealed record VehicleParkingChargeItem(
    int VehicleId,
    string LicensePlate,
    string VehicleType,
    decimal MonthlyRate);

public sealed record ParkingFeeCalculationResult(
    decimal TotalAmount,
    int ActiveVehicleCount,
    string SummaryDescription,
    IReadOnlyList<VehicleParkingChargeItem> Items);
