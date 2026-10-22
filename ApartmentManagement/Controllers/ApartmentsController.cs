using ApartmentManagement.Models;
using ApartmentManagement.Services;
using ApartmentManagement.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApartmentManagement.Controllers
{
    [Authorize(Policy = AppPolicies.RequireManagement)]
    public class ApartmentsController : Controller
    {
        private readonly IApartmentService _apartmentService;
        private readonly IBuildingService _buildingService;

        public ApartmentsController(
            IApartmentService apartmentService,
            IBuildingService buildingService)
        {
            _apartmentService = apartmentService;
            _buildingService = buildingService;
        }

        public async Task<IActionResult> Index(
            int? buildingId,
            string? searchTerm)
        {
            var apartments =
                await _apartmentService.GetAllApartmentsAsync(
                    buildingId,
                    searchTerm);

            ViewBag.Buildings =
                await _buildingService.GetAllBuildingsAsync();

            ViewBag.SelectedBuildingId = buildingId;
            ViewBag.SearchTerm = searchTerm;

            return View(apartments);
        }

        public async Task<IActionResult> Details(int id)
        {
            var apartment =
                await _apartmentService.GetApartmentByIdAsync(
                    id,
                    includeResidents: true);

            if (apartment == null)
            {
                return NotFound();
            }

            return View(apartment);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? buildingId)
        {
            ViewBag.Buildings =
                await _buildingService.GetAllBuildingsAsync();

            var model = new Apartment
            {
                BuildingId = buildingId ?? 0,
                Status = ApartmentStatus.Vacant,
                Floor = 1
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("BuildingId,ApartmentCode,Floor,Area,Status")] Apartment apartment)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Buildings =
                    await _buildingService.GetAllBuildingsAsync();

                return View(apartment);
            }

            var result =
                await _apartmentService.CreateApartmentAsync(
                    apartment);

            if (!result.Success)
            {
                ModelState.AddModelError(
                    string.Empty,
                    result.ErrorMessage!);

                ViewBag.Buildings =
                    await _buildingService.GetAllBuildingsAsync();

                return View(apartment);
            }

            TempData["SuccessMessage"] =
                "Thêm căn hộ thành công.";

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var apartment =
                await _apartmentService.GetApartmentByIdAsync(id);

            if (apartment == null)
            {
                return NotFound();
            }

            ViewBag.Buildings =
                await _buildingService.GetAllBuildingsAsync();

            return View(apartment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("ApartmentId,BuildingId,ApartmentCode,Floor,Area,Status")] Apartment apartment)
        {
            if (id != apartment.ApartmentId)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Buildings =
                    await _buildingService.GetAllBuildingsAsync();

                return View(apartment);
            }

            var result =
                await _apartmentService.UpdateApartmentAsync(
                    apartment);

            if (!result.Success)
            {
                ModelState.AddModelError(
                    string.Empty,
                    result.ErrorMessage!);

                ViewBag.Buildings =
                    await _buildingService.GetAllBuildingsAsync();

                return View(apartment);
            }

            TempData["SuccessMessage"] =
                "Cập nhật căn hộ thành công.";

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var apartment =
                await _apartmentService.GetApartmentByIdAsync(
                    id,
                    includeResidents: true);

            if (apartment == null)
            {
                return NotFound();
            }

            return View(apartment);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(
            int id)
        {
            var result =
                await _apartmentService.DeleteApartmentAsync(id);

            if (!result.Success)
            {
                TempData["ErrorMessage"] =
                    result.ErrorMessage;

                return RedirectToAction(nameof(Delete), new { id });
            }

            TempData["SuccessMessage"] =
                "Xóa căn hộ thành công.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(int id)
        {
            var result =
                await _apartmentService.DeactivateApartmentAsync(id);

            if (!result.Success)
            {
                TempData["ErrorMessage"] =
                    result.ErrorMessage;
            }
            else
            {
                TempData["SuccessMessage"] =
                    "Đã chuyển trạng thái căn hộ sang 'Không hoạt động'.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
