using ApartmentManagement.Common.Security;
using ApartmentManagement.Data;
using ApartmentManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Services;

public sealed class IncidentTicketService : IIncidentTicketService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<IncidentTicketService> _logger;

    public IncidentTicketService(
        ApplicationDbContext context,
        ILogger<IncidentTicketService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task<List<IncidentTicket>> GetForResidentAsync(string userId) =>
        TicketQuery()
            .Where(x => x.Resident.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();

    public Task<IncidentTicket?> GetForResidentByIdAsync(string userId, int id) =>
        TicketQuery().FirstOrDefaultAsync(x =>
            x.IncidentTicketId == id && x.Resident.UserId == userId);

    public async Task<List<IncidentTicket>> GetStaffQueueAsync(string userId)
    {
        if (!await IsTicketStaffAsync(userId)) return [];

        return await TicketQuery()
            .Where(x => x.Status == IncidentTicketStatus.Submitted ||
                (x.Status == IncidentTicketStatus.InProgress && x.AssignedToUserId == userId))
            .OrderBy(x => x.Status)
            .ThenBy(x => x.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<IncidentTicket?> GetForStaffByIdAsync(string userId, int id)
    {
        if (!await IsTicketStaffAsync(userId)) return null;

        return await TicketQuery().FirstOrDefaultAsync(x =>
            x.IncidentTicketId == id &&
            (x.Status == IncidentTicketStatus.Submitted ||
             (x.Status == IncidentTicketStatus.InProgress && x.AssignedToUserId == userId)));
    }

    public async Task<IncidentTicketResult> CreateAsync(
        string residentUserId,
        IncidentTicketInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Trim().Length > 120)
            return Failure("Tiêu đề sự cố là bắt buộc và không được vượt quá 120 ký tự.");
        if (string.IsNullOrWhiteSpace(input.Description) || input.Description.Trim().Length > 4000)
            return Failure("Nội dung mô tả là bắt buộc và không được vượt quá 4000 ký tự.");
        if (!Enum.IsDefined(input.Category))
            return Failure("Loại sự cố không hợp lệ.");

        var resident = await _context.Residents
            .FirstOrDefaultAsync(x => x.UserId == residentUserId);
        if (resident == null) return Failure("Không tìm thấy hồ sơ cư dân.");

        var hasActiveOccupancy = await _context.ApartmentResidents.AnyAsync(x =>
            x.ResidentId == resident.ResidentId &&
            x.ApartmentId == input.ApartmentId &&
            x.MoveOutDate == null);
        if (!hasActiveOccupancy)
            return Failure("Bạn chỉ có thể gửi sự cố cho căn hộ đang cư trú của mình.");

        var ticket = new IncidentTicket
        {
            TicketCode = $"INC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..24],
            ApartmentId = input.ApartmentId,
            ResidentId = resident.ResidentId,
            Category = input.Category,
            Title = input.Title.Trim(),
            Description = input.Description.Trim(),
            Status = IncidentTicketStatus.Submitted,
            CreatedAtUtc = DateTime.UtcNow
        };
        ticket.StatusHistory.Add(CreateHistory(
            null, IncidentTicketStatus.Submitted, residentUserId));
        _context.IncidentTickets.Add(ticket);

        try
        {
            await _context.SaveChangesAsync();
            return Success(ticket);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Lỗi lưu ticket sự cố của cư dân {ResidentId}", resident.ResidentId);
            return Failure("Không thể gửi phiếu sự cố. Vui lòng thử lại.");
        }
    }

    public async Task<IncidentTicketResult> ClaimAsync(
        int id,
        string staffUserId,
        byte[] rowVersion)
    {
        if (!await IsTicketStaffAsync(staffUserId))
            return Failure("Bạn không có quyền nhận phiếu sự cố.");

        var ticket = await _context.IncidentTickets
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.IncidentTicketId == id);
        if (ticket == null) return Failure("Không tìm thấy phiếu sự cố.");
        if (ticket.Status != IncidentTicketStatus.Submitted || ticket.AssignedToUserId != null)
            return Failure("Phiếu sự cố đã được nhân viên khác tiếp nhận.");
        if (!HasCurrentRowVersion(ticket, rowVersion))
            return ConcurrencyFailure();

        ticket.Status = IncidentTicketStatus.InProgress;
        ticket.AssignedToUserId = staffUserId;
        ticket.AssignedAtUtc = DateTime.UtcNow;
        ticket.StatusHistory.Add(CreateHistory(
            IncidentTicketStatus.Submitted, IncidentTicketStatus.InProgress, staffUserId));
        SetOriginalRowVersion(ticket, rowVersion);
        return await SaveAsync(ticket, "Không thể nhận phiếu sự cố. Vui lòng tải lại và thử lại.");
    }

    public async Task<IncidentTicketResult> CompleteAsync(
        int id,
        string staffUserId,
        byte[] rowVersion)
    {
        if (!await IsTicketStaffAsync(staffUserId))
            return Failure("Bạn không có quyền cập nhật phiếu sự cố.");

        var ticket = await _context.IncidentTickets
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.IncidentTicketId == id);
        if (ticket == null) return Failure("Không tìm thấy phiếu sự cố.");
        if (ticket.Status != IncidentTicketStatus.InProgress ||
            ticket.AssignedToUserId != staffUserId)
            return Failure("Chỉ nhân viên đang phụ trách mới có thể hoàn tất phiếu.");
        if (!HasCurrentRowVersion(ticket, rowVersion))
            return ConcurrencyFailure();

        ticket.Status = IncidentTicketStatus.Completed;
        ticket.CompletedAtUtc = DateTime.UtcNow;
        ticket.StatusHistory.Add(CreateHistory(
            IncidentTicketStatus.InProgress, IncidentTicketStatus.Completed, staffUserId));
        SetOriginalRowVersion(ticket, rowVersion);
        return await SaveAsync(ticket, "Không thể hoàn tất phiếu sự cố. Vui lòng tải lại và thử lại.");
    }

    public async Task<IncidentTicketResult> RateAsync(
        int id,
        string residentUserId,
        byte rating,
        string? feedback,
        byte[] rowVersion)
    {
        if (rating is < 1 or > 5) return Failure("Đánh giá phải từ 1 đến 5 sao.");
        if (feedback?.Trim().Length > 1000)
            return Failure("Nhận xét không được vượt quá 1000 ký tự.");

        var ticket = await _context.IncidentTickets
            .FirstOrDefaultAsync(x =>
                x.IncidentTicketId == id && x.Resident.UserId == residentUserId);
        if (ticket == null) return Failure("Không tìm thấy phiếu sự cố.");
        if (ticket.Status != IncidentTicketStatus.Completed)
            return Failure("Chỉ có thể đánh giá phiếu đã hoàn tất.");
        if (ticket.Rating.HasValue)
            return Failure("Phiếu sự cố này đã được đánh giá.");
        if (!HasCurrentRowVersion(ticket, rowVersion))
            return ConcurrencyFailure();

        ticket.Rating = rating;
        ticket.ResidentFeedback = string.IsNullOrWhiteSpace(feedback) ? null : feedback.Trim();
        ticket.RatedAtUtc = DateTime.UtcNow;
        SetOriginalRowVersion(ticket, rowVersion);
        try
        {
            await _context.SaveChangesAsync();
            return Success(ticket);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            _logger.LogInformation(exception, "Xung đột đánh giá ticket {TicketId}", id);
            return ConcurrencyFailure();
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Lỗi lưu đánh giá ticket {TicketId}", id);
            return Failure("Không thể lưu đánh giá. Vui lòng thử lại.");
        }
    }

    private IQueryable<IncidentTicket> TicketQuery() =>
        _context.IncidentTickets.AsNoTracking()
            .Include(x => x.Apartment).ThenInclude(x => x.Building)
            .Include(x => x.Resident).ThenInclude(x => x.User)
            .Include(x => x.AssignedToUser)
            .Include(x => x.StatusHistory).ThenInclude(x => x.ChangedByUser);

    private Task<bool> IsTicketStaffAsync(string userId) =>
        _context.UserRoles
            .Join(_context.Roles, userRole => userRole.RoleId, role => role.Id,
                (userRole, role) => new { userRole.UserId, role.Name })
            .AnyAsync(x => x.UserId == userId &&
                (x.Name == AppRoles.Technician ||
                 x.Name == AppRoles.BuildingManager ||
                 x.Name == AppRoles.SuperAdmin));

    private bool HasCurrentRowVersion(IncidentTicket ticket, byte[]? rowVersion) =>
        !_context.Database.IsRelational() ||
        (rowVersion is { Length: > 0 } && ticket.RowVersion.AsSpan().SequenceEqual(rowVersion));

    private void SetOriginalRowVersion(IncidentTicket ticket, byte[] rowVersion)
    {
        if (_context.Database.IsRelational())
            _context.Entry(ticket).Property(x => x.RowVersion).OriginalValue = rowVersion;
    }

    private async Task<IncidentTicketResult> SaveAsync(
        IncidentTicket ticket,
        string errorMessage)
    {
        try
        {
            await _context.SaveChangesAsync();
            return Success(ticket);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            _logger.LogInformation(exception, "Xung đột cập nhật ticket {TicketId}", ticket.IncidentTicketId);
            return ConcurrencyFailure();
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Lỗi cập nhật ticket {TicketId}", ticket.IncidentTicketId);
            return Failure(errorMessage);
        }
    }

    private static IncidentTicketStatusHistory CreateHistory(
        IncidentTicketStatus? previousStatus,
        IncidentTicketStatus newStatus,
        string changedByUserId) =>
        new()
        {
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            ChangedAtUtc = DateTime.UtcNow,
            ChangedByUserId = changedByUserId
        };

    private static IncidentTicketResult Success(IncidentTicket ticket) => new(true, null, ticket);
    private static IncidentTicketResult Failure(string errorMessage) => new(false, errorMessage, null);
    private static IncidentTicketResult ConcurrencyFailure() =>
        Failure("Phiếu sự cố đã được cập nhật đồng thời. Tải lại dữ liệu trước khi thử lại.");
}
