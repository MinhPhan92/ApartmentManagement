using System.Security.Claims;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApartmentManagement.Controllers;

[Authorize(Policy = AppPolicies.RequireResident)]
public class MyAmenitiesController : Controller
{
    private readonly IAmenityService _amenityService;

    public MyAmenitiesController(IAmenityService amenityService)
    {
        _amenityService = amenityService;
    }

    // GET: /MyAmenities
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var myBookings = await _amenityService.GetResidentBookingsAsync(userId);
        ViewBag.Amenities = await _amenityService.GetAllAmenitiesAsync();
        return View(myBookings);
    }

    // GET: /MyAmenities/Book/5
    [HttpGet]
    public async Task<IActionResult> Book(int id)
    {
        var amenity = await _amenityService.GetAmenityByIdAsync(id);
        if (amenity == null || !amenity.IsActive) return NotFound();

        ViewBag.Amenity = amenity;
        var model = new AmenityBooking
        {
            AmenityId = id,
            BookingDate = DateTime.Today,
            StartTime = amenity.OpenTime,
            EndTime = amenity.OpenTime.Add(TimeSpan.FromHours(1)),
            NumberOfGuests = 1
        };
        return View(model);
    }

    // POST: /MyAmenities/Book
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(AmenityBooking model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
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
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            ViewBag.Amenity = await _amenityService.GetAmenityByIdAsync(model.AmenityId);
            return View(model);
        }

        TempData["SuccessMessage"] = "Đặt tiện ích thành công!";
        return RedirectToAction(nameof(Index));
    }

    // POST: /MyAmenities/Cancel/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _amenityService.CancelBookingAsync(userId, id, isManager: false);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Đã hủy lịch đặt tiện ích.";
        }
        return RedirectToAction(nameof(Index));
    }
}
