using System.ComponentModel.DataAnnotations;

namespace ApartmentManagement.Models;

public class VisitorPass
{
    public int VisitorPassId { get; set; }

    [Required]
    public int ResidentId { get; set; }

    [Required]
    public int ApartmentId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ tên khách.")]
    [StringLength(100, ErrorMessage = "Họ tên không quá 100 ký tự.")]
    [Display(Name = "Họ tên khách")]
    public string VisitorName { get; set; } = string.Empty;

    [StringLength(20)]
    [Display(Name = "Số điện thoại")]
    public string? PhoneNumber { get; set; }

    [StringLength(20)]
    [Display(Name = "Số CMND / CCCD")]
    public string? IdCardNumber { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày đến.")]
    [DataType(DataType.Date)]
    [Display(Name = "Ngày dự kiến đến")]
    public DateTime ExpectedArrivalDate { get; set; }

    [Display(Name = "Thời gian vào")]
    public DateTime? ActualCheckInUtc { get; set; }

    [Display(Name = "Thời gian ra")]
    public DateTime? ActualCheckOutUtc { get; set; }

    [Required]
    [StringLength(50)]
    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = "Đã đăng ký"; // "Đã đăng ký", "Đã vào", "Đã ra", "Đã hủy"

    [StringLength(500, ErrorMessage = "Mục đích không quá 500 ký tự.")]
    [Display(Name = "Mục đích đến thăm")]
    public string? Purpose { get; set; }

    [Display(Name = "Thời gian đăng ký")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Resident Resident { get; set; } = null!;
    public Apartment Apartment { get; set; } = null!;
}
