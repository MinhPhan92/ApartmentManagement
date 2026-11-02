using System.Security.Claims;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApartmentManagement.Controllers;

[Authorize]
public class AmenitiesController : Controller
{
    private readonly IAmenityService _amenityService;

    public AmenitiesController(IAmenityService amenityService)
    {
        _amenityService = amenityService;
    }

    // GET: /Amenities
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var amenities = await _amenityService.GetAllAmenitiesAsync();
        return View(amenities);
    }

    // GET: /Amenities/Create
    [HttpGet]
    [Authorize(Policy = AppPolicies.RequireManagement)]
    public IActionResult Create()
    {
        return View(new Amenity());
    }

    // POST: /Amenities/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.RequireManagement)]
    public async Task<IActionResult> Create(Amenity model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _amenityService.CreateAmenityAsync(model);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return View(model);
        }

        TempData["SuccessMessage"] = "Thêm tiện ích thành công!";
        return RedirectToAction(nameof(Index));
    }

    // GET: /Amenities/Edit/5
    [HttpGet]
    [Authorize(Policy = AppPolicies.RequireManagement)]
    public async Task<IActionResult> Edit(int id)
    {
        var amenity = await _amenityService.GetAmenityByIdAsync(id);
        if (amenity == null) return NotFound();
        return View(amenity);
    }

    // POST: /Amenities/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.RequireManagement)]
    public async Task<IActionResult> Edit(Amenity model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _amenityService.UpdateAmenityAsync(model);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return View(model);
        }

        TempData["SuccessMessage"] = "Cập nhật tiện ích thành công!";
        return RedirectToAction(nameof(Index));
    }

    // POST: /Amenities/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.RequireManagement)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _amenityService.DeleteAmenityAsync(id);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Xóa tiện ích thành công!";
        }
        return RedirectToAction(nameof(Index));
    }

    // GET: /Amenities/Bookings
    [HttpGet]
    [Authorize(Policy = AppPolicies.RequireManagement)]
    public async Task<IActionResult> Bookings(int? amenityId, DateTime? date, string? status)
    {
        ViewBag.Amenities = await _amenityService.GetAllAmenitiesAsync();
        ViewBag.SelectedAmenityId = amenityId;
        ViewBag.SelectedDate = date?.ToString("yyyy-MM-dd");
        ViewBag.SelectedStatus = status;

        var bookings = await _amenityService.GetBookingsAsync(amenityId, date, status);
        return View(bookings);
    }

    // POST: /Amenities/ApproveBooking/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.RequireManagement)]
    public async Task<IActionResult> ApproveBooking(int id)
    {
        var managerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _amenityService.ApproveBookingAsync(managerUserId, id);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Đã phê duyệt lịch đặt tiện ích.";
        }
        return RedirectToAction(nameof(Bookings));
    }

    // POST: /Amenities/RejectBooking/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.RequireManagement)]
    public async Task<IActionResult> RejectBooking(int id, string? reason)
    {
        var managerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _amenityService.RejectBookingAsync(managerUserId, id, reason);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Đã từ chối lịch đặt tiện ích.";
        }
        return RedirectToAction(nameof(Bookings));
    }

    // POST: /Amenities/CancelBooking/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.RequireManagement)]
    public async Task<IActionResult> CancelBooking(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _amenityService.CancelBookingAsync(userId, id, isManager: true);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Đã hủy lịch đặt chỗ.";
        }
        return RedirectToAction(nameof(Bookings));
    }
}
