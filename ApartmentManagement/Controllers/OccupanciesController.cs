using ApartmentManagement.Common.Security;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApartmentManagement.Controllers;

[Authorize(Policy = AppPolicies.RequireManagement)]
public sealed class OccupanciesController : Controller
{
    private readonly IOccupancyService _service;
    private readonly IApartmentService _apartmentService;
    private readonly IResidentService _residentService;

    public OccupanciesController(
        IOccupancyService service,
        IApartmentService apartmentService,
        IResidentService residentService)
    {
        _service = service;
        _apartmentService = apartmentService;
        _residentService = residentService;
    }

    public async Task<IActionResult> Index(string? status, int? apartmentId, int? residentId)
    {
        bool? activeOnly = status switch { "active" => true, "history" => false, _ => null };
        ViewBag.Status = status;
        ViewBag.Apartments = await _apartmentService.GetAllApartmentsAsync();
        ViewBag.Residents = await _residentService.GetAllAsync();
        return View(await _service.GetAllAsync(activeOnly, apartmentId, residentId));
    }

    public async Task<IActionResult> Details(int id)
    {
        var occupancy = await _service.GetByIdAsync(id);
        return occupancy == null ? NotFound() : View(occupancy);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? apartmentId, int? residentId)
    {
        await PopulateSelectionsAsync();
        var model = new MoveInViewModel();
        if (apartmentId.HasValue && apartmentId.Value > 0) model.ApartmentId = apartmentId.Value;
        if (residentId.HasValue && residentId.Value > 0) model.ResidentId = residentId.Value;
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var occupancy = await _service.GetByIdAsync(id);
        if (occupancy == null) return NotFound();
        return View(new OccupancyEditViewModel
        {
            ApartmentResidentId = occupancy.ApartmentResidentId,
            ApartmentId = occupancy.ApartmentId,
            ApartmentCode = occupancy.Apartment.ApartmentCode,
            BuildingName = occupancy.Apartment.Building?.BuildingName,
            ResidentId = occupancy.ResidentId,
            ResidentName = occupancy.Resident.User.FullName,
            CitizenId = occupancy.Resident.CitizenId,
            Relationship = occupancy.Relationship,
            IsOwner = occupancy.IsOwner,
            MoveInDate = occupancy.MoveInDate,
            MoveOutDate = occupancy.MoveOutDate
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, OccupancyEditViewModel model)
    {
        if (id != model.ApartmentResidentId) return BadRequest();
        if (!ModelState.IsValid)
        {
            await RepopulateEditDisplayAsync(model, id);
            return View(model);
        }
        var result = await _service.UpdateOccupancyAsync(model);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            await RepopulateEditDisplayAsync(model, id);
            return View(model);
        }
        TempData["SuccessMessage"] = "Đã cập nhật quan hệ và thông tin cư trú.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task RepopulateEditDisplayAsync(OccupancyEditViewModel model, int id)
    {
        var occupancy = await _service.GetByIdAsync(id);
        if (occupancy != null)
        {
            model.ApartmentCode = occupancy.Apartment.ApartmentCode;
            model.BuildingName = occupancy.Apartment.Building?.BuildingName;
            model.ResidentName = occupancy.Resident.User.FullName;
            model.CitizenId = occupancy.Resident.CitizenId;
            model.MoveInDate = occupancy.MoveInDate;
            model.MoveOutDate = occupancy.MoveOutDate;
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MoveInViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateSelectionsAsync();
            return View(model);
        }
        var result = await _service.MoveInAsync(model);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            await PopulateSelectionsAsync();
            return View(model);
        }
        TempData["SuccessMessage"] = "Đã hoàn tất thủ tục chuyển vào.";
        return RedirectToAction(nameof(Details), new { id = result.Occupancy!.ApartmentResidentId });
    }

    [HttpGet]
    public async Task<IActionResult> MoveOut(int id)
    {
        var occupancy = await _service.GetByIdAsync(id);
        if (occupancy == null) return NotFound();
        if (occupancy.MoveOutDate != null) return BadRequest();
        ViewBag.Occupancy = occupancy;
        return View(new MoveOutViewModel { ApartmentResidentId = id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveOut(MoveOutViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Occupancy = await _service.GetByIdAsync(model.ApartmentResidentId);
            return View(model);
        }
        var result = await _service.MoveOutAsync(model.ApartmentResidentId, model.MoveOutDate);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            ViewBag.Occupancy = await _service.GetByIdAsync(model.ApartmentResidentId);
            return View(model);
        }
        TempData["SuccessMessage"] = "Đã hoàn tất thủ tục chuyển đi và lưu lịch sử cư trú.";
        return RedirectToAction(nameof(Details), new { id = model.ApartmentResidentId });
    }

    private async Task PopulateSelectionsAsync()
    {
        ViewBag.Apartments = await _apartmentService.GetAllApartmentsAsync();
        ViewBag.Residents = await _residentService.GetAllAsync();
    }
}
