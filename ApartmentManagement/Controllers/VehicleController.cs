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
        [Authorize(Roles = "SuperAdmin,BuildingManager,SystemAdmin,Manager,Resident")]
        public async Task<IActionResult> Index(string? searchTerm, string? status)
        {
            var vehicles = await _vehicleService.GetVehiclesAsync(searchTerm, status);
            ViewBag.SearchTerm = searchTerm;
            ViewBag.SelectedStatus = status;

            return View(vehicles);
        }

        // GET: /Vehicle/Details/5
        [HttpGet]
        [Authorize(Roles = "SuperAdmin,BuildingManager,SystemAdmin,Manager,Resident")]
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
        [Authorize(Roles = "SuperAdmin,BuildingManager,SystemAdmin,Manager")]
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
                ResidentName = vehicle.Resident?.User?.FullName
            };

            return View(model);
        }

        // POST: /Vehicle/Approve/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "SuperAdmin,BuildingManager,SystemAdmin,Manager")]
        public async Task<IActionResult> Approve(int id, string? notes)
        {
            var result = await _vehicleService.ApproveVehicleAsync(id, notes);
            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
            }
            else
            {
                TempData["SuccessMessage"] = "Đã phê duyệt đăng ký phương tiện thành công.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: /Vehicle/Reject/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "SuperAdmin,BuildingManager,SystemAdmin,Manager")]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            var result = await _vehicleService.RejectVehicleAsync(id, reason);
            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
            }
            else
            {
                TempData["SuccessMessage"] = "Đã từ chối đơn đăng ký phương tiện.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
