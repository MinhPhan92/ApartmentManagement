namespace ApartmentManagement.Models;

public class ContractParty
{
    public int ApartmentContractId { get; set; }
    public int ResidentId { get; set; }
    public ContractPartyRole Role { get; set; }

    public ApartmentContract ApartmentContract { get; set; } = null!;
    public Resident Resident { get; set; } = null!;
}
