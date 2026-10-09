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

[Authorize(Policy = AppPolicies.RequireSuperAdmin)]
public sealed class ContractsController : Controller
{
    private const int PageSize = 10;
    private readonly IContractService _contractService;
    private readonly ApplicationDbContext _context;

    public ContractsController(IContractService contractService, ApplicationDbContext context)
    {
        _contractService = contractService;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? searchTerm, ApartmentContractStatus? status, int page = 1)
    {
        var result = await _contractService.GetPageAsync(searchTerm, status, page, PageSize);
        return View(new ContractListViewModel
        {
            PageResult = result,
            SearchTerm = searchTerm,
            Status = status,
            StatusOptions = StatusOptions(status)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var contract = await _contractService.GetByIdAsync(id);
        if (contract == null) return NotFound();

        return View(new ContractDetailsViewModel(
            contract.ApartmentContractId,
            contract.ContractCode,
            $"{contract.Apartment.ApartmentCode} — {contract.Apartment.Building?.BuildingName ?? "Không rõ tòa nhà"}",
            contract.StartDate,
            contract.EndDate,
            contract.MonthlyRent,
            contract.DepositAmount,
            contract.Status,
            contract.CreatedByUser?.FullName ?? "Không rõ",
            contract.CreatedAtUtc,
            contract.UpdatedByUser?.FullName,
            contract.UpdatedAtUtc,
            Convert.ToBase64String(contract.RowVersion),
            contract.Parties
                .Select(x => new ContractPartyDetailsViewModel(x.Resident.User.FullName, x.Role))
                .ToList(),
            contract.StatusHistory
                .OrderByDescending(x => x.ChangedAtUtc)
                .Select(x => new ContractHistoryDetailsViewModel(
                    x.PreviousStatus,
                    x.NewStatus,
                    x.ChangedAtUtc,
                    x.ChangedByUser.FullName,
                    x.Reason))
                .ToList()));
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new ContractEditViewModel();
        await PopulateOptionsAsync(model);
        return View("Edit", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ContractEditViewModel model)
    {
        ValidateParties(model);
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model);
            return View("Edit", model);
        }

        var result = await _contractService.CreateDraftAsync(ToDraftInput(model), CurrentUserId());
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể tạo hợp đồng.");
            await PopulateOptionsAsync(model);
            return View("Edit", model);
        }

        TempData["SuccessMessage"] = "Đã tạo hợp đồng ở trạng thái nháp.";
        return RedirectToAction(nameof(Details), new { id = result.Contract!.ApartmentContractId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var contract = await _contractService.GetByIdAsync(id);
        if (contract == null) return NotFound();
        if (contract.Status != ApartmentContractStatus.Draft)
        {
            TempData["ErrorMessage"] = "Chỉ có thể chỉnh sửa hợp đồng ở trạng thái nháp.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = new ContractEditViewModel
        {
            ContractCode = contract.ContractCode,
            ApartmentId = contract.ApartmentId,
            StartDate = contract.StartDate,
            EndDate = contract.EndDate,
            MonthlyRent = contract.MonthlyRent,
            DepositAmount = contract.DepositAmount,
            LessorResidentId = contract.Parties.FirstOrDefault(x => x.Role == ContractPartyRole.Lessor)?.ResidentId ?? 0,
            LesseeResidentId = contract.Parties.FirstOrDefault(x => x.Role == ContractPartyRole.Lessee)?.ResidentId ?? 0,
            RowVersionToken = Convert.ToBase64String(contract.RowVersion)
        };
        await PopulateOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ContractEditViewModel model)
    {
        if (id <= 0) return BadRequest();
        ValidateParties(model);
        if (!TryDecodeRowVersion(model.RowVersionToken, out var rowVersion))
            ModelState.AddModelError(nameof(model.RowVersionToken), "Phiên bản hợp đồng không hợp lệ. Vui lòng tải lại trang.");
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model);
            return View(model);
        }

        var result = await _contractService.UpdateDraftAsync(
            id, ToDraftInput(model), CurrentUserId(), rowVersion);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể cập nhật hợp đồng.");
            await PopulateOptionsAsync(model);
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã cập nhật hợp đồng nháp.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Activate(int id, ContractActionViewModel model) =>
        ChangeStatusAsync(id, model, (rowVersion) =>
            _contractService.ActivateAsync(id, CurrentUserId(), rowVersion));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Cancel(int id, ContractActionViewModel model)
    {
        ValidateReason(model);
        if (!ModelState.IsValid) return InvalidActionAsync(id, model);
        return ChangeStatusAsync(id, model, rowVersion =>
            _contractService.CancelAsync(id, CurrentUserId(), rowVersion, model.Reason!));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Complete(int id, ContractActionViewModel model) =>
        ChangeStatusAsync(id, model, rowVersion =>
            _contractService.CompleteAsync(id, CurrentUserId(), rowVersion, model.Reason));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Terminate(int id, ContractActionViewModel model)
    {
        ValidateReason(model);
        if (!ModelState.IsValid) return InvalidActionAsync(id, model);
        return ChangeStatusAsync(id, model, rowVersion =>
            _contractService.TerminateAsync(id, CurrentUserId(), rowVersion, model.Reason!));
    }

    private async Task<IActionResult> ChangeStatusAsync(
        int id,
        ContractActionViewModel model,
        Func<byte[], Task<ContractServiceResult>> operation)
    {
        if (!TryDecodeRowVersion(model.RowVersionToken, out var rowVersion))
        {
            TempData["ErrorMessage"] = "Phiên bản hợp đồng không hợp lệ. Vui lòng tải lại dữ liệu.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var result = await operation(rowVersion);
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] =
            result.Success ? "Đã cập nhật trạng thái hợp đồng." : result.ErrorMessage;
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<IActionResult> InvalidActionAsync(int id, ContractActionViewModel model)
    {
        var contract = await _contractService.GetByIdAsync(id);
        if (contract == null) return NotFound();
        var details = await Details(id);
        if (details is not ViewResult viewResult) return details;
        viewResult.ViewData["ActionErrors"] = ModelState.Values
            .SelectMany(x => x.Errors)
            .Select(x => x.ErrorMessage)
            .ToArray();
        viewResult.ViewData["ActionModel"] = model;
        return details;
    }

    private async Task PopulateOptionsAsync(ContractEditViewModel model)
    {
        model.ApartmentOptions = await _context.Apartments.AsNoTracking()
            .OrderBy(x => x.ApartmentCode)
            .Select(x => new SelectListItem(
                x.Building!.BuildingName + " — " + x.ApartmentCode,
                x.ApartmentId.ToString()))
            .ToListAsync();
        model.ResidentOptions = await _context.Residents.AsNoTracking()
            .Where(x => x.User.IsActive)
            .OrderBy(x => x.User.FullName)
            .Select(x => new SelectListItem(
                x.User.FullName + " — " + x.CitizenId,
                x.ResidentId.ToString()))
            .ToListAsync();
    }

    private static IReadOnlyList<SelectListItem> StatusOptions(ApartmentContractStatus? selected) =>
        new[] { new SelectListItem("Tất cả trạng thái", "") }
            .Concat(Enum.GetValues<ApartmentContractStatus>()
                .Select(x => new SelectListItem(StatusLabel(x), x.ToString(), selected == x)))
            .ToList();

    private static string StatusLabel(ApartmentContractStatus status) => status switch
    {
        ApartmentContractStatus.Draft => "Nháp",
        ApartmentContractStatus.Active => "Đang hiệu lực",
        ApartmentContractStatus.Completed => "Đã hoàn tất",
        ApartmentContractStatus.Terminated => "Đã chấm dứt",
        ApartmentContractStatus.Cancelled => "Đã hủy",
        _ => "Không xác định"
    };

    private static ContractDraftInput ToDraftInput(ContractEditViewModel model) => new()
    {
        ContractCode = model.ContractCode,
        ApartmentId = model.ApartmentId,
        StartDate = model.StartDate,
        EndDate = model.EndDate,
        MonthlyRent = model.MonthlyRent,
        DepositAmount = model.DepositAmount,
        Parties =
        [
            new ContractPartyInput(model.LessorResidentId, ContractPartyRole.Lessor),
            new ContractPartyInput(model.LesseeResidentId, ContractPartyRole.Lessee)
        ]
    };

    private void ValidateParties(ContractEditViewModel model)
    {
        if (model.LessorResidentId > 0 && model.LessorResidentId == model.LesseeResidentId)
            ModelState.AddModelError(nameof(model.LesseeResidentId), "Bên cho thuê và bên thuê phải là hai cư dân khác nhau.");
    }

    private void ValidateReason(ContractActionViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Reason))
            ModelState.AddModelError(nameof(model.Reason), "Vui lòng nhập lý do.");
    }

    private static bool TryDecodeRowVersion(string? token, out byte[] rowVersion)
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

    private string CurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Tài khoản đăng nhập không hợp lệ.");
}
