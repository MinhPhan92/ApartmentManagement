using System.ComponentModel.DataAnnotations;
using ApartmentManagement.Services;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ApartmentManagement.Models;

public sealed class ContractListViewModel
{
    public required ContractPageResult PageResult { get; init; }
    public string? SearchTerm { get; init; }
    public ApartmentContractStatus? Status { get; init; }
    public IReadOnlyList<SelectListItem> StatusOptions { get; init; } = [];
}

public sealed class ContractEditViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập mã hợp đồng.")]
    [StringLength(50, ErrorMessage = "Mã hợp đồng không được vượt quá 50 ký tự.")]
    [Display(Name = "Mã hợp đồng")]
    public string ContractCode { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn căn hộ.")]
    [Display(Name = "Căn hộ")]
    public int ApartmentId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập ngày bắt đầu.")]
    [DataType(DataType.Date)]
    [Display(Name = "Ngày bắt đầu")]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [DataType(DataType.Date)]
    [Display(Name = "Ngày kết thúc")]
    public DateTime? EndDate { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999999", ErrorMessage = "Tiền thuê hàng tháng phải lớn hơn 0.")]
    [Display(Name = "Tiền thuê hàng tháng")]
    public decimal MonthlyRent { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999", ErrorMessage = "Tiền đặt cọc không thể nhỏ hơn 0.")]
    [Display(Name = "Tiền đặt cọc")]
    public decimal DepositAmount { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn bên cho thuê.")]
    [Display(Name = "Bên cho thuê")]
    public int LessorResidentId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn bên thuê.")]
    [Display(Name = "Bên thuê")]
    public int LesseeResidentId { get; set; }

    public string RowVersionToken { get; set; } = string.Empty;

    public IReadOnlyList<SelectListItem> ApartmentOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> ResidentOptions { get; set; } = [];
}

public sealed class ContractActionViewModel
{
    [Required]
    public string RowVersionToken { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Lý do không được vượt quá 1000 ký tự.")]
    [Display(Name = "Lý do")]
    public string? Reason { get; set; }
}

public sealed record ContractDetailsViewModel(
    int Id,
    string ContractCode,
    string Apartment,
    DateTime StartDate,
    DateTime? EndDate,
    decimal MonthlyRent,
    decimal DepositAmount,
    ApartmentContractStatus Status,
    string CreatedBy,
    DateTime CreatedAtUtc,
    string? UpdatedBy,
    DateTime? UpdatedAtUtc,
    string RowVersionToken,
    IReadOnlyList<ContractPartyDetailsViewModel> Parties,
    IReadOnlyList<ContractHistoryDetailsViewModel> History);

public sealed record ContractPartyDetailsViewModel(string ResidentName, ContractPartyRole Role);

public sealed record ContractHistoryDetailsViewModel(
    ApartmentContractStatus? PreviousStatus,
    ApartmentContractStatus NewStatus,
    DateTime ChangedAtUtc,
    string ChangedBy,
    string? Reason);
