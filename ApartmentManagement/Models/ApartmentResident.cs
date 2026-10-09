using System.ComponentModel.DataAnnotations;
using ApartmentManagement.Common.Validation;

namespace ApartmentManagement.Models;

public class ApartmentResident
{
    public int ApartmentResidentId { get; set; }
    public int ApartmentId { get; set; }
    public int ResidentId { get; set; }

    [Required, StringLength(ResidentValidation.RelationshipMaxLength)]
    public string Relationship { get; set; } = string.Empty;

    public DateTime MoveInDate { get; set; }
    public DateTime? MoveOutDate { get; set; }
    public bool IsOwner { get; set; }
    public Apartment Apartment { get; set; } = null!;
    public Resident Resident { get; set; } = null!;
}
