using ApartmentManagement.Models;

namespace ApartmentManagement.Services;

public interface IAccountManagementService
{
    Task<List<AccountListItemViewModel>> GetAllAsync(string? searchTerm = null);
    Task<AccountDetailsViewModel?> GetByIdAsync(string userId);
    Task<(bool Success, string? ErrorMessage)> SetActiveAsync(string userId, bool isActive, string currentUserId);
    Task<(bool Success, string? ErrorMessage)> ResetPasswordAsync(string userId, string temporaryPassword);
    Task<(bool Success, string? ErrorMessage)> ChangeRoleAsync(string userId, string role, string currentUserId);
}
