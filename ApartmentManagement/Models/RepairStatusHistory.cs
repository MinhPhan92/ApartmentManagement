using System.ComponentModel.DataAnnotations;

namespace ApartmentManagement.Models;

public class RepairStatusHistory
{
    public int RepairStatusHistoryId { get; set; }

    [Required]
    public int RepairRequestId { get; set; }

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

    // Navigations
    public RepairRequest RepairRequest { get; set; } = null!;
    public ApplicationUser ChangedByUser { get; set; } = null!;
}
