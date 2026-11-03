using ApartmentManagement.Models;

namespace ApartmentManagement.Services;

public record TechnicalIncidentOperationResult(bool Success, string? ErrorMessage = null, int? IncidentId = null);

public interface ITechnicalIncidentService
{
    Task<IEnumerable<TechnicalIncident>> GetIncidentsAsync(
        string? status = null,
        string? priority = null,
        int? buildingId = null,
        string? searchTerm = null,
        string? technicianUserId = null);

    Task<TechnicalIncident?> GetIncidentDetailsAsync(int incidentId);

    // UC47: Kỹ thuật viên tạo báo cáo sự cố
    Task<TechnicalIncidentOperationResult> CreateIncidentAsync(
        string technicianUserId,
        int buildingId,
        string location,
        string category,
        string title,
        string description,
        string priority);

    // UC48: Ban quản lý tiếp nhận báo cáo sự cố
    Task<TechnicalIncidentOperationResult> AcknowledgeIncidentAsync(
        int incidentId,
        string managerUserId,
        string? note = null,
        byte[]? rowVersion = null);

    // UC49: Ban quản lý phân công kỹ thuật viên xử lý
    Task<TechnicalIncidentOperationResult> AssignTechnicianAsync(
        int incidentId,
        string technicianUserId,
        string managerUserId,
        DateTime? scheduledDate = null,
        string? note = null,
        byte[]? rowVersion = null);

    // Kỹ thuật viên bắt đầu xử lý
    Task<TechnicalIncidentOperationResult> StartWorkAsync(
        int incidentId,
        string technicianUserId,
        string? note = null,
        byte[]? rowVersion = null);

    // Kỹ thuật viên hoàn tất và báo cáo kết quả
    Task<TechnicalIncidentOperationResult> ResolveIncidentAsync(
        int incidentId,
        string technicianUserId,
        string resolutionNotes,
        string? spareParts = null,
        decimal? materialCost = null,
        byte[]? rowVersion = null);

    // UC50: Ban quản lý theo dõi và đóng nghiệm thu sự cố
    Task<TechnicalIncidentOperationResult> CloseIncidentAsync(
        int incidentId,
        string managerUserId,
        string? note = null,
        byte[]? rowVersion = null);

    Task<IEnumerable<ApplicationUser>> GetAvailableTechniciansAsync();
}

