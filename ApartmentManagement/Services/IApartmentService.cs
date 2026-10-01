using ApartmentManagement.Models;

namespace ApartmentManagement.Services
{
    public interface IApartmentService
    {
        Task<List<Apartment>> GetAllApartmentsAsync(
            int? buildingId = null,
            string? searchTerm = null);

        Task<Apartment?> GetApartmentByIdAsync(
            int id,
            bool includeResidents = false);

        Task<bool> ApartmentCodeExistsAsync(
            int buildingId,
            string apartmentCode,
            int? excludeId = null);

        Task<(bool Success, string? ErrorMessage)> CreateApartmentAsync(
            Apartment apartment);

        Task<(bool Success, string? ErrorMessage)> UpdateApartmentAsync(
            Apartment apartment);

        Task<(bool Success, string? ErrorMessage)> DeleteApartmentAsync(
            int id);
    }
}