using System.ComponentModel.DataAnnotations;
using ApartmentManagement.Common.Validation;

namespace ApartmentManagement.Models;

public class Resident
{
    public int ResidentId { get; set; }
    public string UserId { get; set; } = string.Empty;

    [Required, StringLength(ResidentValidation.CitizenIdMaxLength)]
    [RegularExpression(ResidentValidation.CitizenIdPattern)]
    public string CitizenId { get; set; } = string.Empty;

    [ValidDateOfBirth]
    public DateTime? DateOfBirth { get; set; }

    [StringLength(ResidentValidation.GenderMaxLength)]
    public string? Gender { get; set; }

    [StringLength(ResidentValidation.AddressMaxLength)]
    public string? Address { get; set; }

    [StringLength(ResidentValidation.PhoneMaxLength)]
    [RegularExpression(ResidentValidation.PhonePattern)]
    public string? EmergencyContact { get; set; }

    public ApplicationUser User { get; set; } = null!;
    public ICollection<ApartmentResident> ApartmentResidents { get; set; } = new List<ApartmentResident>();
    public ICollection<ContractParty> ContractParties { get; set; } = new List<ContractParty>();
    public ICollection<IncidentTicket> IncidentTickets { get; set; } = new List<IncidentTicket>();
    public ICollection<ApartmentInvoice> Invoices { get; set; } = new List<ApartmentInvoice>();
}
