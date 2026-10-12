using ApartmentManagement.Data;
using ApartmentManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Services;

public class AmenityService : IAmenityService
{
    private readonly ApplicationDbContext _context;

    public AmenityService(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<List<Amenity>> GetAllAmenitiesAsync() =>
        _context.Amenities.AsNoTracking().OrderBy(x => x.Name).ToListAsync();

    public Task<Amenity?> GetAmenityByIdAsync(int id) =>
        _context.Amenities.Include(x => x.Bookings).FirstOrDefaultAsync(x => x.AmenityId == id);

    public async Task<(bool Success, string? ErrorMessage)> CreateAmenityAsync(Amenity amenity)
    {
        if (string.IsNullOrWhiteSpace(amenity.Name))
            return (false, "Tên tiện ích không được để trống.");

        var trimmedName = amenity.Name.Trim();
        var duplicate = await _context.Amenities
            .AnyAsync(x => x.Name.ToLower() == trimmedName.ToLower());
        if (duplicate)
            return (false, $"Tên tiện ích '{trimmedName}' đã tồn tại trong hệ thống.");

        if (amenity.OpenTime >= amenity.CloseTime)
            return (false, "Giờ mở cửa phải trước giờ đóng cửa.");

        amenity.Name = trimmedName;
        amenity.Description = amenity.Description?.Trim();
        amenity.Location = amenity.Location?.Trim();

        _context.Amenities.Add(amenity);
        try
        {
            await _context.SaveChangesAsync();
            return (true, null);
        }
        catch (DbUpdateException)
        {
            return (false, $"Tên tiện ích '{trimmedName}' đã tồn tại trong hệ thống.");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateAmenityAsync(Amenity amenity)
    {
        var existing = await _context.Amenities.FindAsync(amenity.AmenityId);
        if (existing == null) return (false, "Không tìm thấy tiện ích.");

        if (string.IsNullOrWhiteSpace(amenity.Name))
            return (false, "Tên tiện ích không được để trống.");

        var trimmedName = amenity.Name.Trim();
        var duplicate = await _context.Amenities
            .AnyAsync(x => x.AmenityId != amenity.AmenityId && x.Name.ToLower() == trimmedName.ToLower());
        if (duplicate)
            return (false, $"Tên tiện ích '{trimmedName}' đã tồn tại trong hệ thống.");

        if (amenity.OpenTime >= amenity.CloseTime)
            return (false, "Giờ mở cửa phải trước giờ đóng cửa.");

        existing.Name = trimmedName;
        existing.Description = amenity.Description?.Trim();
        existing.Location = amenity.Location?.Trim();
        existing.Capacity = amenity.Capacity;
        existing.OpenTime = amenity.OpenTime;
        existing.CloseTime = amenity.CloseTime;
        existing.PricePerHour = amenity.PricePerHour;
        existing.IsActive = amenity.IsActive;

        try
        {
            await _context.SaveChangesAsync();
            return (true, null);
        }
        catch (DbUpdateException)
        {
            return (false, $"Tên tiện ích '{trimmedName}' đã tồn tại trong hệ thống.");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteAmenityAsync(int id)
    {
        var amenity = await _context.Amenities.Include(x => x.Bookings).FirstOrDefaultAsync(x => x.AmenityId == id);
        if (amenity == null) return (false, "Không tìm thấy tiện ích.");
        if (amenity.Bookings.Any(b => b.BookingDate >= OccupancyService.GetApplicationToday() && b.Status != "Đã hủy"))
            return (false, "Không thể xóa tiện ích đang có lịch đặt chỗ sắp tới. Hãy hủy các lịch đặt trước.");

        _context.Amenities.Remove(amenity);
        await _context.SaveChangesAsync();
        return (true, null);
    }

    public Task<List<AmenityBooking>> GetBookingsAsync(int? amenityId = null, DateTime? date = null)
    {
        var query = _context.AmenityBookings.AsNoTracking()
            .Include(x => x.Amenity)
            .Include(x => x.Resident).ThenInclude(r => r.User)
            .AsQueryable();

        if (amenityId.HasValue) query = query.Where(x => x.AmenityId == amenityId.Value);
        if (date.HasValue) query = query.Where(x => x.BookingDate.Date == date.Value.Date);

        return query.OrderByDescending(x => x.BookingDate).ThenBy(x => x.StartTime).ToListAsync();
    }

    public async Task<List<AmenityBooking>> GetResidentBookingsAsync(string userId)
    {
        var resident = await _context.Residents.FirstOrDefaultAsync(x => x.UserId == userId);
        if (resident == null) return new List<AmenityBooking>();

        return await _context.AmenityBookings.AsNoTracking()
            .Include(x => x.Amenity)
            .Where(x => x.ResidentId == resident.ResidentId)
            .OrderByDescending(x => x.BookingDate)
            .ThenByDescending(x => x.StartTime)
            .ToListAsync();
    }

    public async Task<(bool Success, string? ErrorMessage)> BookAmenityAsync(
        string userId, int amenityId, DateTime bookingDate, TimeSpan startTime, TimeSpan endTime, int guests, string? notes)
    {
        var resident = await _context.Residents.FirstOrDefaultAsync(x => x.UserId == userId);
        if (resident == null) return (false, "Không tìm thấy hồ sơ cư dân tương ứng.");

        var amenity = await _context.Amenities.FindAsync(amenityId);
        if (amenity == null || !amenity.IsActive)
            return (false, "Tiện ích không tồn tại hoặc đang tạm đóng.");

        if (startTime >= endTime)
            return (false, "Thời gian bắt đầu phải trước thời gian kết thúc.");

        if (startTime < amenity.OpenTime || endTime > amenity.CloseTime)
            return (false, $"Khung giờ phải nằm trong thời gian hoạt động ({amenity.OpenTime:hh\\:mm} - {amenity.CloseTime:hh\\:mm}).");

        if (bookingDate.Date < OccupancyService.GetApplicationToday())
            return (false, "Không thể đặt lịch cho ngày trong quá khứ.");

        if (guests <= 0 || guests > amenity.Capacity)
            return (false, $"Số người phải từ 1 đến {amenity.Capacity}.");

        if (_context.Database.IsRelational())
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable);
                try
                {
                    var result = await CreateBookingInternalAsync(resident.ResidentId, amenity, bookingDate, startTime, endTime, guests, notes);
                    if (result.Success)
                    {
                        await transaction.CommitAsync();
                    }
                    else
                    {
                        await transaction.RollbackAsync();
                    }
                    return result;
                }
                catch (Exception ex) when (ex is DbUpdateException || ex is InvalidOperationException)
                {
                    try
                    {
                        await transaction.RollbackAsync();
                    }
                    catch
                    {
                        // Transaction may have already been aborted by SQL Server
                    }
                    return (false, "Không thể hoàn tất đặt chỗ do có giao dịch cạnh tranh xảy ra đồng thời. Vui lòng thử lại.");
                }
            });
        }

        return await CreateBookingInternalAsync(resident.ResidentId, amenity, bookingDate, startTime, endTime, guests, notes);
    }

    private async Task<(bool Success, string? ErrorMessage)> CreateBookingInternalAsync(
        int residentId, Amenity amenity, DateTime bookingDate, TimeSpan startTime, TimeSpan endTime, int guests, string? notes)
    {
        // Kiểm tra trùng lịch và quá tải sức chứa
        var existingBookings = await _context.AmenityBookings
            .Where(b => b.AmenityId == amenity.AmenityId &&
                        b.BookingDate.Date == bookingDate.Date &&
                        b.Status != "Đã hủy" &&
                        b.StartTime < endTime &&
                        b.EndTime > startTime)
            .ToListAsync();

        var currentTotalGuests = existingBookings.Sum(b => b.NumberOfGuests);
        if (currentTotalGuests + guests > amenity.Capacity)
        {
            return (false, $"Khung giờ này đã có người đặt ({currentTotalGuests}/{amenity.Capacity} người). Không đủ sức chứa cho {guests} người.");
        }

        var booking = new AmenityBooking
        {
            AmenityId = amenity.AmenityId,
            ResidentId = residentId,
            BookingDate = bookingDate.Date,
            StartTime = startTime,
            EndTime = endTime,
            NumberOfGuests = guests,
            Status = "Đã xác nhận",
            Notes = notes?.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.AmenityBookings.Add(booking);
        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> CancelBookingAsync(string userId, int bookingId, bool isManager = false)
    {
        var booking = await _context.AmenityBookings.Include(x => x.Resident).FirstOrDefaultAsync(x => x.AmenityBookingId == bookingId);
        if (booking == null) return (false, "Không tìm thấy lịch đặt chỗ.");

        if (!isManager && booking.Resident.UserId != userId)
            return (false, "Bạn không có quyền hủy lịch đặt chỗ của cư dân khác.");

        if (booking.Status == "Đã hủy")
            return (false, "Lịch đặt chỗ này đã bị hủy trước đó.");

        booking.Status = "Đã hủy";
        await _context.SaveChangesAsync();
        return (true, null);
    }
}
