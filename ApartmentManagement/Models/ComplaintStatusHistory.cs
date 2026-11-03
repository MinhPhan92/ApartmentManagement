using System.ComponentModel.DataAnnotations;

namespace ApartmentManagement.Models;

public class ComplaintStatusHistory
{
    public int ComplaintStatusHistoryId { get; set; }

    [Required]
    public int ComplaintId { get; set; }

    [StringLength(50)]
    public string? PreviousStatus { get; set; }

    [Required]
    [StringLength(50)]
    public string NewStatus { get; set; } = string.Empty;

    [Required]
    [StringLength(450)]
    public string ChangedByUserId { get; set; } = string.Empty;

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    [StringLength(1000)]
    public string? Note { get; set; }

    public bool IsInternal { get; set; } = false;

    // Navigations
    public Complaint Complaint { get; set; } = null!;
    public ApplicationUser ChangedByUser { get; set; } = null!;
}

