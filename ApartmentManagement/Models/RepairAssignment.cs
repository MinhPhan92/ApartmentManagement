using System.ComponentModel.DataAnnotations;

namespace ApartmentManagement.Models;

public class RepairAssignment
{
    public int AssignmentId { get; set; }

    [Required]
    public int RepairRequestId { get; set; }

    [Required]
    [StringLength(450)]
    public string TechnicianUserId { get; set; } = string.Empty;

    [Display(Name = "Thời điểm phân công")]
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    [Display(Name = "Thời điểm hoàn thành")]
    public DateTime? CompletedAt { get; set; }

    [Required]
    [StringLength(30)]
    [Display(Name = "Trạng thái phân công")]
    public string Status { get; set; } = RepairAssignmentStatus.Assigned;

    [StringLength(2000)]
    [Display(Name = "Ghi chú kỹ thuật")]
    public string? Note { get; set; }

    // Navigations
    public RepairRequest RepairRequest { get; set; } = null!;
    public ApplicationUser TechnicianUser { get; set; } = null!;
}
