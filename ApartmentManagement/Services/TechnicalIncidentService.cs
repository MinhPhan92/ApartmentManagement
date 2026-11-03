using ApartmentManagement.Common.Security;
using ApartmentManagement.Data;
using ApartmentManagement.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Services;

public class TechnicalIncidentService : ITechnicalIncidentService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<TechnicalIncidentService> _logger;

    public TechnicalIncidentService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<TechnicalIncidentService> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<IEnumerable<TechnicalIncident>> GetIncidentsAsync(
        string? status = null,
        string? priority = null,
        int? buildingId = null,
        string? searchTerm = null,
        string? technicianUserId = null)
    {
        var query = _context.TechnicalIncidents
            .AsNoTracking()
            .Include(x => x.Building)
            .Include(x => x.ReportedByUser)
            .Include(x => x.AssignedTechnicianUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);

        if (!string.IsNullOrWhiteSpace(priority))
            query = query.Where(x => x.Priority == priority);

        if (buildingId.HasValue && buildingId.Value > 0)
            query = query.Where(x => x.BuildingId == buildingId.Value);

        if (!string.IsNullOrWhiteSpace(technicianUserId))
            query = query.Where(x => x.AssignedTechnicianUserId == technicianUserId || x.ReportedByUserId == technicianUserId);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(x =>
                x.IncidentCode.ToLower().Contains(term) ||
                x.Title.ToLower().Contains(term) ||
                x.Location.ToLower().Contains(term) ||
                x.Category.ToLower().Contains(term) ||
                (x.Building != null && x.Building.BuildingName.ToLower().Contains(term)));
        }

        return await query.OrderByDescending(x => x.ReportedAt).ToListAsync();
    }

    public async Task<TechnicalIncident?> GetIncidentDetailsAsync(int incidentId)
    {
        if (incidentId <= 0) return null;

        return await _context.TechnicalIncidents
            .AsNoTracking()
            .Include(x => x.Building)
            .Include(x => x.ReportedByUser)
            .Include(x => x.AcknowledgedByUser)
            .Include(x => x.AssignedTechnicianUser)
            .Include(x => x.ClosedByUser)
            .Include(x => x.StatusHistory.OrderByDescending(h => h.ChangedAt))
                .ThenInclude(h => h.ChangedByUser)
            .Include(x => x.StatusHistory)
                .ThenInclude(h => h.PreviousTechnicianUser)
            .Include(x => x.StatusHistory)
                .ThenInclude(h => h.NewTechnicianUser)
            .FirstOrDefaultAsync(x => x.TechnicalIncidentId == incidentId);
    }

    // UC47: Kỹ thuật viên tạo báo cáo sự cố
    public async Task<TechnicalIncidentOperationResult> CreateIncidentAsync(
        string technicianUserId,
        int buildingId,
        string location,
        string category,
        string title,
        string description,
        string priority)
    {
        if (string.IsNullOrWhiteSpace(technicianUserId))
            return new(false, "Mã người dùng không hợp lệ.");

        if (buildingId <= 0)
            return new(false, "Vui lòng chọn tòa nhà hợp lệ.");

        var buildingExists = await _context.Buildings.AnyAsync(b => b.BuildingId == buildingId);
        if (!buildingExists)
            return new(false, "Tòa nhà không tồn tại.");

        if (string.IsNullOrWhiteSpace(location))
            return new(false, "Vui lòng nhập vị trí cụ thể xảy ra sự cố.");

        if (string.IsNullOrWhiteSpace(title))
            return new(false, "Vui lòng nhập tiêu đề sự cố.");

        if (string.IsNullOrWhiteSpace(description))
            return new(false, "Vui lòng nhập mô tả chi tiết sự cố.");

        var validPriority = TechnicalIncidentPriority.AllPriorities.Contains(priority)
            ? priority
            : TechnicalIncidentPriority.Normal;

        var validCategory = TechnicalIncidentCategory.AllCategories.Contains(category)
            ? category
            : TechnicalIncidentCategory.Other;

        var yearMonth = DateTime.UtcNow.ToString("yyyyMM");
        var totalThisMonth = await _context.TechnicalIncidents
            .CountAsync(x => x.IncidentCode.StartsWith($"INC-{yearMonth}"));
        var incidentCode = $"INC-{yearMonth}-{(totalThisMonth + 1):D3}";

        var incident = new TechnicalIncident
        {
            IncidentCode = incidentCode,
            BuildingId = buildingId,
            Location = location.Trim(),
            Category = validCategory,
            Title = title.Trim(),
            Description = description.Trim(),
            Priority = validPriority,
            Status = TechnicalIncidentStatus.Reported,
            ReportedByUserId = technicianUserId,
            ReportedAt = DateTime.UtcNow
        };

        var history = new TechnicalIncidentStatusHistory
        {
            PreviousStatus = null,
            NewStatus = TechnicalIncidentStatus.Reported,
            ChangedByUserId = technicianUserId,
            ChangedAt = DateTime.UtcNow,
            Note = "Kỹ thuật viên khởi tạo báo cáo sự cố kỹ thuật."
        };
        incident.StatusHistory.Add(history);

        _context.TechnicalIncidents.Add(incident);
        try
        {
            await _context.SaveChangesAsync();
            return new(true, null, incident.TechnicalIncidentId);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(false, "Dữ liệu sự cố đã bị thay đổi đồng thời bởi người khác. Vui lòng tải lại trang.");
        }
    }

    // UC48: Ban quản lý tiếp nhận báo cáo sự cố
    public async Task<TechnicalIncidentOperationResult> AcknowledgeIncidentAsync(
        int incidentId,
        string managerUserId,
        string? note = null,
        byte[]? rowVersion = null)
    {
        var incident = await _context.TechnicalIncidents
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.TechnicalIncidentId == incidentId);

        if (incident == null)
            return new(false, "Không tìm thấy sự cố kỹ thuật.");

        if (incident.Status == TechnicalIncidentStatus.Closed || incident.Status == TechnicalIncidentStatus.Cancelled)
            return new(false, "Không thể tiếp nhận sự cố đã đóng hoặc đã hủy.");

        if (incident.Status != TechnicalIncidentStatus.Reported)
            return new(false, $"Sự cố đang ở trạng thái '{incident.Status}', không cần tiếp nhận lại.");

        if (rowVersion != null && rowVersion.Length > 0 && incident.RowVersion.Length > 0 &&
            !incident.RowVersion.AsSpan().SequenceEqual(rowVersion))
            return new(false, "Dữ liệu sự cố đã bị thay đổi bởi người khác. Vui lòng tải lại trang.");

        var prevStatus = incident.Status;
        incident.Status = TechnicalIncidentStatus.Acknowledged;
        incident.AcknowledgedByUserId = managerUserId;
        incident.AcknowledgedAt = DateTime.UtcNow;

        var history = new TechnicalIncidentStatusHistory
        {
            TechnicalIncidentId = incident.TechnicalIncidentId,
            PreviousStatus = prevStatus,
            NewStatus = TechnicalIncidentStatus.Acknowledged,
            ChangedByUserId = managerUserId,
            ChangedAt = DateTime.UtcNow,
            Note = string.IsNullOrWhiteSpace(note) ? "Ban quản lý đã tiếp nhận báo cáo sự cố." : note.Trim()
        };
        _context.TechnicalIncidentStatusHistories.Add(history);

        try
        {
            await _context.SaveChangesAsync();
            return new(true, null, incident.TechnicalIncidentId);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(false, "Dữ liệu sự cố đã bị thay đổi đồng thời bởi người khác. Vui lòng tải lại trang.");
        }
    }

    // UC49: Ban quản lý phân công kỹ thuật viên xử lý
    public async Task<TechnicalIncidentOperationResult> AssignTechnicianAsync(
        int incidentId,
        string technicianUserId,
        string managerUserId,
        DateTime? scheduledDate = null,
        string? note = null,
        byte[]? rowVersion = null)
    {
        var incident = await _context.TechnicalIncidents
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.TechnicalIncidentId == incidentId);

        if (incident == null)
            return new(false, "Không tìm thấy sự cố kỹ thuật.");

        if (incident.Status == TechnicalIncidentStatus.Closed || incident.Status == TechnicalIncidentStatus.Cancelled)
            return new(false, "Không thể phân công sự cố đã đóng hoặc đã hủy.");

        if (string.IsNullOrWhiteSpace(technicianUserId))
            return new(false, "Vui lòng chọn kỹ thuật viên được phân công.");

        var technicianUser = await _userManager.FindByIdAsync(technicianUserId);
        if (technicianUser == null)
            return new(false, "Không tìm thấy kỹ thuật viên được chọn.");

        if (rowVersion != null && rowVersion.Length > 0 && incident.RowVersion.Length > 0 &&
            !incident.RowVersion.AsSpan().SequenceEqual(rowVersion))
            return new(false, "Dữ liệu sự cố đã bị thay đổi bởi người khác. Vui lòng tải lại trang.");

        var prevStatus = incident.Status;
        var prevTechnicianId = incident.AssignedTechnicianUserId;
        incident.Status = TechnicalIncidentStatus.Assigned;
        incident.AssignedTechnicianUserId = technicianUserId;
        incident.AssignedAt = DateTime.UtcNow;
        incident.ScheduledDate = scheduledDate;

        var scheduledText = scheduledDate.HasValue ? $" Hạn chót: {scheduledDate.Value:dd/MM/yyyy}." : "";
        var noteText = !string.IsNullOrWhiteSpace(note) ? $" Ghi chú: {note.Trim()}" : "";
        var historyNote = prevTechnicianId != null
            ? $"Ban quản lý chuyển giao phân công sang KTV {technicianUser.FullName}.{scheduledText}{noteText}"
            : $"Ban quản lý phân công cho KTV {technicianUser.FullName}.{scheduledText}{noteText}";

        var history = new TechnicalIncidentStatusHistory
        {
            TechnicalIncidentId = incident.TechnicalIncidentId,
            PreviousStatus = prevStatus,
            NewStatus = TechnicalIncidentStatus.Assigned,
            PreviousTechnicianUserId = prevTechnicianId,
            NewTechnicianUserId = technicianUserId,
            ChangedByUserId = managerUserId,
            ChangedAt = DateTime.UtcNow,
            Note = historyNote
        };
        _context.TechnicalIncidentStatusHistories.Add(history);

        try
        {
            await _context.SaveChangesAsync();
            return new(true, null, incident.TechnicalIncidentId);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(false, "Dữ liệu sự cố đã bị thay đổi đồng thời bởi người khác. Vui lòng tải lại trang.");
        }
    }

    // Kỹ thuật viên bắt đầu xử lý
    public async Task<TechnicalIncidentOperationResult> StartWorkAsync(
        int incidentId,
        string technicianUserId,
        string? note = null,
        byte[]? rowVersion = null)
    {
        var incident = await _context.TechnicalIncidents
            .FirstOrDefaultAsync(x => x.TechnicalIncidentId == incidentId);

        if (incident == null)
            return new(false, "Không tìm thấy sự cố kỹ thuật.");

        if (incident.AssignedTechnicianUserId != technicianUserId)
            return new(false, "Chỉ kỹ thuật viên được phân công mới có thể cập nhật tiến độ xử lý sự cố này.");

        if (incident.Status != TechnicalIncidentStatus.Assigned)
            return new(false, $"Sự cố đang ở trạng thái '{incident.Status}', không thể chuyển sang Đang xử lý.");

        if (rowVersion != null && rowVersion.Length > 0 && incident.RowVersion.Length > 0 &&
            !incident.RowVersion.AsSpan().SequenceEqual(rowVersion))
            return new(false, "Dữ liệu sự cố đã bị thay đổi bởi người khác. Vui lòng tải lại trang.");

        var prevStatus = incident.Status;
        incident.Status = TechnicalIncidentStatus.InProgress;

        var history = new TechnicalIncidentStatusHistory
        {
            TechnicalIncidentId = incident.TechnicalIncidentId,
            PreviousStatus = prevStatus,
            NewStatus = TechnicalIncidentStatus.InProgress,
            ChangedByUserId = technicianUserId,
            ChangedAt = DateTime.UtcNow,
            Note = string.IsNullOrWhiteSpace(note) ? "Kỹ thuật viên bắt đầu tiến hành kiểm tra và khắc phục sự cố." : note.Trim()
        };
        _context.TechnicalIncidentStatusHistories.Add(history);

        try
        {
            await _context.SaveChangesAsync();
            return new(true, null, incident.TechnicalIncidentId);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(false, "Dữ liệu sự cố đã bị thay đổi đồng thời bởi người khác. Vui lòng tải lại trang.");
        }
    }

    // Kỹ thuật viên hoàn tất và báo cáo kết quả
    public async Task<TechnicalIncidentOperationResult> ResolveIncidentAsync(
        int incidentId,
        string technicianUserId,
        string resolutionNotes,
        string? spareParts = null,
        decimal? materialCost = null,
        byte[]? rowVersion = null)
    {
        var incident = await _context.TechnicalIncidents
            .FirstOrDefaultAsync(x => x.TechnicalIncidentId == incidentId);

        if (incident == null)
            return new(false, "Không tìm thấy sự cố kỹ thuật.");

        if (incident.AssignedTechnicianUserId != technicianUserId)
            return new(false, "Chỉ kỹ thuật viên được phân công mới có thể báo cáo hoàn thành sự cố này.");

        if (incident.Status != TechnicalIncidentStatus.InProgress && incident.Status != TechnicalIncidentStatus.Assigned)
            return new(false, $"Sự cố đang ở trạng thái '{incident.Status}', không thể báo cáo hoàn thành.");

        if (string.IsNullOrWhiteSpace(resolutionNotes))
            return new(false, "Vui lòng nhập nội dung và kết quả xử lý sự cố.");

        if (materialCost.HasValue && materialCost.Value < 0)
            return new(false, "Chi phí vật tư không được âm.");

        if (rowVersion != null && rowVersion.Length > 0 && incident.RowVersion.Length > 0 &&
            !incident.RowVersion.AsSpan().SequenceEqual(rowVersion))
            return new(false, "Dữ liệu sự cố đã bị thay đổi bởi người khác. Vui lòng tải lại trang.");

        var prevStatus = incident.Status;
        incident.Status = TechnicalIncidentStatus.Resolved;
        incident.ResolvedAt = DateTime.UtcNow;
        incident.ResolutionNotes = resolutionNotes.Trim();
        incident.SpareParts = spareParts?.Trim();
        incident.MaterialCost = materialCost;

        var partsNote = !string.IsNullOrWhiteSpace(spareParts) ? $" Vật tư: {spareParts.Trim()}." : "";
        var costNote = materialCost.HasValue ? $" Chi phí: {materialCost.Value:N0} đ." : "";
        var historyNote = $"KTV hoàn tất xử lý sự cố: {resolutionNotes.Trim()}.{partsNote}{costNote}";

        var history = new TechnicalIncidentStatusHistory
        {
            TechnicalIncidentId = incident.TechnicalIncidentId,
            PreviousStatus = prevStatus,
            NewStatus = TechnicalIncidentStatus.Resolved,
            ChangedByUserId = technicianUserId,
            ChangedAt = DateTime.UtcNow,
            Note = historyNote
        };
        _context.TechnicalIncidentStatusHistories.Add(history);

        try
        {
            await _context.SaveChangesAsync();
            return new(true, null, incident.TechnicalIncidentId);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(false, "Dữ liệu sự cố đã bị thay đổi đồng thời bởi người khác. Vui lòng tải lại trang.");
        }
    }

    // UC50: Ban quản lý theo dõi và đóng nghiệm thu sự cố
    public async Task<TechnicalIncidentOperationResult> CloseIncidentAsync(
        int incidentId,
        string managerUserId,
        string? note = null,
        byte[]? rowVersion = null)
    {
        var incident = await _context.TechnicalIncidents
            .FirstOrDefaultAsync(x => x.TechnicalIncidentId == incidentId);

        if (incident == null)
            return new(false, "Không tìm thấy sự cố kỹ thuật.");

        if (incident.Status != TechnicalIncidentStatus.Resolved)
            return new(false, $"Chỉ có thể đóng nghiệm thu sự cố khi kỹ thuật viên đã báo cáo hoàn thành (trạng thái 'Resolved'). Hiện tại: '{incident.Status}'.");

        if (rowVersion != null && rowVersion.Length > 0 && incident.RowVersion.Length > 0 &&
            !incident.RowVersion.AsSpan().SequenceEqual(rowVersion))
            return new(false, "Dữ liệu sự cố đã bị thay đổi bởi người khác. Vui lòng tải lại trang.");

        var prevStatus = incident.Status;
        incident.Status = TechnicalIncidentStatus.Closed;
        incident.ClosedByUserId = managerUserId;
        incident.ClosedAt = DateTime.UtcNow;

        var history = new TechnicalIncidentStatusHistory
        {
            TechnicalIncidentId = incident.TechnicalIncidentId,
            PreviousStatus = prevStatus,
            NewStatus = TechnicalIncidentStatus.Closed,
            ChangedByUserId = managerUserId,
            ChangedAt = DateTime.UtcNow,
            Note = string.IsNullOrWhiteSpace(note) ? "Ban quản lý nghiệm thu thực tế và đóng sự cố kỹ thuật." : note.Trim()
        };
        _context.TechnicalIncidentStatusHistories.Add(history);

        try
        {
            await _context.SaveChangesAsync();
            return new(true, null, incident.TechnicalIncidentId);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(false, "Dữ liệu sự cố đã bị thay đổi đồng thời bởi người khác. Vui lòng tải lại trang.");
        }
    }

    public async Task<IEnumerable<ApplicationUser>> GetAvailableTechniciansAsync()
    {
        var technicians = await _userManager.GetUsersInRoleAsync(AppRoles.Technician);
        return technicians.Where(t => t.IsActive).OrderBy(t => t.FullName);
    }
}

