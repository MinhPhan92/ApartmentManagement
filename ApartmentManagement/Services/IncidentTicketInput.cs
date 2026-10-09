using ApartmentManagement.Models;

namespace ApartmentManagement.Services;

public sealed record IncidentTicketInput(
    int ApartmentId,
    IncidentCategory Category,
    string Title,
    string Description);

public sealed record IncidentTicketResult(
    bool Success,
    string? ErrorMessage,
    IncidentTicket? Ticket);
