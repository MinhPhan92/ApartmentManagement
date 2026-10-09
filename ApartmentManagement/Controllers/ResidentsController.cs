using ApartmentManagement.Common.Security;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApartmentManagement.Controllers;

[Authorize(Policy = AppPolicies.RequireManagement)]
public sealed class ResidentsController : Controller
{
    private readonly IResidentService _residentService;
    public ResidentsController(IResidentService residentService) => _residentService = residentService;

    public async Task<IActionResult> Index(string? searchTerm)
    {
        ViewBag.CurrentSearch = searchTerm;
        return View(await _residentService.GetAllAsync(searchTerm));
    }

    public async Task<IActionResult> Details(int id)
    {
        var resident = await _residentService.GetByIdAsync(id);
        return resident == null ? NotFound() : View(resident);
    }

    [HttpGet]
    public IActionResult Create() => View(new ResidentCreateViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ResidentCreateViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await _residentService.CreateAsync(model);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã tạo hồ sơ và cấp tài khoản cư dân thành công.";
        return RedirectToAction(nameof(Details), new { id = result.Resident!.ResidentId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var resident = await _residentService.GetByIdAsync(id);
        if (resident == null) return NotFound();
        return View(new ResidentEditViewModel
        {
            ResidentId = resident.ResidentId,
            FullName = resident.User.FullName,
            Email = resident.User.Email ?? string.Empty,
            CitizenId = resident.CitizenId,
            DateOfBirth = resident.DateOfBirth,
            Gender = resident.Gender,
            Address = resident.Address,
            EmergencyContact = resident.EmergencyContact
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ResidentEditViewModel model)
    {
        if (id != model.ResidentId) return BadRequest();
        if (!ModelState.IsValid) return View(model);
        var result = await _residentService.UpdateAsync(model);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã cập nhật thông tin cư dân.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var resident = await _residentService.GetByIdAsync(id);
        return resident == null ? NotFound() : View(resident);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var result = await _residentService.DeleteAsync(id);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
            return RedirectToAction(nameof(Delete), new { id });
        }

        TempData["SuccessMessage"] = "Đã xóa cư dân và tài khoản liên kết.";
        return RedirectToAction(nameof(Index));
    }
}
