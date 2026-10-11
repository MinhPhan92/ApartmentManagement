using System.ComponentModel.DataAnnotations;

namespace ApartmentManagement.Models;

public class AmenityBooking
{
    public int AmenityBookingId { get; set; }

    [Required]
    public int AmenityId { get; set; }

    [Required]
    public int ResidentId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày sử dụng.")]
    [DataType(DataType.Date)]
    [Display(Name = "Ngày sử dụng")]
    public DateTime BookingDate { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn giờ bắt đầu.")]
    [Display(Name = "Giờ bắt đầu")]
    public TimeSpan StartTime { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn giờ kết thúc.")]
    [Display(Name = "Giờ kết thúc")]
    public TimeSpan EndTime { get; set; }

    [Range(1, 100, ErrorMessage = "Số người từ 1 đến 100.")]
    [Display(Name = "Số người tham gia")]
    public int NumberOfGuests { get; set; } = 1;

    [Required]
    [StringLength(50)]
    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = "Đã xác nhận"; // "Chờ duyệt", "Đã xác nhận", "Đã hủy"

    [StringLength(500, ErrorMessage = "Ghi chú không quá 500 ký tự.")]
    [Display(Name = "Ghi chú")]
    public string? Notes { get; set; }

    [Display(Name = "Thời gian đăng ký")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Amenity Amenity { get; set; } = null!;
    public Resident Resident { get; set; } = null!;
}
