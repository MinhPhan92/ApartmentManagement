using System.Security.Claims;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using ApartmentManagement.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ApartmentManagement.Controllers;

[Authorize(Policy = AppPolicies.RequireTechnicianOrManagement)]
public class TechnicalIncidentsController : Controller
{
    private readonly ITechnicalIncidentService _incidentService;
    private readonly IBuildingService _buildingService;
    private readonly ICurrentUserService _currentUserService;

    public TechnicalIncidentsController(
        ITechnicalIncidentService incidentService,
        IBuildingService buildingService,
        ICurrentUserService currentUserService)
    {
        _incidentService = incidentService;
        _buildingService = buildingService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? status,
        string? priority,
        int? buildingId,
        string? searchTerm,
        string? tab)
    {
        var isTechnicianOnly = _currentUserService.IsInRole(AppRoles.Technician) &&
                               !_currentUserService.IsInRole(AppRoles.BuildingManager) &&
                               !_currentUserService.IsInRole(AppRoles.SuperAdmin);

        var activeTab = tab ?? (isTechnicianOnly ? "my-assigned" : "all");
        ViewBag.ActiveTab = activeTab;
        ViewBag.IsTechnicianOnly = isTechnicianOnly;
        ViewBag.CurrentStatus = status;
        ViewBag.CurrentPriority = priority;
        ViewBag.CurrentBuildingId = buildingId;
        ViewBag.SearchTerm = searchTerm;

        string? filterUserId = null;
        if (activeTab == "my-assigned" && !string.IsNullOrEmpty(_currentUserService.UserId))
        {
            filterUserId = _currentUserService.UserId;
        }

        var incidents = await _incidentService.GetIncidentsAsync(status, priority, buildingId, searchTerm, filterUserId);
        var buildings = await _buildingService.GetAllBuildingsAsync();

        ViewBag.BuildingOptions = new SelectList(buildings, "BuildingId", "BuildingName", buildingId);
        ViewBag.StatusOptions = new SelectList(TechnicalIncidentStatus.AllStatuses);
        ViewBag.PriorityOptions = new SelectList(TechnicalIncidentPriority.AllPriorities);

        return View(incidents);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        if (id <= 0) return NotFound();

        var incident = await _incidentService.GetIncidentDetailsAsync(id);
        if (incident == null) return NotFound();

        ViewBag.CurrentUserId = _currentUserService.UserId;
        ViewBag.IsManagement = _currentUserService.IsInRole(AppRoles.BuildingManager) || _currentUserService.IsInRole(AppRoles.SuperAdmin);
        ViewBag.IsAssignedTechnician = !string.IsNullOrEmpty(_currentUserService.UserId) && incident.AssignedTechnicianUserId == _currentUserService.UserId;

        return View(incident);
    }

