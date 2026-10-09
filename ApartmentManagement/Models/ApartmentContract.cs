namespace ApartmentManagement.Models;

public class ApartmentContract
{
    public int ApartmentContractId { get; set; }
    public string ContractCode { get; private set; } = string.Empty;
    public string ContractCodeNormalized { get; private set; } = string.Empty;
    public int ApartmentId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal MonthlyRent { get; set; }
    public decimal DepositAmount { get; set; }
    public ApartmentContractStatus Status { get; set; } = ApartmentContractStatus.Draft;
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public Apartment Apartment { get; set; } = null!;
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public ApplicationUser? UpdatedByUser { get; set; }
    public ICollection<ContractParty> Parties { get; set; } = new List<ContractParty>();
    public ICollection<ContractStatusHistory> StatusHistory { get; set; } = new List<ContractStatusHistory>();

    internal void SetContractCode(string contractCode)
    {
        ContractCode = contractCode.Trim();
        ContractCodeNormalized = ContractCode.ToUpperInvariant();
    }
}
