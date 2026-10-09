namespace ApartmentManagement.Models;

public class IncidentTicketStatusHistory
{
    public int IncidentTicketStatusHistoryId { get; set; }
    public int IncidentTicketId { get; set; }
    public IncidentTicketStatus? PreviousStatus { get; set; }
    public IncidentTicketStatus NewStatus { get; set; }
    public DateTime ChangedAtUtc { get; set; }
    public string ChangedByUserId { get; set; } = string.Empty;

    public IncidentTicket IncidentTicket { get; set; } = null!;
    public ApplicationUser ChangedByUser { get; set; } = null!;
}
