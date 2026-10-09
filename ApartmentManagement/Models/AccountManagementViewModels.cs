using System.ComponentModel.DataAnnotations;

namespace ApartmentManagement.Models;

public sealed record AccountListItemViewModel(
    string UserId, string FullName, string UserName, string? Email,
    bool IsActive, DateTime CreatedAt, IReadOnlyList<string> Roles);

public sealed record AccountDetailsViewModel(
    string UserId, string FullName, string UserName, string? Email,
    string? PhoneNumber, bool IsActive, DateTime CreatedAt, IReadOnlyList<string> Roles);

public sealed class ResetPasswordViewModel
{
    [Required] public string UserId { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), Display(Name = "Mật khẩu tạm thời")]
    public string TemporaryPassword { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), Compare(nameof(TemporaryPassword), ErrorMessage = "Xác nhận mật khẩu không khớp.")]
    [Display(Name = "Xác nhận mật khẩu")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class ChangeRoleViewModel
{
    [Required] public string UserId { get; set; } = string.Empty;
    [Required(ErrorMessage = "Vui lòng chọn role.")] public string Role { get; set; } = string.Empty;
}
