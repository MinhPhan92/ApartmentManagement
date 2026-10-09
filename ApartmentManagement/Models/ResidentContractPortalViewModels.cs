namespace ApartmentManagement.Models;

public sealed record ResidentContractListViewModel(
    IReadOnlyList<ResidentContractSummaryViewModel> Contracts);

public sealed record ResidentContractSummaryViewModel(
    int Id,
    string ContractCode,
    string Apartment,
    DateTime StartDate,
    DateTime? EndDate,
    decimal MonthlyRent,
    decimal DepositAmount,
    ApartmentContractStatus Status);

public sealed record ResidentContractDetailsViewModel(
    string ContractCode,
    string Apartment,
    DateTime StartDate,
    DateTime? EndDate,
    decimal MonthlyRent,
    decimal DepositAmount,
    ApartmentContractStatus Status,
    IReadOnlyList<ResidentContractPartyViewModel> Parties,
    IReadOnlyList<ResidentContractHistoryViewModel> History);

public sealed record ResidentContractPartyViewModel(string ResidentName, ContractPartyRole Role);

public sealed record ResidentContractHistoryViewModel(
    ApartmentContractStatus? PreviousStatus,
    ApartmentContractStatus NewStatus,
    DateTime ChangedAtUtc);
