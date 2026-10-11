using ApartmentManagement.Models;
using ApartmentManagement.ViewModels;

namespace ApartmentManagement.Services
{
    public interface IVehicleService
    {
        Task<List<Vehicle>> GetVehiclesAsync(string? searchTerm = null, string? status = null);

        Task<Vehicle?> GetVehicleByIdAsync(int id);

        Task<(bool Success, string? ErrorMessage)> RegisterVehicleAsync(VehicleRegisterViewModel model);

        Task<(bool Success, string? ErrorMessage)> CancelRegistrationAsync(int vehicleId);

        Task<(bool Success, string? ErrorMessage)> ApproveVehicleAsync(int vehicleId, string? notes = null);

        Task<(bool Success, string? ErrorMessage)> RejectVehicleAsync(int vehicleId, string reason);
    }
}
