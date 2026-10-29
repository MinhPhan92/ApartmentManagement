using ApartmentManagement.Models;

namespace ApartmentManagement.Services;

public interface IBillingService
{
    Task<List<FeeTariff>> GetTariffsAsync(int? buildingId = null);
    Task<FeeTariff?> GetTariffAsync(int tariffId);
    Task<BillingOperationResult> CloseTariffAsync(int tariffId, DateTime effectiveTo, string actorUserId);
    Task<List<ApartmentInvoice>> GetInvoicesAsync(InvoiceFilterCriteria? criteria = null);
    Task<BillingSummaryDto> GetBillingSummaryAsync(int? buildingId = null, int? year = null, int? month = null);
    Task<List<ApartmentInvoice>> GetResidentInvoicesAsync(string userId);
    Task<ApartmentInvoice?> GetResidentInvoiceAsync(string userId, int invoiceId);
    Task<ApartmentInvoice?> GetInvoiceAsync(int invoiceId);
    Task<BillingOperationResult> CreateTariffAsync(FeeTariffInput input, string actorUserId);
    Task<BillingOperationResult> RecordReadingAsync(MeterReadingInput input, string actorUserId);
    Task<BillingOperationResult> GenerateDraftAsync(
        int apartmentId, int year, int month, DateTime dueDate, string actorUserId);
    Task<BatchBillingOperationResult> GenerateBuildingDraftsAsync(
        int buildingId, int year, int month, DateTime dueDate, string actorUserId);
    Task<BatchMeterReadingViewModel> GetBatchMeterReadingSheetAsync(int buildingId, int billingYear, int billingMonth);
    Task<BatchMeterReadingOperationResult> SaveBatchMeterReadingsAsync(BatchMeterReadingSaveInput input, string actorUserId);
    Task<BillingOperationResult> IssueAsync(int invoiceId, string actorUserId, byte[] rowVersion);
    Task<BillingOperationResult> VoidAsync(int invoiceId, string actorUserId, byte[] rowVersion, string? reason = null);
}
