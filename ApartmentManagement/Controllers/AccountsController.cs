using System.Security.Claims;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApartmentManagement.Controllers;

[Authorize(Policy = AppPolicies.RequireSuperAdmin)]
public sealed class AccountsController : Controller
{
    private readonly IAccountManagementService _service;
    public AccountsController(IAccountManagementService service) => _service = service;

    public async Task<IActionResult> Index(string? searchTerm)
    {
        ViewBag.CurrentSearch = searchTerm;
        return View(await _service.GetAllAsync(searchTerm));
    }

    public async Task<IActionResult> Details(string id)
    {
        var account = await _service.GetByIdAsync(id);
        if (account == null) return NotFound();
        ViewBag.AllRoles = AppRoles.AllRoles;
        return View(account);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(string id, bool isActive)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _service.SetActiveAsync(id, isActive, currentUserId);
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Success
            ? (isActive ? "Đã kích hoạt tài khoản." : "Đã vô hiệu hóa tài khoản và thu hồi phiên đăng nhập.")
            : result.ErrorMessage;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string id)
    {
        if (await _service.GetByIdAsync(id) == null) return NotFound();
        return View(new ResetPasswordViewModel { UserId = id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await _service.ResetPasswordAsync(model.UserId, model.TemporaryPassword);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return View(model);
        }
        TempData["SuccessMessage"] = "Đã đặt lại mật khẩu. Mật khẩu không được lưu hoặc ghi log.";
        return RedirectToAction(nameof(Details), new { id = model.UserId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(ChangeRoleViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Role không hợp lệ.";
            return RedirectToAction(nameof(Details), new { id = model.UserId });
        }
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _service.ChangeRoleAsync(model.UserId, model.Role, currentUserId);
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] =
            result.Success ? "Đã cập nhật role và thu hồi phiên đăng nhập cũ." : result.ErrorMessage;
        return RedirectToAction(nameof(Details), new { id = model.UserId });
    }
}
