using System.ComponentModel.DataAnnotations;
using ApartmentManagement.Models;

namespace ApartmentManagement.ViewModels;

public class AmenityBookingCreateViewModel : IValidatableObject
{
    [Required]
    public int AmenityId { get; set; }

    public string AmenityName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public int Capacity { get; set; } = 20;
    public decimal PricePerHour { get; set; }
    public TimeSpan OpenTime { get; set; }
    public TimeSpan CloseTime { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày sử dụng.")]
    [DataType(DataType.Date)]
    [Display(Name = "Ngày sử dụng")]
    public DateTime BookingDate { get; set; } = DateTime.Today.AddDays(1);

    [Required(ErrorMessage = "Vui lòng chọn giờ bắt đầu.")]
    [Display(Name = "Giờ bắt đầu")]
    public TimeSpan StartTime { get; set; } = new TimeSpan(8, 0, 0);

    [Required(ErrorMessage = "Vui lòng chọn giờ kết thúc.")]
    [Display(Name = "Giờ kết thúc")]
    public TimeSpan EndTime { get; set; } = new TimeSpan(10, 0, 0);

    [Range(1, 1000, ErrorMessage = "Số người tham gia phải từ 1 người trở lên.")]
    [Display(Name = "Số người tham gia")]
    public int NumberOfGuests { get; set; } = 1;

    [StringLength(500, ErrorMessage = "Ghi chú không quá 500 ký tự.")]
    [Display(Name = "Ghi chú / Nhu cầu đặc biệt")]
    public string? Notes { get; set; }

    public decimal EstimatedTotalCost
    {
        get
        {
            if (EndTime <= StartTime || PricePerHour <= 0) return 0;
            var hours = (decimal)(EndTime - StartTime).TotalHours;
            return Math.Round(hours * PricePerHour, 0);
        }
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (BookingDate.Date < DateTime.Today)
        {
            yield return new ValidationResult("Không thể đặt lịch cho ngày trong quá khứ.", [nameof(BookingDate)]);
        }

        if (StartTime >= EndTime)
        {
            yield return new ValidationResult("Giờ kết thúc phải sau giờ bắt đầu.", [nameof(EndTime)]);
        }

        if (Capacity > 0 && NumberOfGuests > Capacity)
        {
            yield return new ValidationResult($"Số lượng người ({NumberOfGuests}) vượt quá sức chứa tối đa ({Capacity}) của tiện ích.", [nameof(NumberOfGuests)]);
        }
    }
}

public class AmenityManagementViewModel
{
    public int? SelectedAmenityId { get; set; }
    public DateTime? SelectedDate { get; set; }
    public string? SelectedStatus { get; set; }

    public List<AmenityBooking> Bookings { get; set; } = [];
    public List<Amenity> Amenities { get; set; } = [];

    public int TotalPending => Bookings.Count(b => b.Status == "Chờ duyệt");
    public int TotalConfirmed => Bookings.Count(b => b.Status == "Đã xác nhận");
    public int TotalToday => Bookings.Count(b => b.BookingDate.Date == DateTime.Today);
}

public class AmenityDefinitionViewModel
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
    [Display(Name = "Sức chứa tối đa (người)")]
    public int Capacity { get; set; } = 20;

    [Required(ErrorMessage = "Vui lòng chọn giờ mở cửa.")]
    [Display(Name = "Giờ mở cửa")]
    public TimeSpan OpenTime { get; set; } = new TimeSpan(6, 0, 0);

    [Required(ErrorMessage = "Vui lòng chọn giờ đóng cửa.")]
    [Display(Name = "Giờ đóng cửa")]
    public TimeSpan CloseTime { get; set; } = new TimeSpan(22, 0, 0);

    [Display(Name = "Trạng thái hoạt động")]
    public bool IsActive { get; set; } = true;

    [Range(0, 100000000, ErrorMessage = "Đơn giá theo giờ không hợp lệ.")]
    [Display(Name = "Đơn giá mỗi giờ (VNĐ)")]
    public decimal PricePerHour { get; set; } = 0;
}

