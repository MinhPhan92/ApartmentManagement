using ApartmentManagement.Models;

namespace ApartmentManagement.Services;

public interface IBillingService
{
    Task<List<FeeTariff>> GetTariffsAsync(int? buildingId = null);
    Task<List<ApartmentInvoice>> GetInvoicesAsync();
    Task<List<ApartmentInvoice>> GetResidentInvoicesAsync(string userId);
    Task<ApartmentInvoice?> GetResidentInvoiceAsync(string userId, int invoiceId);
    Task<ApartmentInvoice?> GetInvoiceAsync(int invoiceId);
    Task<BillingOperationResult> CreateTariffAsync(FeeTariffInput input, string actorUserId);
    Task<BillingOperationResult> RecordReadingAsync(MeterReadingInput input, string actorUserId);
    Task<BillingOperationResult> GenerateDraftAsync(
        int apartmentId, int year, int month, DateTime dueDate, string actorUserId);
    Task<BillingOperationResult> IssueAsync(int invoiceId, string actorUserId, byte[] rowVersion);
}
