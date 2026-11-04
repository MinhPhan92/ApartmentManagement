using System.Security.Claims;
using ApartmentManagement.Services;
using ApartmentManagement.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApartmentManagement.Controllers
{
    [Authorize]
    public class VehicleController : Controller
    {
        private readonly IVehicleService _vehicleService;

        public VehicleController(IVehicleService vehicleService)
        {
            _vehicleService = vehicleService;
        }

        // GET: /Vehicle
        [HttpGet]
        [Authorize(Roles = "SuperAdmin,BuildingManager,Resident")]
        public async Task<IActionResult> Index(string? searchTerm, string? status)
        {
            var vehicles = await _vehicleService.GetVehiclesAsync(searchTerm, status);
            ViewBag.SearchTerm = searchTerm;
            ViewBag.SelectedStatus = status;

            return View(vehicles);
        }

        // GET: /Vehicle/Details/5
        [HttpGet]
        [Authorize(Roles = "SuperAdmin,BuildingManager,Resident")]
        public async Task<IActionResult> Details(int id)
        {
            var vehicle = await _vehicleService.GetVehicleByIdAsync(id);
            if (vehicle == null)
            {
                return NotFound();
            }

            return View(vehicle);
        }

        // GET: /Vehicle/Register
        [HttpGet]
        [Authorize(Roles = "Resident")]
        public IActionResult Register()
        {
            var model = new VehicleRegisterViewModel();
            return View(model);
        }

        // POST: /Vehicle/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Resident")]
        public async Task<IActionResult> Register(VehicleRegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _vehicleService.RegisterVehicleAsync(model);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage!);
                return View(model);
            }

            TempData["SuccessMessage"] = "Đăng ký phương tiện thành công! Đơn đang chờ ban quản lý xét duyệt.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Vehicle/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Resident")]
        public async Task<IActionResult> Cancel(int id)
        {
            var result = await _vehicleService.CancelRegistrationAsync(id);
            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
            }
            else
            {
                TempData["SuccessMessage"] = "Đã hủy đơn đăng ký phương tiện.";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: /Vehicle/Review/5
        [HttpGet]
        [Authorize(Roles = "SuperAdmin,BuildingManager")]
        public async Task<IActionResult> Review(int id)
        {
            var vehicle = await _vehicleService.GetVehicleByIdAsync(id);
            if (vehicle == null)
            {
                return NotFound();
            }

            if (vehicle.Status != "Chờ duyệt")
            {
                TempData["ErrorMessage"] = $"Phương tiện này đang ở trạng thái '{vehicle.Status}', không cần duyệt lại.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var model = new VehicleReviewViewModel
            {
                VehicleId = vehicle.VehicleId,
                LicensePlate = vehicle.LicensePlate,
                VehicleType = vehicle.VehicleType,
                ResidentName = vehicle.Resident?.User?.FullName ?? string.Empty
            };

            return View(model);
        }

        // POST: /Vehicle/Approve
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "SuperAdmin,BuildingManager,SystemAdmin,Manager")]
        public async Task<IActionResult> Approve(int? vehicleId, int? id, string? cardCode, string? reviewNotes, string? notes)
        {
            var targetId = vehicleId ?? id ?? 0;
            if (targetId <= 0) return NotFound();

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var notesToUse = reviewNotes ?? notes;

            var result = await _vehicleService.ApproveVehicleAsync(targetId, cardCode, notesToUse, currentUserId);
            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
            }
            else
            {
                TempData["SuccessMessage"] = "Phê duyệt phương tiện và cấp thẻ bãi đỗ thành công!";
            }

            return RedirectToAction(nameof(Details), new { id = targetId });
        }

        // POST: /Vehicle/Reject
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "SuperAdmin,BuildingManager,SystemAdmin,Manager")]
        public async Task<IActionResult> Reject(int? vehicleId, int? id, string? rejectReason, string? reason)
        {
            var targetId = vehicleId ?? id ?? 0;
            if (targetId <= 0) return NotFound();

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var reasonToUse = rejectReason ?? reason ?? string.Empty;

            var result = await _vehicleService.RejectVehicleAsync(targetId, reasonToUse, currentUserId);
            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
            }
            else
            {
                TempData["SuccessMessage"] = "Đã từ chối đơn đăng ký phương tiện.";
            }

            return RedirectToAction(nameof(Details), new { id = targetId });
        }

        // POST: /Vehicle/LockCard
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LockCard(int? vehicleId, int? id, string? reason)
        {
            var targetId = vehicleId ?? id ?? 0;
            if (targetId <= 0) return NotFound();

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var isManagerOrAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("BuildingManager") || User.IsInRole("SystemAdmin") || User.IsInRole("Manager");

            if (!isManagerOrAdmin)
            {
                var vehicle = await _vehicleService.GetVehicleByIdAsync(targetId);
                if (vehicle == null) return NotFound();
                if (vehicle.Resident?.UserId != currentUserId)
                {
                    return Forbid();
                }
            }

            var result = await _vehicleService.ReportLostOrLockCardAsync(targetId, reason ?? "Báo mất thẻ", currentUserId);
            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
            }
            else
            {
                TempData["SuccessMessage"] = "Đã khóa thẻ bãi đỗ xe thành công.";
            }

            return RedirectToAction(nameof(Details), new { id = targetId });
        }

        // POST: /Vehicle/ReplaceCard
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "SuperAdmin,BuildingManager,SystemAdmin,Manager")]
        public async Task<IActionResult> ReplaceCard(int? vehicleId, int? id, string? newCardCode, string? cardCode)
        {
            var targetId = vehicleId ?? id ?? 0;
            if (targetId <= 0) return NotFound();

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var codeToUse = newCardCode ?? cardCode ?? string.Empty;

            var result = await _vehicleService.IssueOrReplaceCardAsync(targetId, codeToUse, currentUserId);
            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
            }
            else
            {
                TempData["SuccessMessage"] = "Cấp đổi thẻ bãi đỗ xe thành công!";
            }

            return RedirectToAction(nameof(Details), new { id = targetId });
        }

        // POST: /Vehicle/Decommission/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Decommission(int id, string? reason)
        {
            var result = await _vehicleService.DecommissionVehicleAsync(id, reason);
            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
            }
            else
            {
                TempData["SuccessMessage"] = "Đã chuyển phương tiện sang trạng thái Ngừng sử dụng.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
