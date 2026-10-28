using ApartmentManagement.Common.Security;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using ApartmentManagement.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ApartmentManagement.Controllers;

[Authorize(Policy = AppPolicies.RequireResident)]
[Route("[controller]")]
[Route("MyIncidents")]
public class RepairRequestsController : Controller
{
    private readonly IRepairRequestService _repairService;
    private readonly IResidentService _residentService;
    private readonly ICurrentUserService _currentUserService;

    public RepairRequestsController(
        IRepairRequestService repairService,
        IResidentService residentService,
        ICurrentUserService currentUserService)
    {
        _repairService = repairService;
        _residentService = residentService;
        _currentUserService = currentUserService;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        var resident = await GetCurrentResidentAsync();
        if (resident == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy hồ sơ cư dân liên kết với tài khoản của bạn.";
            return View(Enumerable.Empty<RepairRequest>());
        }

        var requests = await _repairService.GetResidentRequestsAsync(resident.ResidentId);
        return View(requests);
    }

    [HttpGet("Create")]
    public async Task<IActionResult> Create()
    {
        var resident = await GetCurrentResidentAsync();
        if (resident == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy hồ sơ cư dân liên kết với tài khoản của bạn.";
            return RedirectToAction(nameof(Index));
        }

        var apartments = (await _repairService.GetActiveApartmentsForResidentAsync(resident.ResidentId)).ToList();
        if (!apartments.Any())
        {
            TempData["ErrorMessage"] = "Bạn chưa có căn hộ nào đang cư trú hoạt động để báo sửa chữa.";
            return RedirectToAction(nameof(Index));
        }

        var viewModel = new RepairRequestCreateViewModel
        {
            ApartmentId = apartments.First().ApartmentId,
            Priority = RepairPriority.Normal,
            AvailableApartments = apartments.Select(a => new SelectListItem
            {
                Value = a.ApartmentId.ToString(),
                Text = $"{a.ApartmentCode} - {(a.Building != null ? a.Building.BuildingName : string.Empty)}"
            })
        };

        return View(viewModel);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RepairRequestCreateViewModel model)
    {
        var resident = await GetCurrentResidentAsync();
        if (resident == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy hồ sơ cư dân.";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            await PopulateApartmentsAsync(resident.ResidentId, model);
            return View(model);
        }

        var result = await _repairService.CreateRequestAsync(
            resident.ResidentId,
            model.ApartmentId,
            model.Title,
            model.Description,
            model.Priority);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            await PopulateApartmentsAsync(resident.ResidentId, model);
            return View(model);
        }

        TempData["SuccessMessage"] = "Gửi yêu cầu sửa chữa thành công! Ban Quản Lý sẽ sớm tiếp nhận và phân công.";
        return RedirectToAction(nameof(Details), new { id = result.RequestId });
    }

    [HttpGet("Details/{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        if (id <= 0) return NotFound();

        var resident = await GetCurrentResidentAsync();
        if (resident == null) return NotFound();

        var request = await _repairService.GetRequestByIdForResidentAsync(id, resident.ResidentId);
        if (request == null)
        {
            return NotFound();
        }

        return View(request);
    }

    [HttpPost("Cancel/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? reason, string? rowVersionBase64)
    {
        if (id <= 0) return NotFound();

        var resident = await GetCurrentResidentAsync();
        if (resident == null) return NotFound();

        byte[]? rowVersion = !string.IsNullOrEmpty(rowVersionBase64)
            ? Convert.FromBase64String(rowVersionBase64)
            : null;

        var result = await _repairService.CancelRequestAsync(id, resident.ResidentId, reason, rowVersion);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Đã hủy yêu cầu sửa chữa thành công.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("Confirm/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int id, byte rating, string? feedback, string? rowVersionBase64)
    {
        if (id <= 0) return NotFound();

        var resident = await GetCurrentResidentAsync();
        if (resident == null) return NotFound();

        byte[]? rowVersion = !string.IsNullOrEmpty(rowVersionBase64)
            ? Convert.FromBase64String(rowVersionBase64)
            : null;

        var result = await _repairService.ConfirmCompletionAsync(id, resident.ResidentId, rating, feedback, rowVersion);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Cảm ơn bạn đã nghiệm thu và đánh giá chất lượng sửa chữa!";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("RequestRedo/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestRedo(int id, string redoReason, string? rowVersionBase64)
    {
        if (id <= 0) return NotFound();

        var resident = await GetCurrentResidentAsync();
        if (resident == null) return NotFound();

        byte[]? rowVersion = !string.IsNullOrEmpty(rowVersionBase64)
            ? Convert.FromBase64String(rowVersionBase64)
            : null;

        var result = await _repairService.RequestRedoAsync(id, resident.ResidentId, redoReason, rowVersion);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Đã gửi yêu cầu xử lý lại tới Ban Quản Lý.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<Resident?> GetCurrentResidentAsync()
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return null;
        return await _residentService.GetByUserIdAsync(userId);
    }

    private async Task PopulateApartmentsAsync(int residentId, RepairRequestCreateViewModel model)
    {
        var apartments = await _repairService.GetActiveApartmentsForResidentAsync(residentId);
        model.AvailableApartments = apartments.Select(a => new SelectListItem
        {
            Value = a.ApartmentId.ToString(),
            Text = $"{a.ApartmentCode} - {(a.Building != null ? a.Building.BuildingName : string.Empty)}"
        });
    }
}
