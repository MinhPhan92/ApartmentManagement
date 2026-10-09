using ApartmentManagement.Models;

namespace ApartmentManagement.Services;

public interface IContractService
{
    Task<List<ApartmentContract>> GetAllAsync();
    Task<ContractPageResult> GetPageAsync(string? searchTerm, ApartmentContractStatus? status, int page, int pageSize);
    Task<ApartmentContract?> GetByIdAsync(int id);
    Task<List<ApartmentContract>> GetForResidentUserAsync(string userId);
    Task<ContractServiceResult> CreateDraftAsync(ContractDraftInput input, string actorUserId);
    Task<ContractServiceResult> UpdateDraftAsync(
        int id, ContractDraftInput input, string actorUserId, byte[] expectedRowVersion);
    Task<ContractServiceResult> ActivateAsync(int id, string actorUserId, byte[] expectedRowVersion);
    Task<ContractServiceResult> CompleteAsync(
        int id, string actorUserId, byte[] expectedRowVersion, string? reason = null);
    Task<ContractServiceResult> TerminateAsync(
        int id, string actorUserId, byte[] expectedRowVersion, string reason);
    Task<ContractServiceResult> CancelAsync(
        int id, string actorUserId, byte[] expectedRowVersion, string reason);
}
