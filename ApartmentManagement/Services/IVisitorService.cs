using ApartmentManagement.Models;

namespace ApartmentManagement.Services;

public interface IVisitorService
{
    Task<List<VisitorPass>> GetAllVisitorPassesAsync(DateTime? date = null, string? status = null);
    Task<List<VisitorPass>> GetResidentVisitorPassesAsync(string userId);
    Task<VisitorPass?> GetVisitorPassByIdAsync(int id);
    Task<(bool Success, string? ErrorMessage)> RegisterVisitorAsync(string userId, int apartmentId, string visitorName, string? phone, string? idCard, DateTime arrivalDate, string? purpose);
    Task<(bool Success, string? ErrorMessage)> CheckInVisitorAsync(int visitorPassId);
    Task<(bool Success, string? ErrorMessage)> CheckOutVisitorAsync(int visitorPassId);
    Task<(bool Success, string? ErrorMessage)> CancelVisitorPassAsync(string userId, int visitorPassId, bool isManager = false);
}
