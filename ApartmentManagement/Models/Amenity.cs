using System.ComponentModel.DataAnnotations;

namespace ApartmentManagement.Models;

public class Amenity
{
    public int AmenityId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên tiện ích.")]
    [StringLength(100, ErrorMessage = "Tên tiện ích không quá 100 ký tự.")]
    [Display(Name = "Tên tiện ích")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Mô tả không quá 500 ký tự.")]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [StringLength(100)]
    [Display(Name = "Vị trí")]
    public string? Location { get; set; }

    [Range(1, 1000, ErrorMessage = "Sức chứa tối đa từ 1 đến 1000 người.")]
    [Display(Name = "Sức chứa tối đa")]
    public int Capacity { get; set; } = 20;

    [Display(Name = "Giờ mở cửa")]
    public TimeSpan OpenTime { get; set; } = new TimeSpan(6, 0, 0);

    [Display(Name = "Giờ đóng cửa")]
    public TimeSpan CloseTime { get; set; } = new TimeSpan(22, 0, 0);

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;

    [Range(0, 10000000, ErrorMessage = "Phí theo giờ không hợp lệ.")]
    [Display(Name = "Phí mỗi giờ (VNĐ)")]
    public decimal PricePerHour { get; set; } = 0;

    public ICollection<AmenityBooking> Bookings { get; set; } = new List<AmenityBooking>();
}
