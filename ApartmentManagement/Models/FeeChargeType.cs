using System.ComponentModel.DataAnnotations;

namespace ApartmentManagement.Models;

public enum FeeChargeType
{
    [Display(Name = "Phí quản lý")]
    Management,
    [Display(Name = "Điện")]
    Electricity,
    [Display(Name = "Nước")]
    Water
}
