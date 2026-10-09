using ApartmentManagement.Models;

namespace ApartmentManagement.Services;

public sealed record ContractPageResult(
    IReadOnlyList<ApartmentContract> Contracts,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}
