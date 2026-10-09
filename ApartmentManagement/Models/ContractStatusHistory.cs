namespace ApartmentManagement.Models;

public class ContractStatusHistory
{
    public int ContractStatusHistoryId { get; set; }
    public int ApartmentContractId { get; set; }
    public ApartmentContractStatus? PreviousStatus { get; set; }
    public ApartmentContractStatus NewStatus { get; set; }
    public DateTime ChangedAtUtc { get; set; }
    public string ChangedByUserId { get; set; } = string.Empty;
    public string? Reason { get; set; }

    public ApartmentContract ApartmentContract { get; set; } = null!;
    public ApplicationUser ChangedByUser { get; set; } = null!;
}
