using System.Data;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Data;
using ApartmentManagement.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ApartmentManagement.Services;

public sealed class ContractService : IContractService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ContractService> _logger;

    public ContractService(ApplicationDbContext context, ILogger<ContractService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task<List<ApartmentContract>> GetAllAsync() =>
        ContractQuery().OrderByDescending(x => x.CreatedAtUtc).ToListAsync();

    public async Task<ContractPageResult> GetPageAsync(
        string? searchTerm,
        ApartmentContractStatus? status,
        int page,
        int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = ContractQuery();
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(x =>
                x.ContractCode.Contains(term) ||
                x.Apartment.ApartmentCode.Contains(term) ||
                x.Parties.Any(p => p.Resident.User.FullName.Contains(term)));
        }

        var count = await query.CountAsync();
        var contracts = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return new ContractPageResult(contracts, page, pageSize, count);
    }

    public Task<ApartmentContract?> GetByIdAsync(int id) =>
        ContractQuery().FirstOrDefaultAsync(x => x.ApartmentContractId == id);

    public Task<List<ApartmentContract>> GetForResidentUserAsync(string userId) =>
        ContractQuery()
            .Where(x => x.Parties.Any(p => p.Resident.UserId == userId))
            .OrderByDescending(x => x.StartDate)
            .ToListAsync();

    public async Task<ContractServiceResult> CreateDraftAsync(
        ContractDraftInput input,
        string actorUserId)
    {
        if (input == null) return Failure("Dữ liệu hợp đồng không hợp lệ.");
        var validationError = await ValidateDraftAsync(input, actorUserId);
        if (validationError != null) return Failure(validationError);

        var contract = new ApartmentContract
        {
            ApartmentId = input.ApartmentId,
            StartDate = input.StartDate.Date,
            EndDate = input.EndDate?.Date,
            MonthlyRent = input.MonthlyRent,
            DepositAmount = input.DepositAmount,
            Status = ApartmentContractStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = actorUserId
        };
        contract.SetContractCode(input.ContractCode);
        contract.Parties = CreateParties(input.Parties);
        contract.StatusHistory.Add(CreateHistory(
            null, ApartmentContractStatus.Draft, actorUserId, null));

        _context.ApartmentContracts.Add(contract);
        try
        {
            await _context.SaveChangesAsync();
            return Success(contract);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Lỗi dữ liệu khi tạo hợp đồng thuê {ContractCode}", contract.ContractCode);
            return Failure("Không thể tạo hợp đồng vì dữ liệu bị trùng hoặc không hợp lệ.");
        }
    }

    public async Task<ContractServiceResult> UpdateDraftAsync(
        int id,
        ContractDraftInput input,
        string actorUserId,
        byte[] expectedRowVersion)
    {
        if (input == null) return Failure("Dữ liệu hợp đồng không hợp lệ.");
        var contract = await _context.ApartmentContracts
            .Include(x => x.Parties)
            .FirstOrDefaultAsync(x => x.ApartmentContractId == id);
        if (contract == null) return Failure("Không tìm thấy hợp đồng.");
        if (contract.Status != ApartmentContractStatus.Draft)
            return Failure("Chỉ có thể chỉnh sửa hợp đồng ở trạng thái nháp.");
        if (!HasExpectedRowVersion(contract, expectedRowVersion))
            return ConcurrencyFailure();

        var validationError = await ValidateDraftAsync(input, actorUserId, id);
        if (validationError != null) return Failure(validationError);

        contract.SetContractCode(input.ContractCode);
        contract.ApartmentId = input.ApartmentId;
        contract.StartDate = input.StartDate.Date;
        contract.EndDate = input.EndDate?.Date;
        contract.MonthlyRent = input.MonthlyRent;
        contract.DepositAmount = input.DepositAmount;
        contract.UpdatedAtUtc = DateTime.UtcNow;
        contract.UpdatedByUserId = actorUserId;
        _context.ContractParties.RemoveRange(contract.Parties);
        contract.Parties = CreateParties(input.Parties);
        SetOriginalRowVersion(contract, expectedRowVersion);

        return await SaveAsync(contract, "Không thể cập nhật hợp đồng vì dữ liệu bị trùng hoặc không hợp lệ.");
    }

    public async Task<ContractServiceResult> ActivateAsync(
        int id,
        string actorUserId,
        byte[] expectedRowVersion)
    {
        try
        {
            await using var transaction = await BeginSerializableTransactionAsync();
            var contract = await _context.ApartmentContracts
                .Include(x => x.Parties)
                .FirstOrDefaultAsync(x => x.ApartmentContractId == id);
            if (contract == null) return Failure("Không tìm thấy hợp đồng.");
            if (contract.Status != ApartmentContractStatus.Draft)
                return Failure("Chỉ có thể kích hoạt hợp đồng ở trạng thái nháp.");
            if (!HasExpectedRowVersion(contract, expectedRowVersion))
                return ConcurrencyFailure();

            var validationError = await ValidateStoredDraftAsync(contract, actorUserId);
            if (validationError != null) return Failure(validationError);

            var overlaps = await _context.ApartmentContracts.AnyAsync(existing =>
                existing.ApartmentId == contract.ApartmentId &&
                existing.ApartmentContractId != id &&
                existing.Status == ApartmentContractStatus.Active &&
                (existing.EndDate == null || existing.EndDate >= contract.StartDate) &&
                (contract.EndDate == null || existing.StartDate <= contract.EndDate));
            if (overlaps)
                return Failure("Thời hạn hợp đồng bị trùng với một hợp đồng đang hiệu lực của căn hộ.");

            var previousStatus = contract.Status;
            contract.Status = ApartmentContractStatus.Active;
            contract.UpdatedAtUtc = DateTime.UtcNow;
            contract.UpdatedByUserId = actorUserId;
            contract.StatusHistory.Add(CreateHistory(
                previousStatus, contract.Status, actorUserId, null));
            SetOriginalRowVersion(contract, expectedRowVersion);

            var result = await SaveAsync(
                contract,
                "Không thể kích hoạt hợp đồng vì dữ liệu bị trùng hoặc không hợp lệ.");
            if (result.Success && transaction != null) await transaction.CommitAsync();
            return result;
        }
        catch (SqlException exception) when (exception.Number == 1205)
        {
            _logger.LogInformation(exception, "Deadlock khi kích hoạt hợp đồng {ContractId}", id);
            return Failure("Hợp đồng đang được xử lý đồng thời. Tải lại dữ liệu trước khi thử lại.");
        }
    }

    public Task<ContractServiceResult> CompleteAsync(
        int id,
        string actorUserId,
        byte[] expectedRowVersion,
        string? reason = null) =>
        ChangeStatusAsync(
            id,
            actorUserId,
            expectedRowVersion,
            ApartmentContractStatus.Active,
            ApartmentContractStatus.Completed,
            reason,
            reasonRequired: false,
            earlyCompletionRequiresReason: true);

    public Task<ContractServiceResult> TerminateAsync(
        int id,
        string actorUserId,
        byte[] expectedRowVersion,
        string reason) =>
        ChangeStatusAsync(
            id,
            actorUserId,
            expectedRowVersion,
            ApartmentContractStatus.Active,
            ApartmentContractStatus.Terminated,
            reason,
            reasonRequired: true,
            earlyCompletionRequiresReason: false);

    public Task<ContractServiceResult> CancelAsync(
        int id,
        string actorUserId,
        byte[] expectedRowVersion,
        string reason) =>
        ChangeStatusAsync(
            id,
            actorUserId,
            expectedRowVersion,
            ApartmentContractStatus.Draft,
            ApartmentContractStatus.Cancelled,
            reason,
            reasonRequired: true,
            earlyCompletionRequiresReason: false);

    private async Task<ContractServiceResult> ChangeStatusAsync(
        int id,
        string actorUserId,
        byte[] expectedRowVersion,
        ApartmentContractStatus requiredStatus,
        ApartmentContractStatus newStatus,
        string? reason,
        bool reasonRequired,
        bool earlyCompletionRequiresReason)
    {
        var contract = await _context.ApartmentContracts
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.ApartmentContractId == id);
        if (contract == null) return Failure("Không tìm thấy hợp đồng.");
        if (contract.Status != requiredStatus)
            return Failure("Không thể chuyển hợp đồng từ trạng thái hiện tại.");
        if (!HasExpectedRowVersion(contract, expectedRowVersion))
            return ConcurrencyFailure();

        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (reasonRequired && normalizedReason == null)
            return Failure("Vui lòng nhập lý do.");
        if (normalizedReason?.Length > 1000)
            return Failure("Lý do không được vượt quá 1000 ký tự.");
        if (earlyCompletionRequiresReason &&
            contract.EndDate.HasValue &&
            contract.EndDate.Value.Date > DateTime.UtcNow.Date &&
            normalizedReason == null)
            return Failure("Vui lòng nhập lý do khi kết thúc hợp đồng trước hạn.");
        if (!await IsSuperAdminAsync(actorUserId))
            return Failure("Chỉ SuperAdmin mới được quản lý hợp đồng.");

        var previousStatus = contract.Status;
        contract.Status = newStatus;
        contract.UpdatedAtUtc = DateTime.UtcNow;
        contract.UpdatedByUserId = actorUserId;
        contract.StatusHistory.Add(CreateHistory(
            previousStatus, newStatus, actorUserId, normalizedReason));
        SetOriginalRowVersion(contract, expectedRowVersion);

        return await SaveAsync(contract, "Không thể cập nhật trạng thái hợp đồng.");
    }

    private async Task<string?> ValidateDraftAsync(
        ContractDraftInput? input,
        string actorUserId,
        int? currentContractId = null)
    {
        if (input == null) return "Dữ liệu hợp đồng không hợp lệ.";
        if (string.IsNullOrWhiteSpace(input.ContractCode) || input.ContractCode.Trim().Length > 50)
            return "Mã hợp đồng là bắt buộc và không được vượt quá 50 ký tự.";
        if (input.ApartmentId <= 0 ||
            !await _context.Apartments.AnyAsync(x => x.ApartmentId == input.ApartmentId))
            return "Căn hộ không tồn tại.";
        if (input.EndDate.HasValue && input.EndDate.Value.Date < input.StartDate.Date)
            return "Ngày kết thúc không thể trước ngày bắt đầu.";
        if (input.MonthlyRent <= 0)
            return "Tiền thuê hàng tháng phải lớn hơn 0.";
        if (input.DepositAmount < 0)
            return "Tiền đặt cọc không thể nhỏ hơn 0.";
        if (!await IsSuperAdminAsync(actorUserId))
            return "Chỉ SuperAdmin mới được quản lý hợp đồng.";

        var normalizedCode = input.ContractCode.Trim().ToUpperInvariant();
        if (await _context.ApartmentContracts.AnyAsync(x =>
                x.ContractCodeNormalized == normalizedCode &&
                x.ApartmentContractId != currentContractId))
            return "Mã hợp đồng đã tồn tại.";

        var partiesError = await ValidatePartiesAsync(input.Parties);
        if (partiesError != null) return partiesError;

        return null;
    }

    private async Task<string?> ValidateStoredDraftAsync(
        ApartmentContract contract,
        string actorUserId)
    {
        var input = new ContractDraftInput
        {
            ContractCode = contract.ContractCode,
            ApartmentId = contract.ApartmentId,
            StartDate = contract.StartDate,
            EndDate = contract.EndDate,
            MonthlyRent = contract.MonthlyRent,
            DepositAmount = contract.DepositAmount,
            Parties = contract.Parties.Select(x => new ContractPartyInput(x.ResidentId, x.Role)).ToList()
        };
        return await ValidateDraftAsync(input, actorUserId, contract.ApartmentContractId);
    }

    private async Task<string?> ValidatePartiesAsync(IReadOnlyList<ContractPartyInput>? parties)
    {
        if (parties == null)
            return "Danh sách bên tham gia không hợp lệ.";
        if (parties.Count == 0)
            return "Hợp đồng phải có ít nhất một bên cho thuê và một bên thuê.";
        if (parties.Any(x => x.ResidentId <= 0) ||
            parties.Select(x => x.ResidentId).Distinct().Count() != parties.Count)
            return "Mỗi cư dân chỉ được khai báo một lần trong cùng hợp đồng.";
        if (!parties.Any(x => x.Role == ContractPartyRole.Lessor) ||
            !parties.Any(x => x.Role == ContractPartyRole.Lessee))
            return "Hợp đồng phải có ít nhất một bên cho thuê và một bên thuê.";
        if (parties.Any(x => !Enum.IsDefined(x.Role)))
            return "Vai trò của bên tham gia không hợp lệ.";

        var residentIds = parties.Select(x => x.ResidentId).Distinct().ToArray();
        var existingCount = await _context.Residents.CountAsync(x => residentIds.Contains(x.ResidentId));
        return existingCount == residentIds.Length ? null : "Một hoặc nhiều cư dân không tồn tại.";
    }

    private IQueryable<ApartmentContract> ContractQuery() =>
        _context.ApartmentContracts.AsNoTracking()
            .Include(x => x.Apartment).ThenInclude(x => x.Building)
            .Include(x => x.Parties).ThenInclude(x => x.Resident).ThenInclude(x => x.User)
            .Include(x => x.StatusHistory).ThenInclude(x => x.ChangedByUser);

    private static List<ContractParty> CreateParties(IReadOnlyList<ContractPartyInput> parties) =>
        parties.Select(x => new ContractParty { ResidentId = x.ResidentId, Role = x.Role }).ToList();

    private static ContractStatusHistory CreateHistory(
        ApartmentContractStatus? previousStatus,
        ApartmentContractStatus newStatus,
        string actorUserId,
        string? reason) =>
        new()
        {
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            ChangedAtUtc = DateTime.UtcNow,
            ChangedByUserId = actorUserId,
            Reason = reason
        };

    private static bool HasExpectedRowVersion(ApartmentContract contract, byte[]? expectedRowVersion) =>
        expectedRowVersion is { Length: > 0 } &&
        contract.RowVersion.AsSpan().SequenceEqual(expectedRowVersion);

    private void SetOriginalRowVersion(ApartmentContract contract, byte[] expectedRowVersion)
    {
        if (_context.Database.IsRelational())
            _context.Entry(contract).Property(x => x.RowVersion).OriginalValue = expectedRowVersion;
    }

    private async Task<IDbContextTransaction?> BeginSerializableTransactionAsync() =>
        _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable)
            : null;

    private Task<bool> IsSuperAdminAsync(string actorUserId) =>
        _context.UserRoles
            .Join(_context.Roles, userRole => userRole.RoleId, role => role.Id,
                (userRole, role) => new { userRole.UserId, role.Name })
            .AnyAsync(x => x.UserId == actorUserId && x.Name == AppRoles.SuperAdmin);

    private async Task<ContractServiceResult> SaveAsync(
        ApartmentContract contract,
        string dataErrorMessage)
    {
        try
        {
            await _context.SaveChangesAsync();
            return Success(contract);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            _logger.LogInformation(exception, "Xung đột cập nhật hợp đồng {ContractId}", contract.ApartmentContractId);
            return ConcurrencyFailure();
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Lỗi toàn vẹn dữ liệu hợp đồng {ContractId}", contract.ApartmentContractId);
            return Failure(dataErrorMessage);
        }
    }

    private static ContractServiceResult Success(ApartmentContract contract) =>
        new(true, null, contract);

    private static ContractServiceResult Failure(string errorMessage) =>
        new(false, errorMessage, null);

    private static ContractServiceResult ConcurrencyFailure() =>
        Failure("Hợp đồng đã được cập nhật bởi người khác. Tải lại dữ liệu trước khi thử lại.");
}
