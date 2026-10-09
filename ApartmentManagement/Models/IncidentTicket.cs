namespace ApartmentManagement.Models;

public class IncidentTicket
{
    public int IncidentTicketId { get; set; }
    public string TicketCode { get; set; } = string.Empty;
    public int ApartmentId { get; set; }
    public int ResidentId { get; set; }
    public IncidentCategory Category { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IncidentTicketStatus Status { get; set; } = IncidentTicketStatus.Submitted;
    public DateTime CreatedAtUtc { get; set; }
    public string? AssignedToUserId { get; set; }
    public DateTime? AssignedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public byte? Rating { get; set; }
    public string? ResidentFeedback { get; set; }
    public DateTime? RatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public Apartment Apartment { get; set; } = null!;
    public Resident Resident { get; set; } = null!;
    public ApplicationUser? AssignedToUser { get; set; }
    public ICollection<IncidentTicketStatusHistory> StatusHistory { get; set; } = new List<IncidentTicketStatusHistory>();
}
