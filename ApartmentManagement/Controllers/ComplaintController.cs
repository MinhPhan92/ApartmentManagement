using ApartmentManagement.Services;
using ApartmentManagement.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApartmentManagement.Controllers
{
    [Authorize]
    public class ComplaintController : Controller
    {
        private readonly IComplaintService _complaintService;
        private readonly IResidentService _residentService;

        public ComplaintController(
            IComplaintService complaintService,
            IResidentService residentService)
        {
            _complaintService = complaintService;
            _residentService = residentService;
        }

        // GET: /Complaint
        [HttpGet]
        [Authorize(Roles = "SuperAdmin,BuildingManager,SystemAdmin,Manager,Resident")]
        public async Task<IActionResult> Index(string? searchTerm, string? status, string? category)
        {
            var complaints = await _complaintService.GetComplaintsAsync(searchTerm, status, category);
            ViewBag.SearchTerm = searchTerm;
            ViewBag.SelectedStatus = status;
            ViewBag.SelectedCategory = category;

            return View(complaints);
        }

        // GET: /Complaint/Details/5
        [HttpGet]
        [Authorize(Roles = "SuperAdmin,BuildingManager,SystemAdmin,Manager,Resident")]
        public async Task<IActionResult> Details(int id)
        {
            if (id <= 0)
            {
                return NotFound();
            }

            var complaint = await _complaintService.GetComplaintByIdAsync(id);
            if (complaint == null)
            {
                return NotFound();
            }

            return View(complaint);
        }

        // GET: /Complaint/Create
        [HttpGet]
        [Authorize(Roles = "Resident")]
        public async Task<IActionResult> Create()
        {
            ViewBag.MyApartments = await _residentService.GetApartmentsForCurrentResidentAsync();
            var model = new ComplaintCreateViewModel();
            return View(model);
        }

        // POST: /Complaint/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Resident")]
        public async Task<IActionResult> Create(ComplaintCreateViewModel model)
        {
            if (model == null)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                ViewBag.MyApartments = await _residentService.GetApartmentsForCurrentResidentAsync();
                return View(model);
            }

            var result = await _complaintService.CreateComplaintAsync(model);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage!);
                ViewBag.MyApartments = await _residentService.GetApartmentsForCurrentResidentAsync();
                return View(model);
            }

            TempData["SuccessMessage"] = "Gửi phản ánh thành công! Ban Quản Lý sẽ sớm tiếp nhận và phản hồi.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Complaint/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Resident")]
        public async Task<IActionResult> Cancel(int id)
        {
            if (id <= 0)
            {
                return NotFound();
            }

            var result = await _complaintService.CancelComplaintAsync(id);
            if (!result.Success)
            {
                if (result.ErrorMessage == "Không tìm thấy phản ánh.")
                {
                    return NotFound();
                }

                TempData["ErrorMessage"] = result.ErrorMessage;
            }
            else
            {
                TempData["SuccessMessage"] = "Đã rút phản ánh thành công.";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: /Complaint/Process/5
        [HttpGet]
        [Authorize(Roles = "SuperAdmin,BuildingManager,SystemAdmin,Manager")]
        public async Task<IActionResult> Process(int id)
        {
            if (id <= 0)
            {
                return NotFound();
            }

            var complaint = await _complaintService.GetComplaintByIdAsync(id);
            if (complaint == null)
            {
                return NotFound();
            }

            if (complaint.Status == "Đã hủy")
            {
                TempData["ErrorMessage"] = "Không thể xử lý phản ánh đã bị cư dân hủy.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var model = new ComplaintProcessViewModel
            {
                ComplaintId = complaint.ComplaintId,
                Title = complaint.Title,
                Content = complaint.Content,
                Category = complaint.Category,
                ResidentName = complaint.Resident?.User?.FullName,
                ApartmentCode = complaint.Apartment?.ApartmentCode,
                CurrentStatus = complaint.Status,
                TargetStatus = complaint.Status == "Chờ tiếp nhận" ? "Đang xử lý" : complaint.Status,
                ResponseNotes = complaint.ResponseNotes
            };

            return View(model);
        }

        // POST: /Complaint/Process
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "SuperAdmin,BuildingManager,SystemAdmin,Manager")]
        public async Task<IActionResult> Process(ComplaintProcessViewModel model)
        {
            if (model == null || model.ComplaintId <= 0)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                await RepopulateProcessViewModelAsync(model);
                return View(model);
            }

            var result = await _complaintService.ProcessComplaintAsync(model);
            if (!result.Success)
            {
                if (result.ErrorMessage == "Không tìm thấy phản ánh.")
                {
                    return NotFound();
                }

                ModelState.AddModelError(string.Empty, result.ErrorMessage!);
                await RepopulateProcessViewModelAsync(model);
                return View(model);
            }

            TempData["SuccessMessage"] = $"Đã cập nhật trạng thái phản ánh sang '{model.TargetStatus}'.";
            return RedirectToAction(nameof(Details), new { id = model.ComplaintId });
        }

        private async Task RepopulateProcessViewModelAsync(ComplaintProcessViewModel model)
        {
            var complaint = await _complaintService.GetComplaintByIdAsync(model.ComplaintId);
            if (complaint != null)
            {
                model.Title = complaint.Title;
                model.Content = complaint.Content;
                model.Category = complaint.Category;
                model.ResidentName = complaint.Resident?.User?.FullName;
                model.ApartmentCode = complaint.Apartment?.ApartmentCode;
                model.CurrentStatus = complaint.Status;
            }
        }
    }
}
