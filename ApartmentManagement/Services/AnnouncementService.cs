using ApartmentManagement.Data;
using ApartmentManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Services;

public class AnnouncementService : IAnnouncementService
{
    private readonly ApplicationDbContext _context;

    public AnnouncementService(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<List<Announcement>> GetAllAnnouncementsAsync(int? buildingId = null)
    {
        var query = _context.Announcements.AsNoTracking()
            .Include(x => x.Building)
            .Include(x => x.CreatedByUser)
            .AsQueryable();

        if (buildingId.HasValue)
        {
            query = query.Where(x => x.BuildingId == null || x.BuildingId == buildingId.Value);
        }

        return query.OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<List<Announcement>> GetAnnouncementsForResidentAsync(string userId)
    {
        // Lấy danh sách buildingId mà cư dân đang cư trú
        var buildingIds = await _context.ApartmentResidents.AsNoTracking()
            .Where(ar => ar.Resident.UserId == userId && ar.MoveOutDate == null)
            .Select(ar => ar.Apartment.BuildingId)
            .Distinct()
            .ToListAsync();

        return await _context.Announcements.AsNoTracking()
            .Include(x => x.Building)
            .Include(x => x.CreatedByUser)
            .Where(x => x.BuildingId == null || (x.BuildingId.HasValue && buildingIds.Contains(x.BuildingId.Value)))
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync();
    }

    public Task<Announcement?> GetAnnouncementByIdAsync(int id) =>
        _context.Announcements.Include(x => x.Building).Include(x => x.CreatedByUser)
            .FirstOrDefaultAsync(x => x.AnnouncementId == id);

    public async Task<(bool Success, string? ErrorMessage)> CreateAnnouncementAsync(string userId, Announcement announcement)
    {
        if (string.IsNullOrWhiteSpace(announcement.Title))
            return (false, "Tiêu đề thông báo không được để trống.");
        if (string.IsNullOrWhiteSpace(announcement.Content))
            return (false, "Nội dung thông báo không được để trống.");

        announcement.CreatedByUserId = userId;
        announcement.CreatedAtUtc = DateTime.UtcNow;

        _context.Announcements.Add(announcement);
        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteAnnouncementAsync(int id)
    {
        var announcement = await _context.Announcements.FindAsync(id);
        if (announcement == null) return (false, "Không tìm thấy thông báo.");

        _context.Announcements.Remove(announcement);
        await _context.SaveChangesAsync();
        return (true, null);
    }
}
