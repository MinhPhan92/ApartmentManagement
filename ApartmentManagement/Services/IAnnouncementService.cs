using ApartmentManagement.Models;

namespace ApartmentManagement.Services;

public interface IAnnouncementService
{
    Task<List<Announcement>> GetAllAnnouncementsAsync(int? buildingId = null);
    Task<List<Announcement>> GetAnnouncementsForResidentAsync(string userId);
    Task<Announcement?> GetAnnouncementByIdAsync(int id);
    Task<(bool Success, string? ErrorMessage)> CreateAnnouncementAsync(string userId, Announcement announcement);
    Task<(bool Success, string? ErrorMessage)> DeleteAnnouncementAsync(int id);
}
