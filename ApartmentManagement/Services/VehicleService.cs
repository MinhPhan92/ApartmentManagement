using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.ViewModels;
using ApartmentManagement.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Services
{
    public class VehicleService : IVehicleService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public VehicleService(
            ApplicationDbContext context,
            ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<List<Vehicle>> GetVehiclesAsync(string? searchTerm = null, string? status = null)
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return new List<Vehicle>();
            }

            var query = _context.Vehicles
                .Include(v => v.Resident)
                    .ThenInclude(r => r!.User)
                .AsQueryable();

            // Phân quyền dữ liệu: Cư dân chỉ thấy phương tiện của chính mình
            if (!_currentUserService.IsInRole(AppRoles.SuperAdmin) && !_currentUserService.IsInRole(AppRoles.BuildingManager))
            {
                query = query.Where(v => v.Resident != null && v.Resident.UserId == userId);
            }

            // Lọc theo trạng thái
            if (!string.IsNullOrWhiteSpace(status))
            {
                status = status.Trim();
                query = query.Where(v => v.Status == status);
            }

            // Tìm kiếm theo biển số, dòng xe hoặc tên cư dân
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(v =>
                    v.LicensePlate.ToLower().Contains(term) ||
                    (v.BrandModel != null && v.BrandModel.ToLower().Contains(term)) ||
                    (v.Resident != null && v.Resident.User != null && v.Resident.User.FullName.ToLower().Contains(term)));
            }

            return await query
                .OrderByDescending(v => v.CreatedAt)
                .ToListAsync();
        }

        public async Task<Vehicle?> GetVehicleByIdAsync(int id)
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return null;
            }

            var query = _context.Vehicles
                .Include(v => v.Resident)
                    .ThenInclude(r => r!.User)
                .AsQueryable();

            // Quản trị viên và Ban quản lý được xem chi tiết phương tiện bất kỳ
            if (_currentUserService.IsInRole(AppRoles.SuperAdmin) || _currentUserService.IsInRole(AppRoles.BuildingManager))
            {
                return await query.FirstOrDefaultAsync(v => v.VehicleId == id);
            }

            // Cư dân chỉ được xem phương tiện của chính mình (chống IDOR)
            return await query.FirstOrDefaultAsync(v =>
                v.VehicleId == id &&
                v.Resident != null &&
                v.Resident.UserId == userId);
        }

        public async Task<(bool Success, string? ErrorMessage)> RegisterVehicleAsync(VehicleRegisterViewModel model)
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return (false, "Người dùng chưa được xác thực.");
            }

            // Tìm thông tin Resident dựa trên UserId đăng nhập (không tin cậy input từ client)
            var resident = await _context.Residents
                .FirstOrDefaultAsync(r => r.UserId == userId);

            if (resident == null)
            {
                return (false, "Tài khoản của bạn chưa được liên kết với hồ sơ cư dân.");
            }

            var allowedTypes = new[] { "Xe máy", "Ô tô", "Xe đạp điện" };
            if (!allowedTypes.Contains(model.VehicleType?.Trim()))
            {
                return (false, "Loại phương tiện không hợp lệ. Chỉ chấp nhận: Xe máy, Ô tô, Xe đạp điện.");
            }

            var normalizedPlate = model.LicensePlate.Trim().ToUpper();

            var apartmentId = await _context.ApartmentResidents
                .Where(x => x.ResidentId == resident.ResidentId && x.MoveOutDate == null)
                .OrderByDescending(x => x.MoveInDate)
                .Select(x => (int?)x.ApartmentId)
                .FirstOrDefaultAsync();
            if (apartmentId is null)
            {
                return (false, "Bạn chưa có căn hộ đang cư trú để đăng ký phương tiện.");
            }

            // Kiểm tra trùng biển số đang còn hiệu lực (Chờ duyệt hoặc Đã duyệt) trong toàn hệ thống
            var duplicateActive = await _context.Vehicles
                .AnyAsync(v => v.LicensePlate == normalizedPlate);

            if (duplicateActive)
            {
                return (false, $"Biển số '{normalizedPlate}' đang có đăng ký đang chờ duyệt hoặc đã được duyệt sử dụng.");
            }

            var vehicle = new Vehicle
            {
                ResidentId = resident.ResidentId,
                ApartmentId = apartmentId.Value,
                VehicleType = model.VehicleType!.Trim(),
                LicensePlate = normalizedPlate,
                BrandModel = model.BrandModel?.Trim(),
                Color = model.Color?.Trim(),
                Status = "Chờ duyệt",
                CreatedAt = DateTime.UtcNow
            };

            _context.Vehicles.Add(vehicle);
            try
            {
                await _context.SaveChangesAsync();
                return (true, null);
            }
            catch (DbUpdateException)
            {
                return (false, $"Biển số '{normalizedPlate}' đang có đăng ký đang chờ duyệt hoặc đã được duyệt sử dụng.");
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> CancelRegistrationAsync(int vehicleId)
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return (false, "Người dùng chưa được xác thực.");
            }

            var vehicle = await _context.Vehicles
                .Include(v => v.Resident)
                .FirstOrDefaultAsync(v => v.VehicleId == vehicleId);

            if (vehicle == null)
            {
                return (false, "Không tìm thấy phương tiện.");
            }

            // Chỉ chủ sở hữu phương tiện mới được quyền hủy đăng ký
            if (vehicle.Resident == null || vehicle.Resident.UserId != userId)
            {
                return (false, "Bạn không có quyền thực hiện thao tác trên phương tiện này.");
            }

            // Nghiệp vụ: Chỉ cho phép hủy khi đang ở trạng thái 'Chờ duyệt'
            if (vehicle.Status != "Chờ duyệt")
            {
                return (false, $"Không thể hủy đăng ký phương tiện đang ở trạng thái '{vehicle.Status}'.");
            }

            vehicle.Status = "Đã hủy";
            vehicle.ReviewedAt = DateTime.UtcNow;
            vehicle.ReviewNotes = "Cư dân chủ động hủy đăng ký.";

            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool Success, string? ErrorMessage)> ApproveVehicleAsync(int vehicleId, string? notes = null)
        {
            var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.VehicleId == vehicleId);
            if (vehicle == null)
            {
                return (false, "Không tìm thấy phương tiện.");
            }

            if (vehicle.Status != "Chờ duyệt")
            {
                return (false, $"Chỉ có thể duyệt phương tiện ở trạng thái 'Chờ duyệt'. Trạng thái hiện tại: '{vehicle.Status}'.");
            }

            vehicle.Status = "Đã duyệt";
            vehicle.ReviewedAt = DateTime.UtcNow;
            vehicle.ReviewNotes = notes?.Trim();

            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool Success, string? ErrorMessage)> RejectVehicleAsync(int vehicleId, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return (false, "Vui lòng nhập lý do từ chối đăng ký.");
            }

            var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.VehicleId == vehicleId);
            if (vehicle == null)
            {
                return (false, "Không tìm thấy phương tiện.");
            }

            if (vehicle.Status != "Chờ duyệt")
            {
                return (false, $"Chỉ có thể từ chối phương tiện ở trạng thái 'Chờ duyệt'. Trạng thái hiện tại: '{vehicle.Status}'.");
            }

            vehicle.Status = "Từ chối";
            vehicle.ReviewedAt = DateTime.UtcNow;
            vehicle.ReviewNotes = reason.Trim();

            await _context.SaveChangesAsync();
            return (true, null);
        }
    }
}
