using System.ComponentModel.DataAnnotations;

namespace ApartmentManagement.Models;

public class TechnicalIncidentStatusHistory
{
    public int TechnicalIncidentStatusHistoryId { get; set; }

    [Required]
    public int TechnicalIncidentId { get; set; }

    [StringLength(30)]
    public string? PreviousStatus { get; set; }

    [Required]
    [StringLength(30)]
    public string NewStatus { get; set; } = string.Empty;

    [Required]
    [StringLength(450)]
    public string ChangedByUserId { get; set; } = string.Empty;

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    [StringLength(1000)]
    public string? Note { get; set; }

    [StringLength(450)]
    public string? PreviousTechnicianUserId { get; set; }

    [StringLength(450)]
    public string? NewTechnicianUserId { get; set; }

    // Navigations
    public TechnicalIncident Incident { get; set; } = null!;
    public ApplicationUser ChangedByUser { get; set; } = null!;
    public ApplicationUser? PreviousTechnicianUser { get; set; }
    public ApplicationUser? NewTechnicianUser { get; set; }
}

