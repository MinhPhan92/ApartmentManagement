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

        Task<VehicleReviewResult> ApproveVehicleAsync(int vehicleId, string? notes = null);

        Task<VehicleReviewResult> RejectVehicleAsync(int vehicleId, string reason);

        Task<VehicleReviewResult> ApproveVehicleAsync(int vehicleId, string? cardCode, string? notes, string reviewerUserId);

        Task<VehicleReviewResult> RejectVehicleAsync(int vehicleId, string reason, string reviewerUserId);

        Task<VehicleReviewResult> IssueOrReplaceCardAsync(int vehicleId, string newCardCode, string reviewerUserId);

        Task<VehicleReviewResult> ReportLostOrLockCardAsync(int vehicleId, string reason, string actorUserId);
    }
}
