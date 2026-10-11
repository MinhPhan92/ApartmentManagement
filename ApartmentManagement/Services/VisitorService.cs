using ApartmentManagement.Data;
using ApartmentManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Services;

public class VisitorService : IVisitorService
{
    private readonly ApplicationDbContext _context;

    public VisitorService(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<List<VisitorPass>> GetAllVisitorPassesAsync(DateTime? date = null, string? status = null)
    {
        var query = _context.VisitorPasses.AsNoTracking()
            .Include(x => x.Apartment).ThenInclude(a => a.Building)
            .Include(x => x.Resident).ThenInclude(r => r.User)
            .AsQueryable();

        if (date.HasValue) query = query.Where(x => x.ExpectedArrivalDate.Date == date.Value.Date);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status.Trim());

        return query.OrderByDescending(x => x.ExpectedArrivalDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<List<VisitorPass>> GetResidentVisitorPassesAsync(string userId)
    {
        var resident = await _context.Residents.FirstOrDefaultAsync(x => x.UserId == userId);
        if (resident == null) return new List<VisitorPass>();

        return await _context.VisitorPasses.AsNoTracking()
            .Include(x => x.Apartment).ThenInclude(a => a.Building)
            .Where(x => x.ResidentId == resident.ResidentId)
            .OrderByDescending(x => x.ExpectedArrivalDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync();
    }

    public Task<VisitorPass?> GetVisitorPassByIdAsync(int id) =>
        _context.VisitorPasses.Include(x => x.Apartment).ThenInclude(a => a.Building)
            .Include(x => x.Resident).ThenInclude(r => r.User)
            .FirstOrDefaultAsync(x => x.VisitorPassId == id);

    public async Task<(bool Success, string? ErrorMessage)> RegisterVisitorAsync(
        string userId, int apartmentId, string visitorName, string? phone, string? idCard, DateTime arrivalDate, string? purpose)
    {
        var resident = await _context.Residents.FirstOrDefaultAsync(x => x.UserId == userId);
        if (resident == null) return (false, "Không tìm thấy hồ sơ cư dân tương ứng.");

        var hasActiveResidency = await _context.ApartmentResidents.AnyAsync(ar =>
            ar.ApartmentId == apartmentId && ar.ResidentId == resident.ResidentId && ar.MoveOutDate == null);
        if (!hasActiveResidency)
            return (false, "Căn hộ không hợp lệ hoặc bạn không cư trú tại căn hộ này.");

        if (string.IsNullOrWhiteSpace(visitorName))
            return (false, "Vui lòng nhập họ tên khách.");

        if (arrivalDate.Date < DateTime.UtcNow.Date)
            return (false, "Ngày đến dự kiến không được trong quá khứ.");

        var pass = new VisitorPass
        {
            ResidentId = resident.ResidentId,
            ApartmentId = apartmentId,
            VisitorName = visitorName.Trim(),
            PhoneNumber = phone?.Trim(),
            IdCardNumber = idCard?.Trim(),
            ExpectedArrivalDate = arrivalDate.Date,
            Purpose = purpose?.Trim(),
            Status = "Đã đăng ký",
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.VisitorPasses.Add(pass);
        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> CheckInVisitorAsync(int visitorPassId)
    {
        var pass = await _context.VisitorPasses.FindAsync(visitorPassId);
        if (pass == null) return (false, "Không tìm thấy thẻ khách.");
        if (pass.Status != "Đã đăng ký")
            return (false, $"Thẻ khách đang ở trạng thái '{pass.Status}', không thể Check-in.");

        pass.Status = "Đã vào";
        pass.ActualCheckInUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> CheckOutVisitorAsync(int visitorPassId)
    {
        var pass = await _context.VisitorPasses.FindAsync(visitorPassId);
        if (pass == null) return (false, "Không tìm thấy thẻ khách.");
        if (pass.Status != "Đã vào")
            return (false, $"Thẻ khách đang ở trạng thái '{pass.Status}', chỉ có thể Check-out khi khách đã vào.");

        pass.Status = "Đã ra";
        pass.ActualCheckOutUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> CancelVisitorPassAsync(string userId, int visitorPassId, bool isManager = false)
    {
        var pass = await _context.VisitorPasses.Include(x => x.Resident).FirstOrDefaultAsync(x => x.VisitorPassId == visitorPassId);
        if (pass == null) return (false, "Không tìm thấy thẻ khách.");

        if (!isManager && pass.Resident.UserId != userId)
            return (false, "Bạn không có quyền hủy đăng ký của cư dân khác.");

        if (pass.Status != "Đã đăng ký")
            return (false, "Chỉ có thể hủy khi khách chưa check-in vào tòa nhà.");

        pass.Status = "Đã hủy";
        await _context.SaveChangesAsync();
        return (true, null);
    }
}
