using ApartmentManagement.Common.Security;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using ApartmentManagement.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApartmentManagement.Controllers;

[Authorize]
public class AmenityBookingController : Controller
{
    private readonly IAmenityService _amenityService;
    private readonly ICurrentUserService _currentUserService;

    public AmenityBookingController(
        IAmenityService amenityService,
        ICurrentUserService currentUserService)
    {
        _amenityService = amenityService;
        _currentUserService = currentUserService;
    }

    // GET: /AmenityBooking
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var amenities = await _amenityService.GetAllAmenitiesAsync();
        return View(amenities);
    }

    // GET: /AmenityBooking/Create?amenityId=1
    [HttpGet]
    public async Task<IActionResult> Create(int amenityId)
    {
        if (amenityId <= 0) return RedirectToAction(nameof(Index));

        var amenity = await _amenityService.GetAmenityByIdAsync(amenityId);
        if (amenity == null) return NotFound();

        if (!amenity.IsActive)
        {
            TempData["ErrorMessage"] = $"Tiện ích '{amenity.Name}' hiện đang tạm ngừng hoạt động.";
            return RedirectToAction(nameof(Index));
        }

        var defaultStart = amenity.OpenTime;
        var defaultEnd = defaultStart.Add(TimeSpan.FromHours(2));
        if (defaultEnd > amenity.CloseTime) defaultEnd = amenity.CloseTime;

        var model = new AmenityBookingCreateViewModel
        {
            AmenityId = amenity.AmenityId,
            AmenityName = amenity.Name,
            Description = amenity.Description,
            Location = amenity.Location,
            Capacity = amenity.Capacity,
            PricePerHour = amenity.PricePerHour,
            OpenTime = amenity.OpenTime,
            CloseTime = amenity.CloseTime,
            BookingDate = DateTime.Today.AddDays(1),
            StartTime = defaultStart,
            EndTime = defaultEnd,
            NumberOfGuests = 1
        };

        return View(model);
    }

    // POST: /AmenityBooking/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AmenityBookingCreateViewModel model)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return Forbid();

        var amenity = await _amenityService.GetAmenityByIdAsync(model.AmenityId);
        if (amenity == null) return NotFound();

        // Populate presentation details in case of validation failure
        model.AmenityName = amenity.Name;
        model.Description = amenity.Description;
        model.Location = amenity.Location;
        model.Capacity = amenity.Capacity;
        model.PricePerHour = amenity.PricePerHour;
        model.OpenTime = amenity.OpenTime;
        model.CloseTime = amenity.CloseTime;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _amenityService.BookAmenityAsync(
            userId,
            model.AmenityId,
            model.BookingDate,
            model.StartTime,
            model.EndTime,
            model.NumberOfGuests,
            model.Notes);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể hoàn tất đăng ký tiện ích.");
            return View(model);
        }

        TempData["SuccessMessage"] = $"Đăng ký sử dụng tiện ích '{amenity.Name}' thành công! Yêu cầu đang chờ Ban quản lý xác nhận.";
        return RedirectToAction(nameof(MyBookings));
    }

    // GET: /AmenityBooking/MyBookings
    [HttpGet]
    public async Task<IActionResult> MyBookings()
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return Forbid();

        var bookings = await _amenityService.GetResidentBookingsAsync(userId);
        return View(bookings);
    }

    // POST: /AmenityBooking/Cancel
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        if (id <= 0) return NotFound();

        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return Forbid();

        var result = await _amenityService.CancelBookingAsync(userId, id, isManager: false);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Đã hủy lịch đặt chỗ tiện ích thành công.";
        }

        return RedirectToAction(nameof(MyBookings));
    }

    // GET: /AmenityBooking/GetBookedSlots?amenityId=1&date=2026-11-04
    [HttpGet]
    public async Task<IActionResult> GetBookedSlots(int amenityId, DateTime date)
    {
        if (amenityId <= 0) return BadRequest();

        var bookings = await _amenityService.GetBookingsAsync(amenityId, date);
        var activeBookings = bookings
            .Where(b => b.Status == "Chờ duyệt" || b.Status == "Đã xác nhận")
            .Select(b => new
            {
                startTime = b.StartTime.ToString(@"hh\:mm"),
                endTime = b.EndTime.ToString(@"hh\:mm"),
                guests = b.NumberOfGuests,
                status = b.Status
            });

        return Json(activeBookings);
    }
}