    // UC47: Kỹ thuật viên tạo báo cáo sự cố kỹ thuật
    [HttpGet]
    [Authorize(Policy = AppPolicies.RequireTechnicianOrManagement)]
    public async Task<IActionResult> Create()
    {
        var model = new TechnicalIncidentCreateViewModel();
        await PopulateCreateDropdownsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.RequireTechnicianOrManagement)]
    public async Task<IActionResult> Create(TechnicalIncidentCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateCreateDropdownsAsync(model);
            return View(model);
        }

        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Forbid();

        var result = await _incidentService.CreateIncidentAsync(
            userId,
            model.BuildingId,
            model.Location,
            model.Category,
            model.Title,
            model.Description,
            model.Priority);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể tạo báo cáo sự cố.");
            await PopulateCreateDropdownsAsync(model);
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã gửi báo cáo sự cố kỹ thuật thành công tới Ban quản lý.";
        return RedirectToAction(nameof(Details), new { id = result.IncidentId });
    }

    // UC48: Ban quản lý tiếp nhận báo cáo sự cố
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.RequireManagement)]
    public async Task<IActionResult> Acknowledge(int id, string? note, string? rowVersionBase64)
    {
        if (id <= 0) return NotFound();

        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return Forbid();

        byte[]? rowVersion = DecodeRowVersion(rowVersionBase64);

        var result = await _incidentService.AcknowledgeIncidentAsync(id, userId, note, rowVersion);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Ban quản lý đã tiếp nhận báo cáo sự cố kỹ thuật thành công.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // UC49: Ban quản lý phân công kỹ thuật viên xử lý
    [HttpGet]
    [Authorize(Policy = AppPolicies.RequireManagement)]
    public async Task<IActionResult> Assign(int id)
    {
        if (id <= 0) return NotFound();

        var incident = await _incidentService.GetIncidentDetailsAsync(id);
        if (incident == null) return NotFound();

        if (incident.Status == TechnicalIncidentStatus.Closed || incident.Status == TechnicalIncidentStatus.Cancelled)
        {
            TempData["ErrorMessage"] = "Không thể phân công cho sự cố đã đóng hoặc đã hủy.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var technicians = await _incidentService.GetAvailableTechniciansAsync();
        var model = new TechnicalIncidentAssignViewModel
        {
            TechnicalIncidentId = incident.TechnicalIncidentId,
            IncidentCode = incident.IncidentCode,
            Title = incident.Title,
            BuildingName = incident.Building?.BuildingName ?? "",
            Location = incident.Location,
            Priority = incident.Priority,
            TechnicianUserId = incident.AssignedTechnicianUserId ?? "",
            ScheduledDate = incident.ScheduledDate,
            RowVersionBase64 = incident.RowVersion.Length > 0 ? Convert.ToBase64String(incident.RowVersion) : null,
            TechnicianOptions = technicians.Select(t => new SelectListItem
            {
                Value = t.Id,
                Text = $"{t.FullName} ({t.Email})"
            })
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.RequireManagement)]
    public async Task<IActionResult> Assign(int id, TechnicalIncidentAssignViewModel model)
    {
        if (id <= 0 || id != model.TechnicalIncidentId) return NotFound();

        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return Forbid();

        if (!ModelState.IsValid)
        {
            var technicians = await _incidentService.GetAvailableTechniciansAsync();
            model.TechnicianOptions = technicians.Select(t => new SelectListItem
            {
                Value = t.Id,
                Text = $"{t.FullName} ({t.Email})"
            });
            return View(model);
        }

        byte[]? rowVersion = DecodeRowVersion(model.RowVersionBase64);

        var result = await _incidentService.AssignTechnicianAsync(
            id,
            model.TechnicianUserId,
            userId,
            model.ScheduledDate,
            model.AssignmentNote,
            rowVersion);

        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
            return RedirectToAction(nameof(Assign), new { id });
        }

        TempData["SuccessMessage"] = "Đã phân công kỹ thuật viên xử lý sự cố thành công.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // Kỹ thuật viên bắt đầu xử lý sự cố
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.RequireTechnician)]
    public async Task<IActionResult> StartWork(int id, string? note, string? rowVersionBase64)
    {
        if (id <= 0) return NotFound();

        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return Forbid();

        byte[]? rowVersion = DecodeRowVersion(rowVersionBase64);

        var result = await _incidentService.StartWorkAsync(id, userId, note, rowVersion);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Đã chuyển trạng thái sự cố sang 'Đang xử lý'.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // Kỹ thuật viên hoàn tất xử lý và báo cáo kết quả
    [HttpGet]
    [Authorize(Policy = AppPolicies.RequireTechnician)]
    public async Task<IActionResult> Resolve(int id)
    {
        if (id <= 0) return NotFound();

        var incident = await _incidentService.GetIncidentDetailsAsync(id);
        if (incident == null) return NotFound();

        var userId = _currentUserService.UserId;
        if (incident.AssignedTechnicianUserId != userId)
        {
            TempData["ErrorMessage"] = "Bạn không phải là kỹ thuật viên được phân công xử lý sự cố này.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = new TechnicalIncidentResolveViewModel
        {
            TechnicalIncidentId = incident.TechnicalIncidentId,
            IncidentCode = incident.IncidentCode,
            Title = incident.Title,
            BuildingName = incident.Building?.BuildingName ?? "",
            Location = incident.Location,
            ResolutionNotes = incident.ResolutionNotes ?? "",
            SpareParts = incident.SpareParts,
            MaterialCost = incident.MaterialCost,
            RowVersionBase64 = incident.RowVersion.Length > 0 ? Convert.ToBase64String(incident.RowVersion) : null
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.RequireTechnician)]
    public async Task<IActionResult> Resolve(int id, TechnicalIncidentResolveViewModel model)
    {
        if (id <= 0 || id != model.TechnicalIncidentId) return NotFound();

        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return Forbid();

        if (!ModelState.IsValid)
            return View(model);

        byte[]? rowVersion = DecodeRowVersion(model.RowVersionBase64);

        var result = await _incidentService.ResolveIncidentAsync(
            id,
            userId,
            model.ResolutionNotes,
            model.SpareParts,
            model.MaterialCost,
            rowVersion);

        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã báo cáo hoàn tất xử lý sự cố kỹ thuật thành công.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // UC50: Ban quản lý theo dõi và đóng nghiệm thu sự cố
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.RequireManagement)]
    public async Task<IActionResult> Close(int id, string? note, string? rowVersionBase64)
    {
        if (id <= 0) return NotFound();

        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return Forbid();

        byte[]? rowVersion = DecodeRowVersion(rowVersionBase64);

        var result = await _incidentService.CloseIncidentAsync(id, userId, note, rowVersion);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Đã đóng và nghiệm thu sự cố kỹ thuật thành công.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task PopulateCreateDropdownsAsync(TechnicalIncidentCreateViewModel model)
    {
        var buildings = await _buildingService.GetAllBuildingsAsync();
        model.BuildingOptions = new SelectList(buildings, "BuildingId", "BuildingName", model.BuildingId);
        model.CategoryOptions = new SelectList(TechnicalIncidentCategory.AllCategories, model.Category);
        model.PriorityOptions = new SelectList(TechnicalIncidentPriority.AllPriorities, model.Priority);
    }

    private static byte[]? DecodeRowVersion(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64)) return null;
        try { return Convert.FromBase64String(base64); }
        catch { return null; }
    }
}

