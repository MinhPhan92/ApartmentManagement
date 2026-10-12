using System.Security.Claims;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApartmentManagement.Controllers;

[Authorize]
public class AnnouncementsController : Controller
{
    private readonly IAnnouncementService _announcementService;
    private readonly IBuildingService _buildingService;

    public AnnouncementsController(
        IAnnouncementService announcementService,
        IBuildingService buildingService)
    {
        _announcementService = announcementService;
        _buildingService = buildingService;
    }

    // GET: /Announcements
    [HttpGet]
    public async Task<IActionResult> Index(int? buildingId)
    {
        ViewBag.Buildings = await _buildingService.GetAllBuildingsAsync();
        ViewBag.SelectedBuildingId = buildingId;

        if (User.IsInRole(AppRoles.Resident))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var residentAnnouncements = await _announcementService.GetAnnouncementsForResidentAsync(userId);
            return View(residentAnnouncements);
        }

        var announcements = await _announcementService.GetAllAnnouncementsAsync(buildingId);
        return View(announcements);
    }

    // GET: /Announcements/Details/5
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var announcement = await _announcementService.GetAnnouncementByIdAsync(id);
        if (announcement == null) return NotFound();

        if (User.IsInRole(AppRoles.Resident))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var canAccess = await _announcementService.CanResidentAccessAnnouncementAsync(userId, announcement);
            if (!canAccess)
            {
                return Forbid();
            }
        }

        return View(announcement);
    }

    // GET: /Announcements/Create
    [HttpGet]
    [Authorize(Policy = AppPolicies.RequireManagement)]
    public async Task<IActionResult> Create()
    {
        ViewBag.Buildings = await _buildingService.GetAllBuildingsAsync();
        return View(new Announcement());
    }

    // POST: /Announcements/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.RequireManagement)]
    public async Task<IActionResult> Create(Announcement model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Buildings = await _buildingService.GetAllBuildingsAsync();
            return View(model);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _announcementService.CreateAnnouncementAsync(userId, model);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            ViewBag.Buildings = await _buildingService.GetAllBuildingsAsync();
            return View(model);
        }

        TempData["SuccessMessage"] = "Đăng thông báo thành công!";
        return RedirectToAction(nameof(Index));
    }

    // POST: /Announcements/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.RequireManagement)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _announcementService.DeleteAnnouncementAsync(id);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Xóa thông báo thành công!";
        }
        return RedirectToAction(nameof(Index));
    }
}
