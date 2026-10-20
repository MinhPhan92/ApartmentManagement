using System.ComponentModel.DataAnnotations;
using ApartmentManagement.Common.Validation;

namespace ApartmentManagement.Models;

public sealed class MoveInViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn căn hộ.")]
    [Display(Name = "Căn hộ")]
    public int ApartmentId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn cư dân.")]
    [Display(Name = "Cư dân")]
    public int ResidentId { get; set; }

    [Required, StringLength(ResidentValidation.RelationshipMaxLength)]
    [Display(Name = "Quan hệ cư trú")]
    public string Relationship { get; set; } = string.Empty;

    [Required, DataType(DataType.Date)]
    [Display(Name = "Ngày chuyển vào")]
    public DateTime MoveInDate { get; set; } = DateTime.Today;

    [Display(Name = "Chủ hộ")]
    public bool IsOwner { get; set; }
}

public sealed class MoveOutViewModel
{
    public int ApartmentResidentId { get; set; }

    [Required, DataType(DataType.Date)]
    [Display(Name = "Ngày chuyển đi")]
    public DateTime MoveOutDate { get; set; } = DateTime.Today;
}

public sealed class OccupancyEditViewModel
{
    public int ApartmentResidentId { get; set; }
    public int ApartmentId { get; set; }
    public string ApartmentCode { get; set; } = string.Empty;
    public string? BuildingName { get; set; }
    public int ResidentId { get; set; }
    public string ResidentName { get; set; } = string.Empty;
    public string CitizenId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập quan hệ cư trú.")]
    [StringLength(ResidentValidation.RelationshipMaxLength, ErrorMessage = "Quan hệ không được vượt quá 100 ký tự.")]
    [Display(Name = "Quan hệ cư trú")]
    public string Relationship { get; set; } = string.Empty;

    [Display(Name = "Chủ hộ")]
    public bool IsOwner { get; set; }

    public DateTime MoveInDate { get; set; }
    public DateTime? MoveOutDate { get; set; }
}
