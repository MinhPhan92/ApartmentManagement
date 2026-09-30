using ApartmentManagement.Data;
using ApartmentManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Services
{
    public class BuildingService : IBuildingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BuildingService> _logger;

        public BuildingService(ApplicationDbContext context, ILogger<BuildingService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<Building>> GetAllBuildingsAsync(string? searchTerm = null)
        {
            var query = _context.Buildings
                .Include(b => b.Apartments)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(b =>
                    b.BuildingCode.ToLower().Contains(term) ||
                    b.BuildingName.ToLower().Contains(term) ||
                    (b.Address != null && b.Address.ToLower().Contains(term)));
            }

            return await query.OrderByDescending(b => b.CreatedAt).ToListAsync();
        }

        public async Task<Building?> GetBuildingByIdAsync(int id, bool includeApartments = false)
        {
            var query = _context.Buildings.AsQueryable();

            if (includeApartments)
            {
                query = query.Include(b => b.Apartments);
            }

            return await query.FirstOrDefaultAsync(b => b.BuildingId == id);
        }

        public async Task<bool> BuildingCodeExistsAsync(string buildingCode, int? excludeId = null)
        {
            var code = buildingCode.Trim();
            var query = _context.Buildings.Where(b => b.BuildingCode.ToLower() == code.ToLower());

            if (excludeId.HasValue)
            {
                query = query.Where(b => b.BuildingId != excludeId.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<(bool Success, string? ErrorMessage, Building? Building)> CreateBuildingAsync(Building building)
        {
            try
            {
                building.BuildingCode = building.BuildingCode.Trim().ToUpper();
                building.BuildingName = building.BuildingName.Trim();
                building.Address = building.Address?.Trim();

                if (await BuildingCodeExistsAsync(building.BuildingCode))
                {
                    return (false, $"Mã tòa nhà '{building.BuildingCode}' đã tồn tại trong hệ thống.", null);
                }

                building.CreatedAt = DateTime.UtcNow;
                _context.Buildings.Add(building);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Đã thêm mới tòa nhà: {BuildingCode} - {BuildingName}", building.BuildingCode, building.BuildingName);
                return (true, null, building);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo mới tòa nhà {BuildingCode}", building.BuildingCode);
                return (false, "Đã xảy ra lỗi trong quá trình lưu dữ liệu: " + ex.Message, null);
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> UpdateBuildingAsync(Building building)
        {
            try
            {
                var existing = await _context.Buildings.FindAsync(building.BuildingId);
                if (existing == null)
                {
                    return (false, "Không tìm thấy tòa nhà cần cập nhật.");
                }

                building.BuildingCode = building.BuildingCode.Trim().ToUpper();
                building.BuildingName = building.BuildingName.Trim();
                building.Address = building.Address?.Trim();

                if (await BuildingCodeExistsAsync(building.BuildingCode, building.BuildingId))
                {
                    return (false, $"Mã tòa nhà '{building.BuildingCode}' đã được sử dụng bởi tòa nhà khác.");
                }

                existing.BuildingCode = building.BuildingCode;
                existing.BuildingName = building.BuildingName;
                existing.Address = building.Address;
                existing.NumberOfFloors = building.NumberOfFloors;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Đã cập nhật tòa nhà ID {BuildingId}: {BuildingCode}", building.BuildingId, building.BuildingCode);
                return (true, null);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Lỗi xung đột dữ liệu khi cập nhật tòa nhà ID {BuildingId}", building.BuildingId);
                return (false, "Dữ liệu đã bị thay đổi bởi phiên làm việc khác. Vui lòng tải lại trang.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật tòa nhà ID {BuildingId}", building.BuildingId);
                return (false, "Đã xảy ra lỗi trong quá trình cập nhật dữ liệu: " + ex.Message);
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> DeleteBuildingAsync(int id)
        {
            try
            {
                var building = await _context.Buildings
                    .Include(b => b.Apartments)
                    .FirstOrDefaultAsync(b => b.BuildingId == id);

                if (building == null)
                {
                    return (false, "Không tìm thấy tòa nhà cần xóa.");
                }

                var apartmentCount = building.Apartments.Count;
                if (apartmentCount > 0)
                {
                    return (false, $"Không thể xóa tòa nhà '{building.BuildingName}' ({building.BuildingCode}) vì đang có {apartmentCount} căn hộ trực thuộc. Vui lòng xóa hoặc di dời các căn hộ trước.");
                }

                _context.Buildings.Remove(building);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Đã xóa tòa nhà ID {BuildingId}: {BuildingCode}", id, building.BuildingCode);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa tòa nhà ID {BuildingId}", id);
                return (false, "Không thể xóa tòa nhà: " + ex.Message);
            }
        }

        public async Task<int> GetApartmentCountAsync(int buildingId)
        {
            return await _context.Apartments.CountAsync(a => a.BuildingId == buildingId);
        }
    }
}
