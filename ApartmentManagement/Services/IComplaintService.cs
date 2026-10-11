using ApartmentManagement.Models;
using ApartmentManagement.ViewModels;

namespace ApartmentManagement.Services
{
    public interface IComplaintService
    {
        Task<List<Complaint>> GetComplaintsAsync(string? searchTerm = null, string? status = null, string? category = null);

        Task<Complaint?> GetComplaintByIdAsync(int id);

        Task<(bool Success, string? ErrorMessage)> CreateComplaintAsync(ComplaintCreateViewModel model);

        Task<(bool Success, string? ErrorMessage)> CancelComplaintAsync(int complaintId);

        Task<(bool Success, string? ErrorMessage)> ProcessComplaintAsync(ComplaintProcessViewModel model);
    }
}
