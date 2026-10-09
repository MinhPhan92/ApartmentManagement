using ApartmentManagement.Models;

namespace ApartmentManagement.Services;

public sealed record ContractDraftInput
{
    public string ContractCode { get; init; } = string.Empty;
    public int ApartmentId { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public decimal MonthlyRent { get; init; }
    public decimal DepositAmount { get; init; }
    public IReadOnlyList<ContractPartyInput> Parties { get; init; } = [];
}

public sealed record ContractPartyInput(int ResidentId, ContractPartyRole Role);

public sealed record ContractServiceResult(
    bool Success,
    string? ErrorMessage,
    ApartmentContract? Contract);
