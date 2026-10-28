using ApartmentManagement.Common.Security;
using ApartmentManagement.Data;
using ApartmentManagement.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ApartmentManagement.Services;

public class RepairRequestService : IRepairRequestService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<RepairRequestService> _logger;

    public RepairRequestService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<RepairRequestService> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    #region Resident Operations

    public async Task<IEnumerable<RepairRequest>> GetResidentRequestsAsync(int residentId)
    {
        return await _context.RepairRequests.AsNoTracking()
            .Where(r => r.ResidentId == residentId)
            .Include(r => r.Apartment).ThenInclude(a => a.Building)
            .Include(r => r.Assignments.OrderByDescending(a => a.AssignedAt)).ThenInclude(a => a.TechnicianUser)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<RepairRequest?> GetRequestByIdForResidentAsync(int requestId, int residentId)
    {
        return await _context.RepairRequests.AsNoTracking()
            .Include(r => r.Apartment).ThenInclude(a => a.Building)
            .Include(r => r.Resident).ThenInclude(res => res.User)
            .Include(r => r.Assignments.OrderByDescending(a => a.AssignedAt)).ThenInclude(a => a.TechnicianUser)
            .Include(r => r.StatusHistory.OrderByDescending(h => h.ChangedAt)).ThenInclude(h => h.ChangedByUser)
            .FirstOrDefaultAsync(r => r.RepairRequestId == requestId && r.ResidentId == residentId);
    }

    public async Task<RepairOperationResult> CreateRequestAsync(
        int residentId,
        int apartmentId,
        string title,
        string description,
        string priority)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Failure("Vui lòng nhập tiêu đề yêu cầu sửa chữa.");
        if (title.Trim().Length > 200)
            return Failure("Tiêu đề không được vượt quá 200 ký tự.");
        if (string.IsNullOrWhiteSpace(description))
            return Failure("Vui lòng nhập mô tả chi tiết sự cố.");
        if (description.Trim().Length > 4000)
            return Failure("Mô tả không được vượt quá 4000 ký tự.");

        if (!RepairPriority.AllPriorities.Contains(priority))
            priority = RepairPriority.Normal;

        // Verify active residency in apartment
        var isActiveResident = await IsResidentActiveInApartmentAsync(residentId, apartmentId);
        if (!isActiveResident)
        {
            return Failure("Bạn không cư trú tại căn hộ này hoặc hợp đồng cư trú đã kết thúc.");
        }

        var resident = await _context.Residents
            .AsNoTracking()
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.ResidentId == residentId);

        if (resident == null)
            return Failure("Không tìm thấy thông tin cư dân.");

        var request = new RepairRequest
        {
            ResidentId = residentId,
            ApartmentId = apartmentId,
            Title = title.Trim(),
            Description = description.Trim(),
            Priority = priority,
            Status = RepairRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        request.StatusHistory.Add(new RepairStatusHistory
        {
            PreviousStatus = null,
            NewStatus = RepairRequestStatus.Pending,
            ChangedByUserId = resident.UserId,
            ChangedAt = DateTime.UtcNow,
            Note = "Cư dân gửi yêu cầu sửa chữa mới."
        });

        _context.RepairRequests.Add(request);

        try
        {
            await _context.SaveChangesAsync();
            return Success(request.RepairRequestId);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Lỗi khi lưu yêu cầu sửa chữa của cư dân {ResidentId}", residentId);
            return Failure("Không thể lưu yêu cầu sửa chữa. Vui lòng thử lại sau.");
        }
    }

    public async Task<RepairOperationResult> CancelRequestAsync(
        int requestId,
        int residentId,
        string? reason,
        byte[]? rowVersion = null)
    {
        var request = await _context.RepairRequests
            .Include(r => r.Resident)
            .Include(r => r.Assignments)
            .Include(r => r.StatusHistory)
            .FirstOrDefaultAsync(r => r.RepairRequestId == requestId && r.ResidentId == residentId);

        if (request == null)
            return Failure("Không tìm thấy yêu cầu sửa chữa.");

        if (request.Status != RepairRequestStatus.Pending && request.Status != RepairRequestStatus.Assigned)
        {
            return Failure("Chỉ có thể hủy yêu cầu khi đang ở trạng thái 'Chờ tiếp nhận' hoặc 'Đã phân công'.");
        }

        if (!HasCurrentRowVersion(request, rowVersion))
            return ConcurrencyFailure();

        var prevStatus = request.Status;
        request.Status = RepairRequestStatus.Cancelled;

        // Cancel any assigned work
        foreach (var assignment in request.Assignments.Where(a => a.Status == RepairAssignmentStatus.Assigned))
        {
            assignment.Status = RepairAssignmentStatus.Reassigned;
            assignment.CompletedAt = DateTime.UtcNow;
            assignment.Note = (assignment.Note + " [Yêu cầu đã bị hủy bởi cư dân]").Trim();
        }

        request.StatusHistory.Add(new RepairStatusHistory
        {
            PreviousStatus = prevStatus,
            NewStatus = RepairRequestStatus.Cancelled,
            ChangedByUserId = request.Resident.UserId,
            ChangedAt = DateTime.UtcNow,
            Note = string.IsNullOrWhiteSpace(reason) ? "Cư dân hủy yêu cầu sửa chữa." : $"Lý do hủy: {reason.Trim()}"
        });

        SetOriginalRowVersion(request, rowVersion);
        return await SaveChangesAsync(request.RepairRequestId, "Không thể hủy yêu cầu sửa chữa.");
    }

    public async Task<RepairOperationResult> ConfirmCompletionAsync(
        int requestId,
        int residentId,
        byte rating,
        string? feedback,
        byte[]? rowVersion = null)
    {
        if (rating is < 1 or > 5)
            return Failure("Đánh giá chất lượng phải từ 1 đến 5 sao.");

        var request = await _context.RepairRequests
            .Include(r => r.Resident)
            .Include(r => r.StatusHistory)
            .FirstOrDefaultAsync(r => r.RepairRequestId == requestId && r.ResidentId == residentId);

        if (request == null)
            return Failure("Không tìm thấy yêu cầu sửa chữa.");

        if (request.Status != RepairRequestStatus.PendingConfirmation)
        {
            return Failure("Chỉ có thể nghiệm thu yêu cầu đang ở trạng thái 'Chờ nghiệm thu'.");
        }

        if (!HasCurrentRowVersion(request, rowVersion))
            return ConcurrencyFailure();

        var prevStatus = request.Status;
        request.Status = RepairRequestStatus.Completed;
        request.ConfirmedAt = DateTime.UtcNow;
        request.Rating = rating;
        request.ResidentFeedback = string.IsNullOrWhiteSpace(feedback) ? null : feedback.Trim();

        request.StatusHistory.Add(new RepairStatusHistory
        {
            PreviousStatus = prevStatus,
            NewStatus = RepairRequestStatus.Completed,
            ChangedByUserId = request.Resident.UserId,
            ChangedAt = DateTime.UtcNow,
            Note = $"Cư dân đã nghiệm thu đạt kết quả ({rating} sao). Nhận xét: {request.ResidentFeedback ?? "Không có"}"
        });

        SetOriginalRowVersion(request, rowVersion);
        return await SaveChangesAsync(request.RepairRequestId, "Không thể lưu kết quả nghiệm thu.");
    }

    public async Task<RepairOperationResult> RequestRedoAsync(
        int requestId,
        int residentId,
        string reason,
        byte[]? rowVersion = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Failure("Vui lòng nhập lý do yêu cầu xử lý lại.");

        var request = await _context.RepairRequests
            .Include(r => r.Resident)
            .Include(r => r.StatusHistory)
            .FirstOrDefaultAsync(r => r.RepairRequestId == requestId && r.ResidentId == residentId);

        if (request == null)
            return Failure("Không tìm thấy yêu cầu sửa chữa.");

        if (request.Status != RepairRequestStatus.PendingConfirmation)
        {
            return Failure("Chỉ có thể yêu cầu làm lại đối với yêu cầu đang ở trạng thái 'Chờ nghiệm thu'.");
        }

        if (!HasCurrentRowVersion(request, rowVersion))
            return ConcurrencyFailure();

        var prevStatus = request.Status;
        request.Status = RepairRequestStatus.Reopened;

        request.StatusHistory.Add(new RepairStatusHistory
        {
            PreviousStatus = prevStatus,
            NewStatus = RepairRequestStatus.Reopened,
            ChangedByUserId = request.Resident.UserId,
            ChangedAt = DateTime.UtcNow,
            Note = $"Cư dân yêu cầu làm lại. Lý do: {reason.Trim()}"
        });

        SetOriginalRowVersion(request, rowVersion);
        return await SaveChangesAsync(request.RepairRequestId, "Không thể gửi yêu cầu làm lại.");
    }

    #endregion

    #region Management & Queue Operations

    public async Task<IEnumerable<RepairRequest>> GetQueueAsync(
        string? status = null,
        string? priority = null,
        string? searchTerm = null)
    {
        var query = _context.RepairRequests.AsNoTracking()
            .Include(r => r.Apartment).ThenInclude(a => a.Building)
            .Include(r => r.Resident).ThenInclude(res => res.User)
            .Include(r => r.Assignments.OrderByDescending(a => a.AssignedAt)).ThenInclude(a => a.TechnicianUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(r => r.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            query = query.Where(r => r.Priority == priority);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(r =>
                r.Title.Contains(term) ||
                r.Apartment.ApartmentCode.Contains(term) ||
                (r.Apartment.Building != null && r.Apartment.Building.BuildingName.Contains(term)) ||
                r.Resident.User.FullName.Contains(term));
        }

        return await query
            .OrderByDescending(r => r.Priority == RepairPriority.Urgent)
            .ThenByDescending(r => r.Priority == RepairPriority.High)
            .ThenByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<RepairRequest?> GetRequestDetailsAsync(int requestId)
    {
        return await _context.RepairRequests.AsNoTracking()
            .Include(r => r.Apartment).ThenInclude(a => a.Building)
            .Include(r => r.Resident).ThenInclude(res => res.User)
            .Include(r => r.Assignments.OrderByDescending(a => a.AssignedAt)).ThenInclude(a => a.TechnicianUser)
            .Include(r => r.StatusHistory.OrderByDescending(h => h.ChangedAt)).ThenInclude(h => h.ChangedByUser)
            .FirstOrDefaultAsync(r => r.RepairRequestId == requestId);
    }

    public async Task<RepairOperationResult> AssignTechnicianAsync(
        int requestId,
        string technicianUserId,
        string managerUserId,
        string? note,
        byte[]? rowVersion = null)
    {
        var request = await _context.RepairRequests
            .Include(r => r.Assignments)
            .Include(r => r.StatusHistory)
            .FirstOrDefaultAsync(r => r.RepairRequestId == requestId);

        if (request == null)
            return Failure("Không tìm thấy yêu cầu sửa chữa.");

        if (request.Status == RepairRequestStatus.Completed || request.Status == RepairRequestStatus.Cancelled)
        {
            return Failure("Không thể phân công yêu cầu đã hoàn thành hoặc đã hủy.");
        }

        var techUser = await _userManager.FindByIdAsync(technicianUserId);
        if (techUser == null || !techUser.IsActive)
            return Failure("Kỹ thuật viên không tồn tại hoặc tài khoản đã bị khóa.");

        var isTech = await _userManager.IsInRoleAsync(techUser, AppRoles.Technician);
        if (!isTech)
            return Failure("Người dùng được chọn không có vai trò Kỹ thuật viên (Technician).");

        if (!HasCurrentRowVersion(request, rowVersion))
            return ConcurrencyFailure();

        // Mark any previous active assignments as Reassigned
        foreach (var activeAssignment in request.Assignments.Where(a =>
                     a.Status == RepairAssignmentStatus.Assigned ||
                     a.Status == RepairAssignmentStatus.InProgress))
        {
            activeAssignment.Status = RepairAssignmentStatus.Reassigned;
            activeAssignment.CompletedAt = DateTime.UtcNow;
            activeAssignment.Note = (activeAssignment.Note + $" [Chuyển giao cho KTV {techUser.FullName}]").Trim();
        }

        var newAssignment = new RepairAssignment
        {
            RepairRequestId = requestId,
            TechnicianUserId = technicianUserId,
            AssignedAt = DateTime.UtcNow,
            Status = RepairAssignmentStatus.Assigned,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
        };

        request.Assignments.Add(newAssignment);

        var prevStatus = request.Status;
        request.Status = RepairRequestStatus.Assigned;

        request.StatusHistory.Add(new RepairStatusHistory
        {
            PreviousStatus = prevStatus,
            NewStatus = RepairRequestStatus.Assigned,
            ChangedByUserId = managerUserId,
            ChangedAt = DateTime.UtcNow,
            Note = $"Ban quản lý phân công cho KTV {techUser.FullName}. Ghi chú: {note ?? "Không có"}"
        });

        SetOriginalRowVersion(request, rowVersion);
        return await SaveChangesAsync(request.RepairRequestId, "Không thể phân công kỹ thuật viên.");
    }

    public async Task<IEnumerable<ApplicationUser>> GetAvailableTechniciansAsync()
    {
        var techUsers = await _userManager.GetUsersInRoleAsync(AppRoles.Technician);
        return techUsers.Where(u => u.IsActive).OrderBy(u => u.FullName).ToList();
    }

    #endregion

    #region Technician Operations

    public async Task<IEnumerable<RepairAssignment>> GetTechnicianAssignmentsAsync(
        string technicianUserId,
        string? status = null)
    {
        var query = _context.RepairAssignments.AsNoTracking()
            .Where(a => a.TechnicianUserId == technicianUserId)
            .Include(a => a.RepairRequest).ThenInclude(r => r.Apartment).ThenInclude(apt => apt.Building)
            .Include(a => a.RepairRequest).ThenInclude(r => r.Resident).ThenInclude(res => res.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(a => a.Status == status);
        }

        return await query
            .OrderByDescending(a => a.AssignedAt)
            .ToListAsync();
    }

    public async Task<RepairAssignment?> GetAssignmentDetailsAsync(int assignmentId, string technicianUserId)
    {
        return await _context.RepairAssignments.AsNoTracking()
            .Include(a => a.TechnicianUser)
            .Include(a => a.RepairRequest).ThenInclude(r => r.Apartment).ThenInclude(apt => apt.Building)
            .Include(a => a.RepairRequest).ThenInclude(r => r.Resident).ThenInclude(res => res.User)
            .Include(a => a.RepairRequest).ThenInclude(r => r.StatusHistory.OrderByDescending(h => h.ChangedAt)).ThenInclude(h => h.ChangedByUser)
            .FirstOrDefaultAsync(a => a.AssignmentId == assignmentId && a.TechnicianUserId == technicianUserId);
    }

    public async Task<RepairOperationResult> StartProgressAsync(
        int assignmentId,
        string technicianUserId,
        string? note,
        byte[]? rowVersion = null)
    {
        var assignment = await _context.RepairAssignments
            .Include(a => a.RepairRequest).ThenInclude(r => r.StatusHistory)
            .FirstOrDefaultAsync(a => a.AssignmentId == assignmentId);

        if (assignment == null)
            return Failure("Không tìm thấy phân công công việc.");

        if (assignment.TechnicianUserId != technicianUserId)
            return Failure("Bạn không có quyền thao tác trên phân công công việc của người khác.");

        if (assignment.Status != RepairAssignmentStatus.Assigned)
            return Failure("Chỉ có thể tiếp nhận phân công ở trạng thái 'Đã phân công'.");

        var request = assignment.RepairRequest;
        if (!HasCurrentRowVersion(request, rowVersion))
            return ConcurrencyFailure();

        assignment.Status = RepairAssignmentStatus.InProgress;
        if (!string.IsNullOrWhiteSpace(note))
        {
            assignment.Note = string.IsNullOrWhiteSpace(assignment.Note) ? note.Trim() : $"{assignment.Note}\n{note.Trim()}";
        }

        var prevStatus = request.Status;
        request.Status = RepairRequestStatus.InProgress;

        request.StatusHistory.Add(new RepairStatusHistory
        {
            PreviousStatus = prevStatus,
            NewStatus = RepairRequestStatus.InProgress,
            ChangedByUserId = technicianUserId,
            ChangedAt = DateTime.UtcNow,
            Note = string.IsNullOrWhiteSpace(note) ? "Kỹ thuật viên đã tiếp nhận và đang tiến hành sửa chữa." : $"Kỹ thuật viên tiếp nhận: {note.Trim()}"
        });

        SetOriginalRowVersion(request, rowVersion);
        return await SaveChangesAsync(request.RepairRequestId, "Không thể cập nhật trạng thái tiếp nhận.");
    }

    public async Task<RepairOperationResult> CompleteWorkAsync(
        int assignmentId,
        string technicianUserId,
        string result,
        string? note,
        byte[]? rowVersion = null)
    {
        if (string.IsNullOrWhiteSpace(result))
            return Failure("Bắt buộc phải nhập kết quả xử lý trước khi gửi nghiệm thu.");

        var assignment = await _context.RepairAssignments
            .Include(a => a.RepairRequest).ThenInclude(r => r.StatusHistory)
            .FirstOrDefaultAsync(a => a.AssignmentId == assignmentId);

        if (assignment == null)
            return Failure("Không tìm thấy phân công công việc.");

        if (assignment.TechnicianUserId != technicianUserId)
            return Failure("Bạn không có quyền cập nhật phân công công việc của người khác.");

        if (assignment.Status != RepairAssignmentStatus.InProgress && assignment.Status != RepairAssignmentStatus.Assigned)
            return Failure("Phân công này đã hoàn thành hoặc đã chuyển giao.");

        var request = assignment.RepairRequest;
        if (!HasCurrentRowVersion(request, rowVersion))
            return ConcurrencyFailure();

        assignment.Status = RepairAssignmentStatus.Completed;
        assignment.CompletedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(note))
        {
            assignment.Note = string.IsNullOrWhiteSpace(assignment.Note) ? note.Trim() : $"{assignment.Note}\n{note.Trim()}";
        }

        var prevStatus = request.Status;
        request.Status = RepairRequestStatus.PendingConfirmation;
        request.Result = result.Trim();
        request.CompletedAt = DateTime.UtcNow;

        request.StatusHistory.Add(new RepairStatusHistory
        {
            PreviousStatus = prevStatus,
            NewStatus = RepairRequestStatus.PendingConfirmation,
            ChangedByUserId = technicianUserId,
            ChangedAt = DateTime.UtcNow,
            Note = $"Kỹ thuật viên hoàn tất công việc và gửi chờ cư dân nghiệm thu. Kết quả: {result.Trim()}"
        });

        SetOriginalRowVersion(request, rowVersion);
        return await SaveChangesAsync(request.RepairRequestId, "Không thể hoàn tất công việc sửa chữa.");
    }

    #endregion

    #region Residency Verification Helpers

    public async Task<bool> IsResidentActiveInApartmentAsync(int residentId, int apartmentId)
    {
        return await _context.ApartmentResidents.AsNoTracking()
            .AnyAsync(ar => ar.ResidentId == residentId &&
                            ar.ApartmentId == apartmentId &&
                            ar.MoveOutDate == null);
    }

    public async Task<IEnumerable<Apartment>> GetActiveApartmentsForResidentAsync(int residentId)
    {
        return await _context.ApartmentResidents.AsNoTracking()
            .Where(ar => ar.ResidentId == residentId && ar.MoveOutDate == null)
            .Include(ar => ar.Apartment).ThenInclude(a => a.Building)
            .Select(ar => ar.Apartment)
            .ToListAsync();
    }

    #endregion

    #region Internal Save & Concurrency Helpers

    private static bool HasCurrentRowVersion(RepairRequest request, byte[]? rowVersion)
    {
        if (rowVersion == null || rowVersion.Length == 0) return true;
        if (request.RowVersion == null || request.RowVersion.Length == 0) return true;
        return request.RowVersion.AsSpan().SequenceEqual(rowVersion);
    }

    private void SetOriginalRowVersion(RepairRequest request, byte[]? rowVersion)
    {
        if (_context.Database.IsRelational() && rowVersion is { Length: > 0 })
        {
            _context.Entry(request).Property(x => x.RowVersion).OriginalValue = rowVersion;
        }
    }

    private async Task<RepairOperationResult> SaveChangesAsync(int requestId, string defaultErrorMessage)
    {
        try
        {
            await _context.SaveChangesAsync();
            return Success(requestId);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Xung đột đồng thời khi cập nhật yêu cầu {RequestId}", requestId);
            return ConcurrencyFailure();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Lỗi DB khi cập nhật yêu cầu {RequestId}", requestId);
            return Failure(defaultErrorMessage);
        }
    }

    private static RepairOperationResult Success(int requestId) => new(true, null, requestId);
    private static RepairOperationResult Failure(string errorMessage) => new(false, errorMessage, null);
    private static RepairOperationResult ConcurrencyFailure() =>
        new(false, "Dữ liệu yêu cầu đã được cập nhật bởi một người dùng khác. Vui lòng tải lại trang và thử lại.");

    #endregion
}
