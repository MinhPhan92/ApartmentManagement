using ApartmentManagement.Models;

namespace ApartmentManagement.Services;

public interface IIncidentTicketService
{
    Task<List<IncidentTicket>> GetForResidentAsync(string userId);
    Task<IncidentTicket?> GetForResidentByIdAsync(string userId, int id);
    Task<List<IncidentTicket>> GetStaffQueueAsync(string userId);
    Task<IncidentTicket?> GetForStaffByIdAsync(string userId, int id);
    Task<IncidentTicketResult> CreateAsync(string residentUserId, IncidentTicketInput input);
    Task<IncidentTicketResult> ClaimAsync(int id, string staffUserId, byte[] rowVersion);
    Task<IncidentTicketResult> CompleteAsync(int id, string staffUserId, byte[] rowVersion);
    Task<IncidentTicketResult> RateAsync(
        int id, string residentUserId, byte rating, string? feedback, byte[] rowVersion);
}
