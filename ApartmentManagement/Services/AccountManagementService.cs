using ApartmentManagement.Common.Security;
using ApartmentManagement.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Services;

public sealed class AccountManagementService : IAccountManagementService
{
    private readonly UserManager<ApplicationUser> _userManager;
    public AccountManagementService(UserManager<ApplicationUser> userManager) => _userManager = userManager;

    public async Task<List<AccountListItemViewModel>> GetAllAsync(string? searchTerm = null)
    {
        var query = _userManager.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(x => x.FullName.Contains(term) ||
                (x.Email != null && x.Email.Contains(term)) ||
                (x.UserName != null && x.UserName.Contains(term)));
        }

        var users = await query.OrderBy(x => x.FullName).ToListAsync();
        var result = new List<AccountListItemViewModel>(users.Count);
        foreach (var user in users)
            result.Add(new(user.Id, user.FullName, user.UserName ?? string.Empty, user.Email,
                user.IsActive, user.CreatedAt, (await _userManager.GetRolesAsync(user)).ToList()));
        return result;
    }

    public async Task<AccountDetailsViewModel?> GetByIdAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        return user == null ? null : new(
            user.Id, user.FullName, user.UserName ?? string.Empty, user.Email, user.PhoneNumber,
            user.IsActive, user.CreatedAt, (await _userManager.GetRolesAsync(user)).ToList());
    }

    public async Task<(bool Success, string? ErrorMessage)> SetActiveAsync(
        string userId, bool isActive, string currentUserId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return (false, "Không tìm thấy tài khoản.");
        if (!isActive && userId == currentUserId)
            return (false, "Bạn không thể tự vô hiệu hóa tài khoản đang đăng nhập.");
        if (!isActive && await IsLastActiveSuperAdminAsync(user))
            return (false, "Không thể vô hiệu hóa SuperAdmin cuối cùng.");

        user.IsActive = isActive;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded) return (false, Errors(result));
        result = await _userManager.UpdateSecurityStampAsync(user);
        return result.Succeeded ? (true, null) : (false, Errors(result));
    }

    public async Task<(bool Success, string? ErrorMessage)> ResetPasswordAsync(
        string userId, string temporaryPassword)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return (false, "Không tìm thấy tài khoản.");
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, temporaryPassword);
        return result.Succeeded ? (true, null) : (false, Errors(result));
    }

    public async Task<(bool Success, string? ErrorMessage)> ChangeRoleAsync(
        string userId, string role, string currentUserId)
    {
        if (!AppRoles.AllRoles.Contains(role)) return (false, "Role không hợp lệ.");
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return (false, "Không tìm thấy tài khoản.");
        var currentRoles = await _userManager.GetRolesAsync(user);
        if (userId == currentUserId && currentRoles.Contains(AppRoles.SuperAdmin) && role != AppRoles.SuperAdmin)
            return (false, "Bạn không thể tự gỡ quyền SuperAdmin của mình.");
        if (currentRoles.Contains(AppRoles.SuperAdmin) && role != AppRoles.SuperAdmin &&
            await IsLastActiveSuperAdminAsync(user))
            return (false, "Không thể thay đổi role của SuperAdmin cuối cùng.");

        var result = await _userManager.RemoveFromRolesAsync(user, currentRoles);
        if (!result.Succeeded) return (false, Errors(result));
        result = await _userManager.AddToRoleAsync(user, role);
        if (!result.Succeeded) return (false, Errors(result));
        result = await _userManager.UpdateSecurityStampAsync(user);
        return result.Succeeded ? (true, null) : (false, Errors(result));
    }

    private async Task<bool> IsLastActiveSuperAdminAsync(ApplicationUser user)
    {
        if (!await _userManager.IsInRoleAsync(user, AppRoles.SuperAdmin)) return false;
        if (!user.IsActive) return false;
        var admins = await _userManager.GetUsersInRoleAsync(AppRoles.SuperAdmin);
        return admins.Count(x => x.IsActive) <= 1;
    }

    private static string Errors(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(x => x.Description));
}
