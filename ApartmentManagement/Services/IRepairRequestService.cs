using ApartmentManagement.Models;

namespace ApartmentManagement.Services;

public record RepairOperationResult(bool Success, string? ErrorMessage = null, int? RequestId = null);

public interface IRepairRequestService
{
    // Cư dân
    Task<IEnumerable<RepairRequest>> GetResidentRequestsAsync(int residentId);
    Task<RepairRequest?> GetRequestByIdForResidentAsync(int requestId, int residentId);
    Task<RepairOperationResult> CreateRequestAsync(int residentId, int apartmentId, string title, string description, string priority);
    Task<RepairOperationResult> CancelRequestAsync(int requestId, int residentId, string? reason, byte[]? rowVersion = null);
    Task<RepairOperationResult> ConfirmCompletionAsync(int requestId, int residentId, byte rating, string? feedback, byte[]? rowVersion = null);
    Task<RepairOperationResult> RequestRedoAsync(int requestId, int residentId, string reason, byte[]? rowVersion = null);

    // Ban quản lý & Hàng đợi
    Task<IEnumerable<RepairRequest>> GetQueueAsync(string? status = null, string? priority = null, string? searchTerm = null);
    Task<RepairRequest?> GetRequestDetailsAsync(int requestId);
    Task<RepairOperationResult> AssignTechnicianAsync(int requestId, string technicianUserId, string managerUserId, string? note, byte[]? rowVersion = null);
    Task<IEnumerable<ApplicationUser>> GetAvailableTechniciansAsync();

    // Kỹ thuật viên
    Task<IEnumerable<RepairAssignment>> GetTechnicianAssignmentsAsync(string technicianUserId, string? status = null);
    Task<RepairAssignment?> GetAssignmentDetailsAsync(int assignmentId, string technicianUserId);
    Task<RepairOperationResult> StartProgressAsync(int assignmentId, string technicianUserId, string? note, byte[]? rowVersion = null);
    Task<RepairOperationResult> CompleteWorkAsync(int assignmentId, string technicianUserId, string result, string? note, byte[]? rowVersion = null);

    // Kiểm tra quyền sở hữu / cư trú
    Task<bool> IsResidentActiveInApartmentAsync(int residentId, int apartmentId);
    Task<IEnumerable<Apartment>> GetActiveApartmentsForResidentAsync(int residentId);
}
