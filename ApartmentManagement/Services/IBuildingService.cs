using ApartmentManagement.Models;

namespace ApartmentManagement.Services
{
    public interface IBuildingService
    {
        Task<List<Building>> GetAllBuildingsAsync(string? searchTerm = null);
        Task<Building?> GetBuildingByIdAsync(int id, bool includeApartments = false);
        Task<bool> BuildingCodeExistsAsync(string buildingCode, int? excludeId = null);
        Task<(bool Success, string? ErrorMessage, Building? Building)> CreateBuildingAsync(Building building);
        Task<(bool Success, string? ErrorMessage)> UpdateBuildingAsync(Building building);
        Task<(bool Success, string? ErrorMessage)> DeleteBuildingAsync(int id);
        Task<int> GetApartmentCountAsync(int buildingId);
    }
}
