using System.Security.Claims;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApartmentManagement.Controllers;

[Authorize]
public class VisitorsController : Controller
{
    private readonly IVisitorService _visitorService;
    private readonly IResidentService _residentService;

    public VisitorsController(
        IVisitorService visitorService,
        IResidentService residentService)
    {
        _visitorService = visitorService;
        _residentService = residentService;
    }

    // GET: /Visitors
    [HttpGet]
    public async Task<IActionResult> Index(DateTime? date, string? status)
    {
        ViewBag.SelectedDate = date?.ToString("yyyy-MM-dd");
        ViewBag.SelectedStatus = status;

        if (User.IsInRole(AppRoles.Resident))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var myPasses = await _visitorService.GetResidentVisitorPassesAsync(userId);
            return View(myPasses);
        }

        var passes = await _visitorService.GetAllVisitorPassesAsync(date, status);
        return View(passes);
    }

    // GET: /Visitors/Register
    [HttpGet]
    [Authorize(Policy = AppPolicies.RequireResident)]
    public async Task<IActionResult> Register()
    {
        ViewBag.MyApartments = await _residentService.GetApartmentsForCurrentResidentAsync();
        var model = new VisitorPass
        {
            ExpectedArrivalDate = DateTime.Today
        };
        return View(model);
    }

    // POST: /Visitors/Register
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.RequireResident)]
    public async Task<IActionResult> Register(VisitorPass model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _visitorService.RegisterVisitorAsync(
            userId,
            model.ApartmentId,
            model.VisitorName,
            model.PhoneNumber,
            model.IdCardNumber,
            model.ExpectedArrivalDate,
            model.Purpose);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            ViewBag.MyApartments = await _residentService.GetApartmentsForCurrentResidentAsync();
            return View(model);
        }

        TempData["SuccessMessage"] = "Đăng ký khách thành công!";
        return RedirectToAction(nameof(Index));
    }

    // POST: /Visitors/CheckIn/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin,BuildingManager,Technician")]
    public async Task<IActionResult> CheckIn(int id)
    {
        var result = await _visitorService.CheckInVisitorAsync(id);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Khách đã Check-in vào tòa nhà thành công.";
        }
        return RedirectToAction(nameof(Index));
    }

    // POST: /Visitors/CheckOut/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin,BuildingManager,Technician")]
    public async Task<IActionResult> CheckOut(int id)
    {
        var result = await _visitorService.CheckOutVisitorAsync(id);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Khách đã Check-out khỏi tòa nhà.";
        }
        return RedirectToAction(nameof(Index));
    }

    // POST: /Visitors/Cancel/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var isManager = User.IsInRole(AppRoles.SuperAdmin) || User.IsInRole(AppRoles.BuildingManager);
        var result = await _visitorService.CancelVisitorPassAsync(userId, id, isManager);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Đã hủy đăng ký khách.";
        }
        return RedirectToAction(nameof(Index));
    }
}
