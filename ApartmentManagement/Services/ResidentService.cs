using ApartmentManagement.Common.Security;
using ApartmentManagement.Data;
using ApartmentManagement.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ApartmentManagement.Services;

public sealed class ResidentService : IResidentService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ResidentService> _logger;

    public ResidentService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<ResidentService> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<List<Resident>> GetAllAsync(string? searchTerm = null)
    {
        var query = ResidentQuery();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(x =>
                x.User.FullName.Contains(term) ||
                (x.User.Email != null && x.User.Email.Contains(term)) ||
                x.CitizenId.Contains(term));
        }

        return await query.OrderBy(x => x.User.FullName).ToListAsync();
    }

    public Task<Resident?> GetByIdAsync(int id) =>
        ResidentQuery().FirstOrDefaultAsync(x => x.ResidentId == id);

    public async Task<(bool Success, string? ErrorMessage, Resident? Resident)> CreateAsync(
        ResidentCreateViewModel model)
    {
        var email = model.Email.Trim();
        var citizenId = model.CitizenId.Trim();
        if (await _userManager.FindByEmailAsync(email) != null)
            return (false, "Email đã được sử dụng.", null);
        if (await _context.Residents.AnyAsync(x => x.CitizenId == citizenId))
            return (false, "Số CCCD/CMND đã tồn tại.", null);

        await using var transaction = await BeginTransactionAsync();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = model.FullName.Trim(),
            EmailConfirmed = true,
            IsActive = true
        };

        var createResult = await _userManager.CreateAsync(user, model.TemporaryPassword);
        if (!createResult.Succeeded)
            return (false, IdentityErrors(createResult), null);

        var roleResult = await _userManager.AddToRoleAsync(user, AppRoles.Resident);
        if (!roleResult.Succeeded)
            return (false, IdentityErrors(roleResult), null);

        var resident = new Resident
        {
            UserId = user.Id,
            User = user,
            CitizenId = citizenId,
            DateOfBirth = model.DateOfBirth,
            Gender = Clean(model.Gender),
            Address = Clean(model.Address),
            EmergencyContact = Clean(model.EmergencyContact)
        };

        _context.Residents.Add(resident);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Lỗi toàn vẹn dữ liệu khi tạo cư dân cho {Email}", email);
            return (false, "Không thể lưu hồ sơ cư dân vì dữ liệu bị trùng hoặc không hợp lệ.", null);
        }
        if (transaction != null) await transaction.CommitAsync();
        _logger.LogInformation("Đã cấp tài khoản cư dân {ResidentId} cho {Email}", resident.ResidentId, email);
        return (true, null, resident);
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateAsync(ResidentEditViewModel model)
    {
        var resident = await _context.Residents.Include(x => x.User)
            .FirstOrDefaultAsync(x => x.ResidentId == model.ResidentId);
        if (resident == null) return (false, "Không tìm thấy cư dân.");

        var citizenId = model.CitizenId.Trim();
        if (await _context.Residents.AnyAsync(x =>
                x.CitizenId == citizenId && x.ResidentId != model.ResidentId))
            return (false, "Số CCCD/CMND đã tồn tại.");

        var email = model.Email.Trim();
        var emailOwner = await _userManager.FindByEmailAsync(email);
        if (emailOwner != null && emailOwner.Id != resident.UserId)
            return (false, "Email đã được sử dụng.");

        await using var transaction = await BeginTransactionAsync();
        if (!string.Equals(resident.User.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var emailResult = await _userManager.SetEmailAsync(resident.User, email);
            if (!emailResult.Succeeded) return (false, IdentityErrors(emailResult));
            var usernameResult = await _userManager.SetUserNameAsync(resident.User, email);
            if (!usernameResult.Succeeded) return (false, IdentityErrors(usernameResult));
        }

        resident.User.FullName = model.FullName.Trim();
        resident.CitizenId = citizenId;
        resident.DateOfBirth = model.DateOfBirth;
        resident.Gender = Clean(model.Gender);
        resident.Address = Clean(model.Address);
        resident.EmergencyContact = Clean(model.EmergencyContact);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Lỗi toàn vẹn dữ liệu khi cập nhật cư dân {ResidentId}", model.ResidentId);
            return (false, "Không thể cập nhật vì dữ liệu bị trùng hoặc không hợp lệ.");
        }
        if (transaction != null) await transaction.CommitAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteAsync(int id)
    {
        var resident = await _context.Residents.Include(x => x.User)
            .Include(x => x.ApartmentResidents)
            .FirstOrDefaultAsync(x => x.ResidentId == id);
        if (resident == null) return (false, "Không tìm thấy cư dân.");
        if (resident.ApartmentResidents.Count > 0)
            return (false, "Không thể xóa cư dân đang có lịch sử cư trú. Hãy xử lý hồ sơ cư trú trước.");

        var result = await _userManager.DeleteAsync(resident.User);
        if (!result.Succeeded) return (false, IdentityErrors(result));
        return (true, null);
    }

    private IQueryable<Resident> ResidentQuery() => _context.Residents.AsNoTracking()
        .Include(x => x.User)
        .Include(x => x.ApartmentResidents).ThenInclude(x => x.Apartment).ThenInclude(x => x.Building);

    private async Task<IDbContextTransaction?> BeginTransactionAsync() =>
        _context.Database.IsRelational() ? await _context.Database.BeginTransactionAsync() : null;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string IdentityErrors(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(x => x.Description));
}
