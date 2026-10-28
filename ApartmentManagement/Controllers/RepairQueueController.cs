using ApartmentManagement.Common.Security;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using ApartmentManagement.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ApartmentManagement.Controllers;

[Authorize(Policy = AppPolicies.RequireTechnicianOrManagement)]
[Route("[controller]")]
[Route("IncidentQueue")]
public class RepairQueueController : Controller
{
    private readonly IRepairRequestService _repairService;
    private readonly ICurrentUserService _currentUserService;

    public RepairQueueController(
        IRepairRequestService repairService,
        ICurrentUserService currentUserService)
    {
        _repairService = repairService;
        _currentUserService = currentUserService;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(
        string? status,
        string? priority,
        string? searchTerm,
        string? tab)
    {
        var isTechnicianOnly = _currentUserService.IsInRole(AppRoles.Technician) &&
                               !_currentUserService.IsInRole(AppRoles.BuildingManager) &&
                               !_currentUserService.IsInRole(AppRoles.SuperAdmin);

        var activeTab = tab ?? (isTechnicianOnly ? "my-tasks" : "all-requests");
        ViewBag.ActiveTab = activeTab;
        ViewBag.IsTechnicianOnly = isTechnicianOnly;
        ViewBag.CurrentStatus = status;
        ViewBag.CurrentPriority = priority;
        ViewBag.SearchTerm = searchTerm;

        if (activeTab == "my-tasks" && !string.IsNullOrEmpty(_currentUserService.UserId))
        {
            var myAssignments = await _repairService.GetTechnicianAssignmentsAsync(_currentUserService.UserId, status);
            return View("MyTasks", myAssignments);
        }

        var requests = await _repairService.GetQueueAsync(status, priority, searchTerm);
        return View("Index", requests);
    }

    [HttpGet("Details/{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        if (id <= 0) return NotFound();

        var request = await _repairService.GetRequestDetailsAsync(id);
        if (request == null)
        {
            return NotFound();
        }

        var currentUserId = _currentUserService.UserId;
        var myActiveAssignment = request.Assignments
            .FirstOrDefault(a => a.TechnicianUserId == currentUserId &&
                                 (a.Status == RepairAssignmentStatus.Assigned || a.Status == RepairAssignmentStatus.InProgress));

        ViewBag.MyActiveAssignment = myActiveAssignment;
        ViewBag.CanAssign = _currentUserService.IsInRole(AppRoles.SuperAdmin) ||
                            _currentUserService.IsInRole(AppRoles.BuildingManager);
        ViewBag.IsTechnician = _currentUserService.IsInRole(AppRoles.Technician);

        return View(request);
    }

    [HttpGet("Assign/{id:int}")]
    [Authorize(Policy = AppPolicies.RequireManagement)]
    public async Task<IActionResult> Assign(int id)
    {
        if (id <= 0) return NotFound();

        var request = await _repairService.GetRequestDetailsAsync(id);
        if (request == null) return NotFound();

        if (request.Status == RepairRequestStatus.Completed || request.Status == RepairRequestStatus.Cancelled)
        {
            TempData["ErrorMessage"] = "Yêu cầu này đã hoàn thành hoặc đã bị hủy, không thể phân công.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var technicians = (await _repairService.GetAvailableTechniciansAsync()).ToList();
        var viewModel = new RepairRequestAssignViewModel
        {
            RepairRequestId = request.RepairRequestId,
            RequestTitle = request.Title,
            ApartmentCode = request.Apartment.ApartmentCode,
            RowVersion = request.RowVersion,
            AvailableTechnicians = technicians.Select(t => new SelectListItem
            {
                Value = t.Id,
                Text = $"{t.FullName} ({t.Email})"
            })
        };

        return View(viewModel);
    }

    [HttpPost("Assign/{id:int}")]
    [Authorize(Policy = AppPolicies.RequireManagement)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(int id, RepairRequestAssignViewModel model)
    {
        if (id <= 0 || id != model.RepairRequestId) return NotFound();

        if (!ModelState.IsValid)
        {
            var technicians = await _repairService.GetAvailableTechniciansAsync();
            model.AvailableTechnicians = technicians.Select(t => new SelectListItem
            {
                Value = t.Id,
                Text = $"{t.FullName} ({t.Email})"
            });
            return View(model);
        }

        var managerUserId = _currentUserService.UserId ?? string.Empty;
        var result = await _repairService.AssignTechnicianAsync(
            model.RepairRequestId,
            model.TechnicianUserId,
            managerUserId,
            model.Note,
            model.RowVersion);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            var technicians = await _repairService.GetAvailableTechniciansAsync();
            model.AvailableTechnicians = technicians.Select(t => new SelectListItem
            {
                Value = t.Id,
                Text = $"{t.FullName} ({t.Email})"
            });
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã phân công kỹ thuật viên thành công!";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("StartWork/{assignmentId:int}")]
    [Authorize(Policy = AppPolicies.RequireTechnician)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartWork(int assignmentId, string? note, string? rowVersionBase64)
    {
        if (assignmentId <= 0) return NotFound();

        var technicianUserId = _currentUserService.UserId ?? string.Empty;
        byte[]? rowVersion = !string.IsNullOrEmpty(rowVersionBase64)
            ? Convert.FromBase64String(rowVersionBase64)
            : null;

        var result = await _repairService.StartProgressAsync(assignmentId, technicianUserId, note, rowVersion);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Đã tiếp nhận yêu cầu và bắt đầu xử lý!";
        }

        return RedirectToAction(nameof(Index), new { tab = "my-tasks" });
    }

    [HttpGet("CompleteWork/{assignmentId:int}")]
    [Authorize(Policy = AppPolicies.RequireTechnician)]
    public async Task<IActionResult> CompleteWork(int assignmentId)
    {
        if (assignmentId <= 0) return NotFound();

        var technicianUserId = _currentUserService.UserId ?? string.Empty;
        var assignment = await _repairService.GetAssignmentDetailsAsync(assignmentId, technicianUserId);
        if (assignment == null)
        {
            return NotFound();
        }

        var model = new RepairRequestProgressViewModel
        {
            AssignmentId = assignment.AssignmentId,
            RepairRequestId = assignment.RepairRequestId,
            Title = assignment.RepairRequest.Title,
            ApartmentCode = assignment.RepairRequest.Apartment.ApartmentCode,
            CurrentStatus = assignment.Status,
            RowVersion = assignment.RepairRequest.RowVersion
        };

        return View(model);
    }

    [HttpPost("CompleteWork/{assignmentId:int}")]
    [Authorize(Policy = AppPolicies.RequireTechnician)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteWork(int assignmentId, RepairRequestProgressViewModel model)
    {
        if (assignmentId <= 0 || assignmentId != model.AssignmentId) return NotFound();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var technicianUserId = _currentUserService.UserId ?? string.Empty;
        var result = await _repairService.CompleteWorkAsync(
            model.AssignmentId,
            technicianUserId,
            model.Result,
            model.Note,
            model.RowVersion);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã hoàn thành công việc và gửi phiếu chờ cư dân nghiệm thu!";
        return RedirectToAction(nameof(Details), new { id = model.RepairRequestId });
    }
}
