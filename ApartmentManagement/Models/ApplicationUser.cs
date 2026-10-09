using System.ComponentModel.DataAnnotations;
using ApartmentManagement.Common.Validation;
using Microsoft.AspNetCore.Identity;

namespace ApartmentManagement.Models;

public class ApplicationUser : IdentityUser
{
    [Required, StringLength(ResidentValidation.FullNameMaxLength)]
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Resident? Resident { get; set; }
    public ICollection<IncidentTicket> AssignedIncidentTickets { get; set; } = new List<IncidentTicket>();
}
