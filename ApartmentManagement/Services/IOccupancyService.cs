using ApartmentManagement.Models;

namespace ApartmentManagement.Services;

public interface IOccupancyService
{
    Task<List<ApartmentResident>> GetAllAsync(bool? activeOnly = null, int? apartmentId = null, int? residentId = null);
    Task<ApartmentResident?> GetByIdAsync(int id);
    Task<List<ApartmentResident>> GetForUserAsync(string userId);
    Task<(bool Success, string? ErrorMessage, ApartmentResident? Occupancy)> MoveInAsync(MoveInViewModel model);
    Task<(bool Success, string? ErrorMessage)> MoveOutAsync(int id, DateTime moveOutDate);
}
