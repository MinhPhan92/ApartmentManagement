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
        Query().FirstOrDefaultAsync(x => x.ApartmentResidentId == id);

    public Task<List<ApartmentResident>> GetForUserAsync(string userId) =>
        Query().Where(x => x.Resident.UserId == userId)
            .OrderByDescending(x => x.MoveInDate).ToListAsync();

    public async Task<(bool Success, string? ErrorMessage, ApartmentResident? Occupancy)> MoveInAsync(
        MoveInViewModel model)
    {
        var apartment = await _context.Apartments.FindAsync(model.ApartmentId);
        if (apartment == null) return (false, "Căn hộ không tồn tại.", null);
        var residentExists = await _context.Residents.AnyAsync(x => x.ResidentId == model.ResidentId);
        if (!residentExists) return (false, "Cư dân không tồn tại.", null);
        if (string.IsNullOrWhiteSpace(model.Relationship) ||
            model.Relationship.Trim().Length > ResidentValidation.RelationshipMaxLength)
            return (false, "Quan hệ cư trú là bắt buộc và không được vượt quá 100 ký tự.", null);
        if (model.MoveInDate.Date < new DateTime(1900, 1, 1))
            return (false, "Ngày chuyển vào phải từ 01/01/1900.", null);
        if (model.MoveInDate.Date > DateTime.UtcNow.Date)
            return (false, "Ngày chuyển vào không thể ở tương lai.", null);
        if (await _context.ApartmentResidents.AnyAsync(x =>
                x.ApartmentId == model.ApartmentId && x.ResidentId == model.ResidentId && x.MoveOutDate == null))
            return (false, "Cư dân đã có hồ sơ cư trú đang hoạt động tại căn hộ này.", null);

        var occupancy = new ApartmentResident
        {
            ApartmentId = model.ApartmentId,
            ResidentId = model.ResidentId,
            Relationship = model.Relationship.Trim(),
            MoveInDate = model.MoveInDate.Date,
            IsOwner = model.IsOwner
        };
        _context.ApartmentResidents.Add(occupancy);
        apartment.Status = "Đang sử dụng";
        try
        {
            await _context.SaveChangesAsync();
            return (true, null, occupancy);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Lỗi dữ liệu khi move-in cư dân {ResidentId} vào căn hộ {ApartmentId}", model.ResidentId, model.ApartmentId);
            return (false, "Không thể tạo hồ sơ cư trú vì dữ liệu trùng hoặc không hợp lệ.", null);
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> MoveOutAsync(int id, DateTime moveOutDate)
    {
        var occupancy = await _context.ApartmentResidents
            .Include(x => x.Apartment)
            .FirstOrDefaultAsync(x => x.ApartmentResidentId == id);
        if (occupancy == null) return (false, "Không tìm thấy hồ sơ cư trú.");
        if (occupancy.MoveOutDate != null) return (false, "Hồ sơ cư trú này đã kết thúc.");
        if (moveOutDate.Date < occupancy.MoveInDate.Date)
            return (false, "Ngày chuyển đi không thể trước ngày chuyển vào.");
        if (moveOutDate.Date > DateTime.UtcNow.Date)
            return (false, "Ngày chuyển đi không thể ở tương lai.");

        occupancy.MoveOutDate = moveOutDate.Date;
        var hasOtherActiveOccupancy = await _context.ApartmentResidents.AnyAsync(x =>
            x.ApartmentId == occupancy.ApartmentId &&
            x.ApartmentResidentId != occupancy.ApartmentResidentId &&
            x.MoveOutDate == null);
        if (!hasOtherActiveOccupancy) occupancy.Apartment.Status = "Trống";

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
