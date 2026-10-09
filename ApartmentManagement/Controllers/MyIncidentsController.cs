using System.Security.Claims;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Controllers;

[Authorize(Policy = AppPolicies.RequireResident)]
public sealed class MyIncidentsController : Controller
{
    private readonly IIncidentTicketService _ticketService;
    private readonly ApplicationDbContext _context;

    public MyIncidentsController(
        IIncidentTicketService ticketService,
        ApplicationDbContext context)
    {
        _ticketService = ticketService;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = CurrentUserId();
        if (userId == null) return Forbid();

        return View(new IncidentTicketListViewModel
        {
            Tickets = await _ticketService.GetForResidentAsync(userId)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var userId = CurrentUserId();
        if (userId == null) return Forbid();

        var model = new IncidentTicketCreateViewModel();
        await PopulateApartmentsAsync(model, userId);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(IncidentTicketCreateViewModel model)
    {
        var userId = CurrentUserId();
        if (userId == null) return Forbid();

        if (!ModelState.IsValid)
        {
            await PopulateApartmentsAsync(model, userId);
            return View(model);
        }

        var result = await _ticketService.CreateAsync(userId, new IncidentTicketInput(
            model.ApartmentId, model.Category!.Value, model.Title, model.Description));
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể gửi phiếu sự cố.");
            await PopulateApartmentsAsync(model, userId);
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã gửi phiếu sự cố.";
        return RedirectToAction(nameof(Details), new { id = result.Ticket!.IncidentTicketId });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var userId = CurrentUserId();
        if (userId == null) return Forbid();

        var ticket = await _ticketService.GetForResidentByIdAsync(userId, id);
        if (ticket == null) return NotFound();

        return View(ticket);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rate(IncidentTicketReviewViewModel model)
    {
        var userId = CurrentUserId();
        if (userId == null) return Forbid();
        if (!TryDecodeRowVersion(model.RowVersionToken, out var rowVersion))
        {
            ModelState.AddModelError(nameof(model.RowVersionToken), "Dữ liệu phiếu không hợp lệ. Vui lòng tải lại.");
        }
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = ModelState.Values
                .SelectMany(x => x.Errors)
                .Select(x => x.ErrorMessage)
                .FirstOrDefault() ?? "Dữ liệu đánh giá không hợp lệ.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        var result = await _ticketService.RateAsync(model.Id, userId, model.Rating, model.Feedback, rowVersion);
        if (!result.Success) TempData["ErrorMessage"] = result.ErrorMessage;
        else TempData["SuccessMessage"] = "Cảm ơn bạn đã đánh giá.";

        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    private async Task PopulateApartmentsAsync(
        IncidentTicketCreateViewModel model,
        string userId)
    {
        var apartments = await _context.ApartmentResidents.AsNoTracking()
            .Where(x => x.Resident.UserId == userId && x.MoveOutDate == null)
            .Select(x => x.Apartment)
            .Distinct()
            .OrderBy(x => x.ApartmentCode)
            .Select(x => new
            {
                x.ApartmentId,
                x.ApartmentCode,
                Building = x.Building == null ? string.Empty : x.Building.BuildingName
            })
            .ToListAsync();
        model.Apartments = apartments.Select(x => new SelectListItem(
            $"{x.Building} — {x.ApartmentCode}", x.ApartmentId.ToString())).ToList();
    }

    private string? CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

    internal static bool TryDecodeRowVersion(string? token, out byte[] rowVersion)
    {
        try
        {
            rowVersion = Convert.FromBase64String(token ?? string.Empty);
            return rowVersion.Length > 0;
        }
        catch (FormatException)
        {
            rowVersion = [];
            return false;
        }
    }
}
