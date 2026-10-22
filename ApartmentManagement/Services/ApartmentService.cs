using ApartmentManagement.Data;
using ApartmentManagement.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ApartmentManagement.Services
{
    public class ApartmentService : IApartmentService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ApartmentService>? _logger;

        public ApartmentService(
            ApplicationDbContext context,
            ILogger<ApartmentService>? logger = null)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<Apartment>> GetAllApartmentsAsync(
            int? buildingId = null,
            string? searchTerm = null)
        {
            var query = _context.Apartments
                .Include(x => x.Building)
                .AsQueryable();

            if (buildingId.HasValue && buildingId.Value > 0)
            {
                query = query.Where(
                    x => x.BuildingId == buildingId.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.Trim();

                query = query.Where(
                    x => x.ApartmentCode.Contains(searchTerm));
            }

            return await query
                .OrderBy(x => x.BuildingId)
                .ThenBy(x => x.Floor)
                .ThenBy(x => x.ApartmentCode)
                .ToListAsync();
        }

        public async Task<Apartment?> GetApartmentByIdAsync(
            int id,
            bool includeResidents = false)
        {
            var query = _context.Apartments
                .Include(x => x.Building)
                .AsQueryable();

            if (includeResidents)
            {
                query = query
                    .Include(x => x.ApartmentResidents)
                    .ThenInclude(x => x.Resident)
                    .ThenInclude(x => x.User);
            }

            return await query
                .FirstOrDefaultAsync(x => x.ApartmentId == id);
        }

        public async Task<bool> ApartmentCodeExistsAsync(
            int buildingId,
            string apartmentCode,
            int? excludeId = null)
        {
            apartmentCode = apartmentCode.Trim().ToUpper();

            var query = _context.Apartments
                .Where(x =>
                    x.BuildingId == buildingId &&
                    x.ApartmentCode == apartmentCode);

            if (excludeId.HasValue)
            {
                query = query.Where(
                    x => x.ApartmentId != excludeId.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<(bool Success, string? ErrorMessage)>
            CreateApartmentAsync(Apartment apartment)
        {
            if (apartment.BuildingId <= 0)
            {
                return (false, "Vui lòng chọn tòa nhà.");
            }

            if (string.IsNullOrWhiteSpace(apartment.ApartmentCode))
            {
                return (false, "Vui lòng nhập mã căn hộ.");
            }

            if (apartment.Floor <= 0)
            {
                return (false, "Tầng phải lớn hơn 0.");
            }

            if (apartment.Area <= 0)
            {
                return (false, "Diện tích phải lớn hơn 0.");
            }

            if (string.IsNullOrWhiteSpace(apartment.Status))
            {
                apartment.Status = ApartmentStatus.Vacant;
            }
            else
            {
                if (!ApartmentStatus.IsValid(apartment.Status))
                {
                    return (false, "Trạng thái căn hộ không hợp lệ.");
                }
                apartment.Status = ApartmentStatus.Normalize(apartment.Status);
            }

            apartment.ApartmentCode =
                apartment.ApartmentCode.Trim().ToUpper();

            var building = await _context.Buildings
                .FirstOrDefaultAsync(
                    x => x.BuildingId == apartment.BuildingId);

            if (building == null)
            {
                return (false, "Tòa nhà không tồn tại.");
            }

            if (apartment.Floor > building.NumberOfFloors)
            {
                return (
                    false,
                    $"Tầng căn hộ không được vượt quá {building.NumberOfFloors}."
                );
            }

            if (await ApartmentCodeExistsAsync(
                apartment.BuildingId,
                apartment.ApartmentCode))
            {
                return (
                    false,
                    "Mã căn hộ đã tồn tại trong tòa nhà.");
            }

            try
            {
                _context.Apartments.Add(apartment);
                await _context.SaveChangesAsync();
                return (true, null);
            }
            catch (DbUpdateException ex)
            {
                _logger?.LogError(ex, "Lỗi cơ sở dữ liệu khi tạo căn hộ {ApartmentCode}", apartment.ApartmentCode);
                return (false, "Không thể lưu căn hộ do lỗi dữ liệu.");
            }
        }

        public async Task<(bool Success, string? ErrorMessage)>
            UpdateApartmentAsync(Apartment apartment)
        {
            if (apartment.BuildingId <= 0)
            {
                return (false, "Vui lòng chọn tòa nhà.");
            }

            if (string.IsNullOrWhiteSpace(apartment.ApartmentCode))
            {
                return (false, "Vui lòng nhập mã căn hộ.");
            }

            if (apartment.Floor <= 0)
            {
                return (false, "Tầng phải lớn hơn 0.");
            }

            if (apartment.Area <= 0)
            {
                return (false, "Diện tích phải lớn hơn 0.");
            }

            if (string.IsNullOrWhiteSpace(apartment.Status) || !ApartmentStatus.IsValid(apartment.Status))
            {
                return (false, "Trạng thái căn hộ không hợp lệ.");
            }

            var targetStatus = ApartmentStatus.Normalize(apartment.Status);

            var existing = await _context.Apartments
                .FirstOrDefaultAsync(
                    x => x.ApartmentId == apartment.ApartmentId);

            if (existing == null)
            {
                return (false, "Không tìm thấy căn hộ.");
            }

            // Disallow changing to Inactive or Vacant when active residents are living in the apartment
            if (targetStatus == ApartmentStatus.Inactive || targetStatus == ApartmentStatus.Vacant)
            {
                var hasActiveResidents = await _context.ApartmentResidents.AnyAsync(x =>
                    x.ApartmentId == apartment.ApartmentId && x.MoveOutDate == null);

                if (hasActiveResidents)
                {
                    return (
                        false,
                        $"Không thể chuyển trạng thái căn hộ sang '{targetStatus}' khi vẫn còn cư dân đang lưu trú. Vui lòng chuyển cư dân ra khỏi căn hộ trước."
                    );
                }
            }

            var building = await _context.Buildings
                .FirstOrDefaultAsync(
                    x => x.BuildingId == apartment.BuildingId);

            if (building == null)
            {
                return (false, "Tòa nhà không tồn tại.");
            }

            apartment.ApartmentCode =
                apartment.ApartmentCode.Trim().ToUpper();

            if (apartment.Floor > building.NumberOfFloors)
            {
                return (
                    false,
                    $"Tầng căn hộ không được vượt quá {building.NumberOfFloors}."
                );
            }

            if (await ApartmentCodeExistsAsync(
                apartment.BuildingId,
                apartment.ApartmentCode,
                apartment.ApartmentId))
            {
                return (
                    false,
                    "Mã căn hộ đã tồn tại trong tòa nhà.");
            }

            existing.BuildingId = apartment.BuildingId;
            existing.ApartmentCode = apartment.ApartmentCode;
            existing.Floor = apartment.Floor;
            existing.Area = apartment.Area;
            existing.Status = targetStatus;

            try
            {
                await _context.SaveChangesAsync();
                return (true, null);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger?.LogError(ex, "Lỗi xung đột dữ liệu khi cập nhật căn hộ ID {ApartmentId}", apartment.ApartmentId);
                return (false, "Dữ liệu căn hộ đã bị thay đổi bởi người khác. Vui lòng tải lại trang.");
            }
            catch (DbUpdateException ex)
            {
                _logger?.LogError(ex, "Lỗi cơ sở dữ liệu khi cập nhật căn hộ ID {ApartmentId}", apartment.ApartmentId);
                return (false, "Không thể cập nhật căn hộ do lỗi dữ liệu.");
            }
        }

        public async Task<(bool Success, string? ErrorMessage)>
            DeleteApartmentAsync(int id)
        {
            var apartment = await _context.Apartments
                .FirstOrDefaultAsync(x => x.ApartmentId == id);

            if (apartment == null)
            {
                return (false, "Không tìm thấy căn hộ.");
            }

            // 1. Check active residents
            var hasActiveResidents = await _context.ApartmentResidents
                .AnyAsync(x => x.ApartmentId == id && x.MoveOutDate == null);

            if (hasActiveResidents)
            {
                return (
                    false,
                    "Không thể xóa căn hộ đang có cư dân lưu trú. Vui lòng hoàn tất thủ tục chuyển đi cho cư dân trước."
                );
            }

            // 2. Check historical records and business links to prevent data loss or cascade failures
            var hasHistory = await _context.ApartmentResidents.AnyAsync(x => x.ApartmentId == id)
                || await _context.ApartmentContracts.AnyAsync(x => x.ApartmentId == id)
                || await _context.ApartmentInvoices.AnyAsync(x => x.ApartmentId == id)
                || await _context.IncidentTickets.AnyAsync(x => x.ApartmentId == id)
                || await _context.Complaints.AnyAsync(x => x.ApartmentId == id)
                || await _context.UtilityMeterReadings.AnyAsync(x => x.ApartmentId == id)
                || await _context.VisitorPasses.AnyAsync(x => x.ApartmentId == id);

            if (hasHistory)
            {
                return (
                    false,
                    "Không thể xóa căn hộ đã phát sinh dữ liệu lịch sử (hợp đồng, hóa đơn, sự cố, phản ánh hoặc lịch sử cư trú). Vui lòng chuyển trạng thái căn hộ sang 'Không hoạt động' để bảo toàn lịch sử."
                );
            }

            try
            {
                _context.Apartments.Remove(apartment);
                await _context.SaveChangesAsync();
                return (true, null);
            }
            catch (DbUpdateException ex)
            {
                _logger?.LogError(ex, "Lỗi ràng buộc dữ liệu khi xóa căn hộ ID {ApartmentId}", id);
                return (
                    false,
                    "Không thể xóa căn hộ do có dữ liệu liên kết trong hệ thống. Vui lòng chuyển sang trạng thái 'Không hoạt động'."
                );
            }
        }

        public async Task<(bool Success, string? ErrorMessage)>
            DeactivateApartmentAsync(int id)
        {
            var apartment = await _context.Apartments
                .FirstOrDefaultAsync(x => x.ApartmentId == id);

            if (apartment == null)
            {
                return (false, "Không tìm thấy căn hộ.");
            }

            var hasActiveResidents = await _context.ApartmentResidents
                .AnyAsync(x => x.ApartmentId == id && x.MoveOutDate == null);

            if (hasActiveResidents)
            {
                return (
                    false,
                    "Không thể ngừng sử dụng căn hộ đang có cư dân lưu trú. Vui lòng hoàn tất thủ tục chuyển đi cho cư dân trước."
                );
            }

            apartment.Status = ApartmentStatus.Inactive;

            try
            {
                await _context.SaveChangesAsync();
                return (true, null);
            }
            catch (DbUpdateException ex)
            {
                _logger?.LogError(ex, "Lỗi khi chuyển trạng thái căn hộ ID {ApartmentId} sang Không hoạt động", id);
                return (false, "Không thể cập nhật trạng thái ngừng sử dụng cho căn hộ.");
            }
        }
    }
}