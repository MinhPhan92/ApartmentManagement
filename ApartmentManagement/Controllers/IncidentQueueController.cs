using System.Security.Claims;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApartmentManagement.Controllers;

[Authorize(Policy = AppPolicies.RequireTicketStaff)]
public sealed class IncidentQueueController : Controller
{
    private readonly IIncidentTicketService _ticketService;

    public IncidentQueueController(IIncidentTicketService ticketService) =>
        _ticketService = ticketService;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = CurrentUserId();
        if (userId == null) return Forbid();

        return View(new IncidentTicketListViewModel
        {
            Tickets = await _ticketService.GetStaffQueueAsync(userId)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var userId = CurrentUserId();
        if (userId == null) return Forbid();

        var ticket = await _ticketService.GetForStaffByIdAsync(userId, id);
        if (ticket == null) return NotFound();
        return View(new StaffIncidentTicketViewModel
        {
            Ticket = ticket,
            RowVersionToken = Convert.ToBase64String(ticket.RowVersion)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Claim(int id, string rowVersionToken)
    {
        var userId = CurrentUserId();
        if (userId == null) return Forbid();
        if (!MyIncidentsController.TryDecodeRowVersion(rowVersionToken, out var rowVersion))
            return BadRequest();

        var result = await _ticketService.ClaimAsync(id, userId, rowVersion);
        if (!result.Success) TempData["ErrorMessage"] = result.ErrorMessage;
        else TempData["SuccessMessage"] = "Bạn đã tiếp nhận phiếu sự cố.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id, string rowVersionToken)
    {
        var userId = CurrentUserId();
        if (userId == null) return Forbid();
        if (!MyIncidentsController.TryDecodeRowVersion(rowVersionToken, out var rowVersion))
            return BadRequest();

        var result = await _ticketService.CompleteAsync(id, userId, rowVersion);
        if (!result.Success) TempData["ErrorMessage"] = result.ErrorMessage;
        else TempData["SuccessMessage"] = "Đã hoàn tất phiếu sự cố.";
        if (result.Success) return RedirectToAction(nameof(Index));
        return RedirectToAction(nameof(Details), new { id });
    }

    private string? CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
}
