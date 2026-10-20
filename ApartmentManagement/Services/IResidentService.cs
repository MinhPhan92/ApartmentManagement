using ApartmentManagement.Models;

namespace ApartmentManagement.Services;

public interface IResidentService
{
    Task<List<Resident>> GetAllAsync(string? searchTerm = null);
    Task<Resident?> GetByIdAsync(int id);
    Task<Resident?> GetByUserIdAsync(string userId);
    Task<(bool Success, string? ErrorMessage, Resident? Resident)> CreateAsync(ResidentCreateViewModel model);
    Task<(bool Success, string? ErrorMessage)> UpdateAsync(ResidentEditViewModel model);
    Task<(bool Success, string? ErrorMessage)> UpdateProfileAsync(string userId, ResidentProfileEditViewModel model);
    Task<(bool Success, string? ErrorMessage)> DeleteAsync(int id);
    Task<List<Apartment>> GetApartmentsForCurrentResidentAsync();
}

