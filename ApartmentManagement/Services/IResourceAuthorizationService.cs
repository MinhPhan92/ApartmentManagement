namespace ApartmentManagement.Services
{
    public interface IResourceAuthorizationService
    {
        Task<int?> GetCurrentResidentIdAsync();

        Task<List<int>> GetMyApartmentIdsAsync();

        Task<bool> HasAccessToApartmentAsync(int apartmentId);

        Task<bool> CanAccessResidentDataAsync(int targetResidentId);
    }
}
