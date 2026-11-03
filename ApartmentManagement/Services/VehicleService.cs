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
            ICurrentUserService? currentUserService = null)
        {
            _context = context;
            _currentUserService = currentUserService ?? new NullCurrentUserService();
        }

        private sealed class NullCurrentUserService : ICurrentUserService
        {
            public string? UserId => null;
            public string? UserName => null;
            public bool IsAuthenticated => false;
            public bool IsInRole(string role) => false;
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
                .Include(v => v.Apartment)
                .Include(v => v.ParkingCard)
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

        public Task<VehicleReviewResult> ApproveVehicleAsync(int vehicleId, string? notes = null)
            => ApproveVehicleAsync(vehicleId, null, notes, _currentUserService?.UserId ?? string.Empty);

        public Task<VehicleReviewResult> RejectVehicleAsync(int vehicleId, string reason)
            => RejectVehicleAsync(vehicleId, reason, _currentUserService?.UserId ?? string.Empty);

        public async Task<VehicleReviewResult> ApproveVehicleAsync(
            int vehicleId,
            string? cardCode,
            string? notes,
            string reviewerUserId)
        {
            var vehicle = await _context.Vehicles
                .Include(v => v.ParkingCard)
                .FirstOrDefaultAsync(v => v.VehicleId == vehicleId);

            if (vehicle == null)
                return new VehicleReviewResult(false, "Không tìm thấy phương tiện.");

            if (vehicle.Status != "Chờ duyệt")
                return new VehicleReviewResult(false, $"Phương tiện đang ở trạng thái '{vehicle.Status}', không thể phê duyệt.");

            int? cardId = null;
            string? cleanCardCode = null;

            // Nếu Ban Quản Lý gán mã thẻ từ RFID ngay lúc duyệt: thẩm định mã thẻ trước
            if (!string.IsNullOrWhiteSpace(cardCode))
            {
                cleanCardCode = cardCode.Trim().ToUpperInvariant();

                // Chống trùng mã thẻ từ trên toàn hệ thống
                var cardCodeExists = await _context.ParkingCards
                    .AnyAsync(c => c.CardCode == cleanCardCode && c.VehicleId != vehicleId && c.Status == "Active");

                if (cardCodeExists)
                    return new VehicleReviewResult(false, $"Mã thẻ từ '{cleanCardCode}' đang được sử dụng cho một xe khác.");
            }

            // Cập nhật trạng thái duyệt xe
            vehicle.Status = "Đã duyệt";
            vehicle.ReviewedAt = DateTime.UtcNow;
            vehicle.ReviewNotes = notes?.Trim();

            if (cleanCardCode != null)
            {
                if (vehicle.ParkingCard != null)
                {
                    vehicle.ParkingCard.CardCode = cleanCardCode;
                    vehicle.ParkingCard.Status = "Active";
                    vehicle.ParkingCard.IssuedAtUtc = DateTime.UtcNow;
                    cardId = vehicle.ParkingCard.ParkingCardId;
                }
                else
                {
                    var newCard = new ParkingCard
                    {
                        VehicleId = vehicle.VehicleId,
                        CardCode = cleanCardCode,
                        Status = "Active",
                        IssuedAtUtc = DateTime.UtcNow
                    };
                    vehicle.ParkingCard = newCard;
                    _context.ParkingCards.Add(newCard);
                    await _context.SaveChangesAsync();
                    cardId = newCard.ParkingCardId;
                }
            }

            await _context.SaveChangesAsync();
            return new VehicleReviewResult(true, null, cardId);
        }

        public async Task<VehicleReviewResult> RejectVehicleAsync(
            int vehicleId,
            string reason,
            string reviewerUserId)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return new VehicleReviewResult(false, "Vui lòng nhập lý do từ chối phê duyệt.");

            var vehicle = await _context.Vehicles
                .Include(v => v.ParkingCard)
                .FirstOrDefaultAsync(v => v.VehicleId == vehicleId);

            if (vehicle == null)
                return new VehicleReviewResult(false, "Không tìm thấy phương tiện.");

            if (vehicle.Status != "Chờ duyệt")
                return new VehicleReviewResult(false, $"Phương tiện đang ở trạng thái '{vehicle.Status}', không thể từ chối.");

            vehicle.Status = "Từ chối";
            vehicle.ReviewedAt = DateTime.UtcNow;
            vehicle.ReviewNotes = reason.Trim();

            // Hủy thẻ xe nếu có
            if (vehicle.ParkingCard != null)
            {
                vehicle.ParkingCard.Status = "Locked";
            }

            await _context.SaveChangesAsync();
            return new VehicleReviewResult(true, null);
        }

        public async Task<VehicleReviewResult> IssueOrReplaceCardAsync(
            int vehicleId,
            string newCardCode,
            string reviewerUserId)
        {
            if (string.IsNullOrWhiteSpace(newCardCode))
                return new VehicleReviewResult(false, "Vui lòng nhập mã thẻ từ bãi đỗ.");

            var cleanCardCode = newCardCode.Trim().ToUpperInvariant();

            var vehicle = await _context.Vehicles
                .Include(v => v.ParkingCard)
                .FirstOrDefaultAsync(v => v.VehicleId == vehicleId);

            if (vehicle == null)
                return new VehicleReviewResult(false, "Không tìm thấy phương tiện.");

            if (vehicle.Status != "Đã duyệt")
                return new VehicleReviewResult(false, $"Chỉ có thể cấp hoặc đổi thẻ cho phương tiện đã duyệt. Trạng thái hiện tại: '{vehicle.Status}'.");

            // Chống trùng mã thẻ từ trên toàn hệ thống (không chỉ Active, vì UX_ParkingCards_CardCode áp dụng cho toàn bảng)
            var cardCodeExists = await _context.ParkingCards
                .AnyAsync(c => c.CardCode == cleanCardCode && c.VehicleId != vehicleId);

            if (cardCodeExists)
                return new VehicleReviewResult(false, $"Mã thẻ từ '{cleanCardCode}' đang được sử dụng cho một xe khác.");

            int cardId = 0;
            ParkingCard? newCard = null;
            string? originalCardCode = null;
            string? originalStatus = null;
            DateTime originalIssuedAtUtc = default;

            if (vehicle.ParkingCard != null)
            {
                originalCardCode = vehicle.ParkingCard.CardCode;
                originalStatus = vehicle.ParkingCard.Status;
                originalIssuedAtUtc = vehicle.ParkingCard.IssuedAtUtc;

                vehicle.ParkingCard.CardCode = cleanCardCode;
                vehicle.ParkingCard.Status = "Active";
                vehicle.ParkingCard.IssuedAtUtc = DateTime.UtcNow;
                cardId = vehicle.ParkingCard.ParkingCardId;
            }
            else
            {
                newCard = new ParkingCard
                {
                    VehicleId = vehicle.VehicleId,
                    CardCode = cleanCardCode,
                    Status = "Active",
                    IssuedAtUtc = DateTime.UtcNow
                };
                vehicle.ParkingCard = newCard;
                _context.ParkingCards.Add(newCard);
            }

            try
            {
                await _context.SaveChangesAsync();
                cardId = vehicle.ParkingCard?.ParkingCardId ?? cardId;
                return new VehicleReviewResult(true, null, cardId);
            }
            catch (DbUpdateException ex)
            {
                // Khôi phục trạng thái ChangeTracker để không làm bẩn các thao tác tiếp theo trên cùng DbContext
                if (newCard != null)
                {
                    _context.Entry(newCard).State = EntityState.Detached;
                    vehicle.ParkingCard = null;
                }
                else if (vehicle.ParkingCard != null && originalCardCode != null)
                {
                    vehicle.ParkingCard.CardCode = originalCardCode;
                    vehicle.ParkingCard.Status = originalStatus!;
                    vehicle.ParkingCard.IssuedAtUtc = originalIssuedAtUtc;

                    var cardEntry = _context.Entry(vehicle.ParkingCard);
                    cardEntry.CurrentValues.SetValues(cardEntry.OriginalValues);
                    cardEntry.State = EntityState.Unchanged;
                }

                if (IsCardCodeUniqueViolation(ex))
                {
                    return new VehicleReviewResult(false, $"Mã thẻ từ '{cleanCardCode}' đang được sử dụng cho một xe khác.");
                }

                throw;
            }
        }

        private static bool IsCardCodeUniqueViolation(DbUpdateException ex)
        {
            for (Exception? current = ex; current != null; current = current.InnerException)
            {
                if (current is Microsoft.Data.SqlClient.SqlException sqlEx)
                {
                    if ((sqlEx.Number == 2601 || sqlEx.Number == 2627) &&
                        (sqlEx.Message.Contains("UX_ParkingCards_CardCode", StringComparison.OrdinalIgnoreCase) ||
                         sqlEx.Message.Contains("CardCode", StringComparison.OrdinalIgnoreCase) ||
                         sqlEx.Message.Contains("ParkingCards", StringComparison.OrdinalIgnoreCase)))
                    {
                        return true;
                    }
                }

                if (current.Message.Contains("UX_ParkingCards_CardCode", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public async Task<VehicleReviewResult> ReportLostOrLockCardAsync(
            int vehicleId,
            string reason,
            string actorUserId)
        {
            var vehicle = await _context.Vehicles
                .Include(v => v.ParkingCard)
                .FirstOrDefaultAsync(v => v.VehicleId == vehicleId);

            if (vehicle == null)
                return new VehicleReviewResult(false, "Không tìm thấy hồ sơ phương tiện.");

            if (vehicle.ParkingCard == null)
                return new VehicleReviewResult(false, "Phương tiện chưa được cấp thẻ bãi đỗ xe.");

            if (vehicle.ParkingCard.Status == "Lost" || vehicle.ParkingCard.Status == "Locked")
                return new VehicleReviewResult(false, "Thẻ bãi đỗ của phương tiện này đã ở trạng thái khóa trước đó.");

            vehicle.ParkingCard.Status = "Lost";
            var cleanReason = string.IsNullOrWhiteSpace(reason) ? "Báo mất thẻ" : reason.Trim();
            vehicle.ReviewNotes = string.IsNullOrWhiteSpace(vehicle.ReviewNotes)
                ? $"Khóa thẻ ({cleanReason})"
                : $"{vehicle.ReviewNotes} | Khóa thẻ: {cleanReason}";

            await _context.SaveChangesAsync();
            return new VehicleReviewResult(true, null, vehicle.ParkingCard.ParkingCardId);
        }
    }
}
