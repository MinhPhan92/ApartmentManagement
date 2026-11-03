using ApartmentManagement.Common.Security;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using ApartmentManagement.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApartmentManagement.Controllers;

[Authorize(Policy = AppPolicies.RequireManagement)]
public class AmenityManagementController : Controller
{
    private readonly IAmenityService _amenityService;
    private readonly ICurrentUserService _currentUserService;

    public AmenityManagementController(
        IAmenityService amenityService,
        ICurrentUserService currentUserService)
    {
        _amenityService = amenityService;
        _currentUserService = currentUserService;
    }

    // GET: /AmenityManagement
    [HttpGet]
    public async Task<IActionResult> Index(int? amenityId, DateTime? date, string? status)
    {
        var bookings = await _amenityService.GetBookingsAsync(amenityId, date, status);
        var amenities = await _amenityService.GetAllAmenitiesAsync();

        var model = new AmenityManagementViewModel
        {
            SelectedAmenityId = amenityId,
            SelectedDate = date,
            SelectedStatus = status,
            Bookings = bookings,
            Amenities = amenities
        };

        return View(model);
    }

    // POST: /AmenityManagement/Approve
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        if (id <= 0) return NotFound();

        var managerUserId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(managerUserId)) return Forbid();

        var result = await _amenityService.ApproveBookingAsync(managerUserId, id);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Đã phê duyệt yêu cầu đặt chỗ tiện ích thành công.";
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: /AmenityManagement/Reject
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? reason)
    {
        if (id <= 0) return NotFound();

        var managerUserId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(managerUserId)) return Forbid();

        var result = await _amenityService.RejectBookingAsync(managerUserId, id, reason);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Đã từ chối yêu cầu đặt chỗ tiện ích.";
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: /AmenityManagement/Cancel
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        if (id <= 0) return NotFound();

        var managerUserId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(managerUserId)) return Forbid();

        var result = await _amenityService.CancelBookingAsync(managerUserId, id, isManager: true);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Ban quản lý đã hủy lịch đặt chỗ tiện ích thành công.";
        }

        return RedirectToAction(nameof(Index));
    }

    // GET: /AmenityManagement/Amenities
    [HttpGet]
    public async Task<IActionResult> Amenities()
    {
        var amenities = await _amenityService.GetAllAmenitiesAsync();
        return View(amenities);
    }

    // GET: /AmenityManagement/CreateAmenity
    [HttpGet]
    public IActionResult CreateAmenity()
    {
        return View(new AmenityDefinitionViewModel());
    }

    // POST: /AmenityManagement/CreateAmenity
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAmenity(AmenityDefinitionViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var amenity = new Amenity
        {
            Name = model.Name,
            Description = model.Description,
            Location = model.Location,
            Capacity = model.Capacity,
            OpenTime = model.OpenTime,
            CloseTime = model.CloseTime,
            IsActive = model.IsActive,
            PricePerHour = model.PricePerHour
        };

        var result = await _amenityService.CreateAmenityAsync(amenity);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể tạo tiện ích.");
            return View(model);
        }

        TempData["SuccessMessage"] = $"Đã thêm tiện ích mới '{amenity.Name}' thành công.";
        return RedirectToAction(nameof(Amenities));
    }

    // GET: /AmenityManagement/EditAmenity/5
    [HttpGet]
    public async Task<IActionResult> EditAmenity(int id)
    {
        if (id <= 0) return NotFound();

        var amenity = await _amenityService.GetAmenityByIdAsync(id);
        if (amenity == null) return NotFound();

        var model = new AmenityDefinitionViewModel
        {
            AmenityId = amenity.AmenityId,
            Name = amenity.Name,
            Description = amenity.Description,
            Location = amenity.Location,
            Capacity = amenity.Capacity,
            OpenTime = amenity.OpenTime,
            CloseTime = amenity.CloseTime,
            IsActive = amenity.IsActive,
            PricePerHour = amenity.PricePerHour
        };

        return View(model);
    }

    // POST: /AmenityManagement/EditAmenity/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAmenity(int id, AmenityDefinitionViewModel model)
    {
        if (id <= 0 || id != model.AmenityId) return NotFound();

        if (!ModelState.IsValid)
            return View(model);

        var amenity = new Amenity
        {
            AmenityId = model.AmenityId,
            Name = model.Name,
            Description = model.Description,
            Location = model.Location,
            Capacity = model.Capacity,
            OpenTime = model.OpenTime,
            CloseTime = model.CloseTime,
            IsActive = model.IsActive,
            PricePerHour = model.PricePerHour
        };

        var result = await _amenityService.UpdateAmenityAsync(amenity);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể cập nhật tiện ích.");
            return View(model);
        }

        TempData["SuccessMessage"] = $"Đã cập nhật thông tin tiện ích '{amenity.Name}' thành công.";
        return RedirectToAction(nameof(Amenities));
    }

    // POST: /AmenityManagement/DeleteAmenity/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAmenity(int id)
    {
        if (id <= 0) return NotFound();

        var result = await _amenityService.DeleteAmenityAsync(id);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Đã xóa tiện ích thành công.";
        }

        return RedirectToAction(nameof(Amenities));
    }
}

