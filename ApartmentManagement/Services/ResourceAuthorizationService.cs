using ApartmentManagement.Data;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Services
{
    public class ResourceAuthorizationService : IResourceAuthorizationService
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly ApplicationDbContext _context;

        public ResourceAuthorizationService(
            ICurrentUserService currentUserService,
            ApplicationDbContext context)
        {
            _currentUserService = currentUserService;
            _context = context;
        }

        public async Task<int?> GetCurrentResidentIdAsync()
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return null;
            }

            var resident = await _context.Residents
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.UserId == userId);

            return resident?.ResidentId;
        }

        public async Task<List<int>> GetMyApartmentIdsAsync()
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return new List<int>();
            }

            return await _context.ApartmentResidents
                .AsNoTracking()
                .Where(ar => ar.Resident.UserId == userId && ar.MoveOutDate == null)
                .Select(ar => ar.ApartmentId)
                .Distinct()
                .ToListAsync();
        }

        public async Task<bool> HasAccessToApartmentAsync(int apartmentId)
        {
            // Quản trị viên, Ban quản lý và Ban quản trị có quyền truy cập toàn bộ căn hộ trong phạm vi của mình
            if (_currentUserService.IsInRole("SystemAdmin") ||
                _currentUserService.IsInRole("Manager") ||
                _currentUserService.IsInRole("Board"))
            {
                return true;
            }

            // Cư dân chỉ có quyền xem căn hộ mà mình đang thực sự cư trú (MoveOutDate == null)
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return false;
            }

            return await _context.ApartmentResidents
                .AsNoTracking()
                .AnyAsync(ar =>
                    ar.ApartmentId == apartmentId &&
                    ar.Resident.UserId == userId &&
                    ar.MoveOutDate == null);
        }

        public async Task<bool> CanAccessResidentDataAsync(int targetResidentId)
        {
            // Quản trị viên và Ban quản lý có quyền xem thông tin cư dân
            if (_currentUserService.IsInRole("SystemAdmin") ||
                _currentUserService.IsInRole("Manager"))
            {
                return true;
            }

            // Cư dân chỉ có quyền xem dữ liệu của chính mình (chống IDOR)
            var currentResidentId = await GetCurrentResidentIdAsync();
            return currentResidentId.HasValue && currentResidentId.Value == targetResidentId;
        }
    }
}
