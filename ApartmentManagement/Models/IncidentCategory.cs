using System.ComponentModel.DataAnnotations;

namespace ApartmentManagement.Models;

public enum IncidentCategory
{
    [Display(Name = "Điện")]
    Electricity,
    [Display(Name = "Nước")]
    Water,
    [Display(Name = "Thang máy")]
    Elevator,
    [Display(Name = "Khu vực chung")]
    CommonArea,
    [Display(Name = "Khác")]
    Other
}
