using System.ComponentModel.DataAnnotations;
using ApartmentManagement.Common.Validation;

namespace ApartmentManagement.Models;

public sealed class ResidentCreateViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
    [StringLength(ResidentValidation.FullNameMaxLength)]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu tạm thời.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu tạm thời")]
    public string TemporaryPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số CCCD/CMND.")]
    [StringLength(ResidentValidation.CitizenIdMaxLength)]
    [RegularExpression(ResidentValidation.CitizenIdPattern, ErrorMessage = "CCCD/CMND phải gồm 9 hoặc 12 chữ số.")]
    [Display(Name = "CCCD/CMND")]
    public string CitizenId { get; set; } = string.Empty;

    [DataType(DataType.Date), ValidDateOfBirth]
    [Display(Name = "Ngày sinh")]
    public DateTime? DateOfBirth { get; set; }

    [StringLength(ResidentValidation.GenderMaxLength)]
    [Display(Name = "Giới tính")]
    public string? Gender { get; set; }

    [StringLength(ResidentValidation.AddressMaxLength)]
    [Display(Name = "Địa chỉ")]
    public string? Address { get; set; }

    [StringLength(ResidentValidation.PhoneMaxLength)]
    [RegularExpression(ResidentValidation.PhonePattern, ErrorMessage = "Số điện thoại phải gồm 9-15 chữ số và có thể bắt đầu bằng dấu +.")]
    [Display(Name = "Liên hệ khẩn cấp")]
    public string? EmergencyContact { get; set; }
}

public sealed class ResidentEditViewModel
{
    public int ResidentId { get; set; }

    [Required, StringLength(ResidentValidation.FullNameMaxLength)]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(ResidentValidation.CitizenIdMaxLength)]
    [RegularExpression(ResidentValidation.CitizenIdPattern, ErrorMessage = "CCCD/CMND phải gồm 9 hoặc 12 chữ số.")]
    [Display(Name = "CCCD/CMND")]
    public string CitizenId { get; set; } = string.Empty;

    [DataType(DataType.Date), ValidDateOfBirth]
    [Display(Name = "Ngày sinh")]
    public DateTime? DateOfBirth { get; set; }

    [StringLength(ResidentValidation.GenderMaxLength)]
    [Display(Name = "Giới tính")]
    public string? Gender { get; set; }

    [StringLength(ResidentValidation.AddressMaxLength)]
    [Display(Name = "Địa chỉ")]
    public string? Address { get; set; }

    [StringLength(ResidentValidation.PhoneMaxLength)]
    [RegularExpression(ResidentValidation.PhonePattern, ErrorMessage = "Số điện thoại phải gồm 9-15 chữ số và có thể bắt đầu bằng dấu +.")]
    [Display(Name = "Liên hệ khẩn cấp")]
    public string? EmergencyContact { get; set; }
}
