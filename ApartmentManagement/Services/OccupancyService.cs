using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Common.Validation;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Services;

public sealed class OccupancyService : IOccupancyService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<OccupancyService> _logger;

    public OccupancyService(ApplicationDbContext context, ILogger<OccupancyService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task<List<ApartmentResident>> GetAllAsync(
        bool? activeOnly = null, int? apartmentId = null, int? residentId = null)
    {
        var query = Query();
        if (activeOnly == true) query = query.Where(x => x.MoveOutDate == null);
        if (activeOnly == false) query = query.Where(x => x.MoveOutDate != null);
        if (apartmentId.HasValue) query = query.Where(x => x.ApartmentId == apartmentId);
        if (residentId.HasValue) query = query.Where(x => x.ResidentId == residentId);
        return query.OrderByDescending(x => x.MoveInDate).ToListAsync();
    }

    public Task<ApartmentResident?> GetByIdAsync(int id) =>
        id <= 0 ? Task.FromResult<ApartmentResident?>(null) : Query().FirstOrDefaultAsync(x => x.ApartmentResidentId == id);

    public Task<List<ApartmentResident>> GetForUserAsync(string userId) =>
        Query().Where(x => x.Resident.UserId == userId)
            .OrderByDescending(x => x.MoveInDate).ToListAsync();

    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            }
            catch
            {
                return TimeZoneInfo.Local;
            }
        }
    }

    public static DateTime GetApplicationToday()
    {
        var vnDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone).Date;
        var localDate = DateTime.Today;
        return vnDate > localDate ? vnDate : localDate;
    }

    public Task<List<ApartmentResident>> GetActiveHouseholdMembersAsync(int apartmentId) =>
        Query()
            .Where(x => x.ApartmentId == apartmentId && x.MoveOutDate == null)
            .OrderByDescending(x => x.IsOwner)
            .ThenBy(x => x.MoveInDate)
            .ToListAsync();

    public async Task<(bool Success, string? ErrorMessage, ApartmentResident? Occupancy)> MoveInAsync(
        MoveInViewModel model)
    {
        var apartment = await _context.Apartments.FindAsync(model.ApartmentId);
        if (apartment == null) return (false, "Căn hộ không tồn tại.", null);
        var normalizedStatus = ApartmentStatus.Normalize(apartment.Status);
        if (normalizedStatus == ApartmentStatus.Maintenance)
            return (false, "Căn hộ đang trong trạng thái bảo trì, không thể làm thủ tục chuyển vào.", null);
        if (normalizedStatus == ApartmentStatus.Inactive)
            return (false, "Căn hộ đã ngừng sử dụng (không hoạt động), không thể làm thủ tục chuyển vào.", null);

        var residentExists = await _context.Residents.AnyAsync(x => x.ResidentId == model.ResidentId);
        if (!residentExists) return (false, "Cư dân không tồn tại.", null);
        if (string.IsNullOrWhiteSpace(model.Relationship) ||
            model.Relationship.Trim().Length > ResidentValidation.RelationshipMaxLength)
            return (false, "Quan hệ cư trú là bắt buộc và không được vượt quá 100 ký tự.", null);
        if (model.MoveInDate.Date < new DateTime(1900, 1, 1))
            return (false, "Ngày chuyển vào phải từ 01/01/1900.", null);
        if (model.MoveInDate.Date > GetApplicationToday())
            return (false, "Ngày chuyển vào không thể ở tương lai.", null);
        if (await _context.ApartmentResidents.AnyAsync(x =>
                x.ApartmentId == model.ApartmentId && x.ResidentId == model.ResidentId && x.MoveOutDate == null))
            return (false, "Cư dân đã có hồ sơ cư trú đang hoạt động tại căn hộ này.", null);
        if (model.IsOwner && await _context.ApartmentResidents.AnyAsync(x =>
                x.ApartmentId == model.ApartmentId && x.IsOwner && x.MoveOutDate == null))
            return (false, "Căn hộ đã có chủ hộ đang hoạt động.", null);

        var occupancy = new ApartmentResident
        {
            ApartmentId = model.ApartmentId,
            ResidentId = model.ResidentId,
            Relationship = model.Relationship.Trim(),
            MoveInDate = model.MoveInDate.Date,
            IsOwner = model.IsOwner
        };
        _context.ApartmentResidents.Add(occupancy);
        apartment.Status = ApartmentStatus.Occupied;
        try
        {
            await _context.SaveChangesAsync();
            return (true, null, occupancy);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Lỗi dữ liệu khi move-in cư dân {ResidentId} vào căn hộ {ApartmentId}", model.ResidentId, model.ApartmentId);
            if (exception.InnerException?.Message.Contains("UX_ApartmentResidents_ActiveOwner") == true ||
                exception.Message.Contains("UX_ApartmentResidents_ActiveOwner"))
            {
                return (false, "Căn hộ đã có chủ hộ đang hoạt động.", null);
            }
            if (exception.InnerException?.Message.Contains("UX_ApartmentResidents_ActiveAssignment") == true ||
                exception.Message.Contains("UX_ApartmentResidents_ActiveAssignment"))
            {
                return (false, "Cư dân đã có hồ sơ cư trú đang hoạt động tại căn hộ này.", null);
            }
            return (false, "Không thể tạo hồ sơ cư trú vì dữ liệu trùng hoặc không hợp lệ.", null);
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateOccupancyAsync(OccupancyEditViewModel model)
    {
        if (model.ApartmentResidentId <= 0) return (false, "Không tìm thấy hồ sơ cư trú.");

        var occupancy = await _context.ApartmentResidents
            .Include(x => x.Apartment)
            .FirstOrDefaultAsync(x => x.ApartmentResidentId == model.ApartmentResidentId);
        if (occupancy == null) return (false, "Không tìm thấy hồ sơ cư trú.");

        if (string.IsNullOrWhiteSpace(model.Relationship) ||
            model.Relationship.Trim().Length > ResidentValidation.RelationshipMaxLength)
            return (false, "Quan hệ cư trú là bắt buộc và không được vượt quá 100 ký tự.");

        if (model.IsOwner && occupancy.MoveOutDate == null)
        {
            var hasOtherActiveOwner = await _context.ApartmentResidents.AnyAsync(x =>
                x.ApartmentId == occupancy.ApartmentId &&
                x.ApartmentResidentId != occupancy.ApartmentResidentId &&
                x.IsOwner &&
                x.MoveOutDate == null);
            if (hasOtherActiveOwner)
                return (false, "Căn hộ đã có chủ hộ đang hoạt động.");
        }

        occupancy.Relationship = model.Relationship.Trim();
        occupancy.IsOwner = model.IsOwner;

        try
        {
            await _context.SaveChangesAsync();
            return (true, null);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Lỗi dữ liệu khi cập nhật hồ sơ cư trú {OccupancyId}", model.ApartmentResidentId);
            if (exception.InnerException?.Message.Contains("UX_ApartmentResidents_ActiveOwner") == true ||
                exception.Message.Contains("UX_ApartmentResidents_ActiveOwner"))
            {
                return (false, "Căn hộ đã có chủ hộ đang hoạt động.");
            }
            return (false, "Không thể cập nhật hồ sơ cư trú vì dữ liệu không hợp lệ.");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> MoveOutAsync(int id, DateTime moveOutDate)
    {
        if (id <= 0) return (false, "Không tìm thấy hồ sơ cư trú.");

        var occupancy = await _context.ApartmentResidents
            .Include(x => x.Apartment)
            .FirstOrDefaultAsync(x => x.ApartmentResidentId == id);
        if (occupancy == null) return (false, "Không tìm thấy hồ sơ cư trú.");
        if (occupancy.MoveOutDate != null) return (false, "Hồ sơ cư trú này đã kết thúc.");
        if (moveOutDate.Date < occupancy.MoveInDate.Date)
            return (false, "Ngày chuyển đi không thể trước ngày chuyển vào.");
        if (moveOutDate.Date > GetApplicationToday())
            return (false, "Ngày chuyển đi không thể ở tương lai.");

        occupancy.MoveOutDate = moveOutDate.Date;
        var hasOtherActiveOccupancy = await _context.ApartmentResidents.AnyAsync(x =>
            x.ApartmentId == occupancy.ApartmentId &&
            x.ApartmentResidentId != occupancy.ApartmentResidentId &&
            x.MoveOutDate == null);
        if (!hasOtherActiveOccupancy)
        {
            var currentNormalized = ApartmentStatus.Normalize(occupancy.Apartment.Status);
            if (currentNormalized != ApartmentStatus.Inactive)
            {
                occupancy.Apartment.Status = ApartmentStatus.Vacant;
            }
        }

        try
        {
            await _context.SaveChangesAsync();
            return (true, null);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Lỗi dữ liệu khi move-out hồ sơ {OccupancyId}", id);
            return (false, "Không thể cập nhật ngày chuyển đi vì dữ liệu không hợp lệ.");
        }
    }

    private IQueryable<ApartmentResident> Query() => _context.ApartmentResidents.AsNoTracking()
        .Include(x => x.Apartment).ThenInclude(x => x.Building)
        .Include(x => x.Resident).ThenInclude(x => x.User);
}
