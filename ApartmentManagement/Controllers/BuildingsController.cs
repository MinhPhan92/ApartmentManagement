using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApartmentManagement.Controllers
{
    [Authorize(Roles = "SystemAdmin,Manager")]
    public class BuildingsController : Controller
    {
        private readonly IBuildingService _buildingService;

        public BuildingsController(IBuildingService buildingService)
        {
            _buildingService = buildingService;
        }

        // GET: Buildings
        public async Task<IActionResult> Index(string? searchTerm)
        {
            ViewBag.CurrentSearch = searchTerm;
            var buildings = await _buildingService.GetAllBuildingsAsync(searchTerm);
            return View(buildings);
        }

        // GET: Buildings/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var building = await _buildingService.GetBuildingByIdAsync(id.Value, includeApartments: true);
            if (building == null)
            {
                return NotFound();
            }

            return View(building);
        }

        // GET: Buildings/Create
        public IActionResult Create()
        {
            var model = new Building
            {
                NumberOfFloors = 1
            };
            return View(model);
        }

        // POST: Buildings/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("BuildingCode,BuildingName,Address,NumberOfFloors")] Building building)
        {
            if (ModelState.IsValid)
            {
                var result = await _buildingService.CreateBuildingAsync(building);
                if (result.Success)
                {
                    TempData["SuccessMessage"] = $"Đã tạo mới tòa nhà '{building.BuildingName}' ({building.BuildingCode}) thành công.";
                    return RedirectToAction(nameof(Index));
                }

                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể tạo tòa nhà.");
            }

            return View(building);
        }

        // GET: Buildings/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var building = await _buildingService.GetBuildingByIdAsync(id.Value);
            if (building == null)
            {
                return NotFound();
            }

            return View(building);
        }

        // POST: Buildings/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("BuildingId,BuildingCode,BuildingName,Address,NumberOfFloors,CreatedAt")] Building building)
        {
            if (id != building.BuildingId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var result = await _buildingService.UpdateBuildingAsync(building);
                if (result.Success)
                {
                    TempData["SuccessMessage"] = $"Cập nhật tòa nhà '{building.BuildingName}' thành công.";
                    return RedirectToAction(nameof(Index));
                }

                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể cập nhật tòa nhà.");
            }

            return View(building);
        }

        // GET: Buildings/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var building = await _buildingService.GetBuildingByIdAsync(id.Value, includeApartments: true);
            if (building == null)
            {
                return NotFound();
            }

            ViewBag.ApartmentCount = building.Apartments.Count;
            return View(building);
        }

        // POST: Buildings/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var result = await _buildingService.DeleteBuildingAsync(id);
            if (result.Success)
            {
                TempData["SuccessMessage"] = "Đã xóa tòa nhà thành công.";
            }
            else
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
            }

            return RedirectToAction(nameof(Index));
        }

        // Remote validation or AJAX check for BuildingCode uniqueness
        [HttpGet]
        public async Task<IActionResult> CheckCodeExists(string buildingCode, int? buildingId)
        {
            var exists = await _buildingService.BuildingCodeExistsAsync(buildingCode, buildingId);
            return Json(new { exists });
        }
    }
}