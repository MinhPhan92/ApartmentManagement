using ApartmentManagement.Data;
using ApartmentManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Services
{
    public class ApartmentService : IApartmentService
    {
        private readonly ApplicationDbContext _context;

        public ApartmentService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Apartment>> GetAllApartmentsAsync(
            int? buildingId = null,
            string? searchTerm = null)
        {
            var query = _context.Apartments
                .Include(x => x.Building)
                .AsQueryable();

            if (buildingId.HasValue)
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

            _context.Apartments.Add(apartment);

            await _context.SaveChangesAsync();

            return (true, null);
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

            var existing = await _context.Apartments
                .FirstOrDefaultAsync(
                    x => x.ApartmentId == apartment.ApartmentId);

            if (existing == null)
            {
                return (false, "Không tìm thấy căn hộ.");
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
            existing.Status = apartment.Status;

            await _context.SaveChangesAsync();

            return (true, null);
        }

        public async Task<(bool Success, string? ErrorMessage)>
            DeleteApartmentAsync(int id)
        {
            var apartment = await _context.Apartments
                .Include(x => x.ApartmentResidents)
                .FirstOrDefaultAsync(x => x.ApartmentId == id);

            if (apartment == null)
            {
                return (false, "Không tìm thấy căn hộ.");
            }

            if (apartment.ApartmentResidents.Any())
            {
                return (
                    false,
                    "Không thể xóa căn hộ đang có cư dân."
                );
            }

            _context.Apartments.Remove(apartment);

            await _context.SaveChangesAsync();

            return (true, null);
        }
    }
}