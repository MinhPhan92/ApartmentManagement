using System.Globalization;
using System.Security.Claims;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ApartmentManagement.Controllers;

[Authorize(Policy = AppPolicies.RequireAccountantOrManager)]
public sealed class BillingController : Controller
{
    private readonly IBillingService _billingService;
    private readonly IBuildingService _buildingService;
    private readonly IApartmentService _apartmentService;

    public BillingController(
        IBillingService billingService,
        IBuildingService buildingService,
        IApartmentService apartmentService)
    {
        _billingService = billingService;
        _buildingService = buildingService;
        _apartmentService = apartmentService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        int? buildingId = null,
        int? year = null,
        int? month = null,
        ApartmentInvoiceStatus? status = null,
        string? paymentStatus = null,
        string? searchTerm = null)
    {
        bool? isPaid = paymentStatus switch
        {
            "Paid" => true,
            "Unpaid" => false,
            _ => null
        };

        var criteria = new InvoiceFilterCriteria(
            BuildingId: buildingId,
            Year: year,
            Month: month,
            Status: status,
            IsPaid: isPaid,
            SearchTerm: searchTerm);

        var invoices = await _billingService.GetInvoicesAsync(criteria);
        var tariffs = await _billingService.GetTariffsAsync(buildingId);
        var summary = await _billingService.GetBillingSummaryAsync(buildingId, year, month);

        var buildings = await _buildingService.GetAllBuildingsAsync();
        var buildingOptions = buildings.Select(b => new SelectListItem(
            b.BuildingName, b.BuildingId.ToString(), b.BuildingId == buildingId)).ToList();

        var statusOptions = new List<SelectListItem>
        {
            new("Tất cả trạng thái", ""),
            new("Nháp", ApartmentInvoiceStatus.Draft.ToString(), status == ApartmentInvoiceStatus.Draft),
            new("Đã phát hành", ApartmentInvoiceStatus.Issued.ToString(), status == ApartmentInvoiceStatus.Issued),
            new("Đã hủy", ApartmentInvoiceStatus.Void.ToString(), status == ApartmentInvoiceStatus.Void)
        };

        var paymentStatusOptions = new List<SelectListItem>
        {
            new("Tất cả thanh toán", ""),
            new("Đã thanh toán", "Paid", paymentStatus == "Paid"),
            new("Chưa thanh toán", "Unpaid", paymentStatus == "Unpaid")
        };

        return View(new BillingIndexViewModel
        {
            Invoices = invoices,
            Tariffs = tariffs,
            Summary = summary,
            BuildingId = buildingId,
            BillingYear = year,
            BillingMonth = month,
            Status = status,
            PaymentStatus = paymentStatus,
            SearchTerm = searchTerm,
            BuildingOptions = buildingOptions,
            StatusOptions = statusOptions,
            PaymentStatusOptions = paymentStatusOptions
        });
    }


    [HttpGet]
    public async Task<IActionResult> CreateTariff()
    {
        var model = new FeeTariffEditViewModel();
        await PopulateBuildingsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTariff(FeeTariffEditViewModel model)
    {
        if (model == null) return BadRequest();
        var userId = CurrentUserId();
        if (userId == null) return Forbid();

        var tiersParsed = TryParseTiers(model.TierDefinitions, out var tiers);
        if (model.ChargeType is FeeChargeType.Electricity or FeeChargeType.Water && !tiersParsed)
            ModelState.AddModelError(nameof(model.TierDefinitions),
                "Nhập mỗi bậc một dòng theo dạng ngưỡng|đơn giá; dùng * cho ngưỡng không giới hạn ở dòng cuối.");

        if (!ModelState.IsValid)
        {
            await PopulateBuildingsAsync(model);
            return View(model);
        }

        var result = await _billingService.CreateTariffAsync(new FeeTariffInput(
            model.BuildingId,
            model.ChargeType,
            model.EffectiveFrom,
            model.EffectiveTo,
            model.MonthlyRatePerSquareMeter,
            tiers), userId);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể lưu biểu phí.");
            await PopulateBuildingsAsync(model);
            return View(model);
        }
        TempData["SuccessMessage"] = "Đã thêm biểu phí.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> RecordReading()
    {
        var model = new MeterReadingEditViewModel();
        await PopulateApartmentsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordReading(MeterReadingEditViewModel model)
    {
        if (model == null) return BadRequest();
        var userId = CurrentUserId();
        if (userId == null) return Forbid();

        if (!ModelState.IsValid)
        {
            await PopulateApartmentsAsync(model);
            return View(model);
        }
        var result = await _billingService.RecordReadingAsync(new MeterReadingInput(
            model.ApartmentId,
            model.UtilityType,
            model.BillingYear,
            model.BillingMonth,
            model.PreviousReading,
            model.CurrentReading), userId);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể lưu chỉ số.");
            await PopulateApartmentsAsync(model);
            return View(model);
        }
        TempData["SuccessMessage"] = "Đã lưu chỉ số công tơ.";
        return RedirectToAction(nameof(Index));
    }

[HttpGet]
    public async Task<IActionResult> RecordBatchReadings(int? buildingId = null, int? year = null, int? month = null)
    {
        var targetYear = year ?? DateTime.Today.Year;
        var targetMonth = month ?? DateTime.Today.Month;
        var targetBuildingId = buildingId ?? 0;

        BatchMeterReadingViewModel model;
        if (targetBuildingId > 0)
        {
            model = await _billingService.GetBatchMeterReadingSheetAsync(targetBuildingId, targetYear, targetMonth);
        }
        else
        {
            model = new BatchMeterReadingViewModel
            {
                BillingYear = targetYear,
                BillingMonth = targetMonth
            };
        }

        await PopulateBuildingsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordBatchReadings(BatchMeterReadingViewModel model)
    {
        if (model == null) return BadRequest();
        var userId = CurrentUserId();
        if (userId == null) return Forbid();

        if (!ModelState.IsValid)
        {
            await PopulateBuildingsAsync(model);
            return View(model);
        }

        var input = new BatchMeterReadingSaveInput(
            model.BuildingId,
            model.BillingYear,
            model.BillingMonth,
            model.Rows.Select(r => new BatchMeterReadingItemInput(
                r.ApartmentId,
                r.ApartmentCode,
                r.ElectricityOldReading,
                r.ElectricityNewReading,
                r.WaterOldReading,
                r.WaterNewReading)).ToList());

        var result = await _billingService.SaveBatchMeterReadingsAsync(input, userId);
        model.Result = result;

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể lưu chỉ số.");
        }
        else
        {
            if (result.SavedCount > 0)
            {
                TempData["SuccessMessage"] = $"Đã lưu thành công chỉ số cho {result.SavedCount} căn hộ ({result.SkippedCount} bỏ qua, {result.FailedCount} lỗi).";
            }
            else if (result.FailedCount > 0)
            {
                TempData["ErrorMessage"] = $"Có {result.FailedCount} căn hộ phát sinh lỗi khi lưu chỉ số.";
            }

            var refreshed = await _billingService.GetBatchMeterReadingSheetAsync(model.BuildingId, model.BillingYear, model.BillingMonth);
            model.Rows = refreshed.Rows;
            model.BuildingName = refreshed.BuildingName;
        }

        await PopulateBuildingsAsync(model);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> CreateInvoice()
    {
        var model = new GenerateInvoiceViewModel();
        await PopulateApartmentsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateInvoice(GenerateInvoiceViewModel model)
    {
        if (model == null) return BadRequest();
        var userId = CurrentUserId();
        if (userId == null) return Forbid();

        if (!ModelState.IsValid)
        {
            await PopulateApartmentsAsync(model);
            return View(model);
        }
        var result = await _billingService.GenerateDraftAsync(
            model.ApartmentId, model.BillingYear, model.BillingMonth, model.DueDate, userId);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể lập hóa đơn.");
            await PopulateApartmentsAsync(model);
            return View(model);
        }
        return RedirectToAction(nameof(Details), new { id = result.Invoice!.ApartmentInvoiceId });
    }

    [HttpGet]
    public async Task<IActionResult> BatchCreateInvoices(int? buildingId = null, int? year = null, int? month = null)
    {
        var targetYear = year ?? DateTime.Today.Year;
        var targetMonth = month ?? DateTime.Today.Month;
        var defaultDueDate = new DateTime(targetYear, targetMonth, 1).AddMonths(1).AddDays(14);

        var model = new BatchGenerateInvoicesViewModel
        {
            BuildingId = buildingId ?? 0,
            BillingYear = targetYear,
            BillingMonth = targetMonth,
            DueDate = defaultDueDate
        };

        await PopulateBuildingsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BatchCreateInvoices(BatchGenerateInvoicesViewModel model)
    {
        if (model == null) return BadRequest();
        var userId = CurrentUserId();
        if (userId == null) return Forbid();

        if (!ModelState.IsValid)
        {
            await PopulateBuildingsAsync(model);
            return View(model);
        }

        var result = await _billingService.GenerateBuildingDraftsAsync(
            model.BuildingId, model.BillingYear, model.BillingMonth, model.DueDate, userId);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể lập hóa đơn hàng loạt.");
            await PopulateBuildingsAsync(model);
            return View(model);
        }

        model.Result = result;
        await PopulateBuildingsAsync(model);
        TempData["SuccessMessage"] = $"Đã xử lý sinh hóa đơn tòa nhà: {result.CreatedCount} tạo mới, {result.AlreadyExistsCount} đã có sẵn, {result.SkippedCount} bỏ qua, {result.FailedCount} lỗi.";
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> BatchIssueInvoices(int? buildingId = null, int? year = null, int? month = null)
    {
        var targetYear = year ?? DateTime.Today.Year;
        var targetMonth = month ?? DateTime.Today.Month;
        var targetBuildingId = buildingId ?? 0;

        var model = new BatchIssueInvoicesViewModel
        {
            BuildingId = targetBuildingId,
            BillingYear = targetYear,
            BillingMonth = targetMonth
        };

        await PopulateBuildingsAsync(model);

        if (targetBuildingId > 0)
        {
            var drafts = await _billingService.GetDraftInvoicesForBatchIssueAsync(targetBuildingId, targetYear, targetMonth);
            model.DraftInvoices = drafts.Select(d => new BatchIssueItemViewModel
            {
                IsSelected = true,
                InvoiceId = d.InvoiceId,
                InvoiceCode = d.InvoiceCode,
                ApartmentCode = d.ApartmentCode,
                ResidentName = d.ResidentName,
                ManagementFee = d.ManagementFee,
                ElectricityFee = d.ElectricityFee,
                WaterFee = d.WaterFee,
                TotalAmount = d.TotalAmount,
                RowVersionToken = d.RowVersionToken
            }).ToList();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BatchIssueInvoices(BatchIssueInvoicesViewModel model)
    {
        if (model == null) return BadRequest();
        var userId = CurrentUserId();
        if (userId == null) return Forbid();

        var selected = model.DraftInvoices?.Where(x => x.IsSelected).ToList() ?? [];
        if (selected.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Vui lòng chọn ít nhất một hóa đơn để phát hành.");
            await PopulateBuildingsAsync(model);
            return View(model);
        }

        var issueItems = new List<BatchIssueItemInput>();
        foreach (var item in selected)
        {
            if (TryDecodeRowVersion(item.RowVersionToken, out var rowVersion))
            {
                issueItems.Add(new BatchIssueItemInput(item.InvoiceId, rowVersion));
            }
            else
            {
                issueItems.Add(new BatchIssueItemInput(item.InvoiceId, []));
            }
        }

        var input = new BatchIssueInput(model.BuildingId, model.BillingYear, model.BillingMonth, issueItems);
        var result = await _billingService.IssueBuildingInvoicesAsync(input, userId);

        model.Result = result;
        await PopulateBuildingsAsync(model);

        if (result.Success)
        {
            TempData["SuccessMessage"] = $"Đã phát hành: {result.IssuedCount} thành công, {result.AlreadyIssuedCount} đã phát hành trước, {result.FailedCount} lỗi.";
            // Tải lại danh sách hóa đơn Draft còn lại
            var remainingDrafts = await _billingService.GetDraftInvoicesForBatchIssueAsync(model.BuildingId, model.BillingYear, model.BillingMonth);
            model.DraftInvoices = remainingDrafts.Select(d => new BatchIssueItemViewModel
            {
                IsSelected = true,
                InvoiceId = d.InvoiceId,
                InvoiceCode = d.InvoiceCode,
                ApartmentCode = d.ApartmentCode,
                ResidentName = d.ResidentName,
                ManagementFee = d.ManagementFee,
                ElectricityFee = d.ElectricityFee,
                WaterFee = d.WaterFee,
                TotalAmount = d.TotalAmount,
                RowVersionToken = d.RowVersionToken
            }).ToList();
        }
        else
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể phát hành hóa đơn hàng loạt.");
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        if (id <= 0) return NotFound();
        var invoice = await _billingService.GetInvoiceAsync(id);
        return invoice == null ? NotFound() : View(invoice);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Issue(IssueInvoiceViewModel model)
    {
        if (model == null || model.InvoiceId <= 0) return NotFound();
        var userId = CurrentUserId();
        if (userId == null) return Forbid();

        if (!TryDecodeRowVersion(model.RowVersionToken, out var rowVersion))
        {
            TempData["ErrorMessage"] = "Phiên bản hóa đơn không hợp lệ. Vui lòng tải lại dữ liệu.";
            return RedirectToAction(nameof(Details), new { id = model.InvoiceId });
        }

        var result = await _billingService.IssueAsync(model.InvoiceId, userId, rowVersion);
        if (!result.Success)
        {
            if (result.ErrorMessage == "Không tìm thấy hóa đơn.")
                return NotFound();

            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Đã phát hành hóa đơn cho cư dân.";
        }
        return RedirectToAction(nameof(Details), new { id = model.InvoiceId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Void(VoidInvoiceViewModel model)
    {
        if (model == null || model.InvoiceId <= 0) return NotFound();
        var userId = CurrentUserId();
        if (userId == null) return Forbid();

        if (!TryDecodeRowVersion(model.RowVersionToken, out var rowVersion))
        {
            TempData["ErrorMessage"] = "Phiên bản hóa đơn không hợp lệ. Vui lòng tải lại dữ liệu.";
            return RedirectToAction(nameof(Details), new { id = model.InvoiceId });
        }

        var result = await _billingService.VoidAsync(model.InvoiceId, userId, rowVersion, model.Reason);
        if (!result.Success)
        {
            if (result.ErrorMessage == "Không tìm thấy hóa đơn.")
                return NotFound();

            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Đã hủy hóa đơn thành công.";
        }
        return RedirectToAction(nameof(Details), new { id = model.InvoiceId });
    }

    [HttpGet]
    public async Task<IActionResult> TariffDetails(int id)
    {
        if (id <= 0) return NotFound();
        var tariff = await _billingService.GetTariffAsync(id);
        if (tariff == null) return NotFound();

        var today = DateTime.Today;
        var isActive = tariff.EffectiveFrom.Date <= today && (!tariff.EffectiveTo.HasValue || tariff.EffectiveTo.Value >= today);
        var canClose = !tariff.EffectiveTo.HasValue || tariff.EffectiveTo.Value >= today;

        return View(new FeeTariffDetailsViewModel
        {
            Tariff = tariff,
            InvoicesCount = tariff.InvoiceLines.Count,
            IsActive = isActive,
            CanClose = canClose
        });
    }

    [HttpGet]
    public async Task<IActionResult> CloseTariff(int id)
    {
        if (id <= 0) return NotFound();
        var tariff = await _billingService.GetTariffAsync(id);
        if (tariff == null) return NotFound();

        return View(new CloseTariffViewModel
        {
            FeeTariffId = tariff.FeeTariffId,
            BuildingName = tariff.Building?.BuildingName ?? "Chung cư",
            ChargeType = tariff.ChargeType,
            EffectiveFrom = tariff.EffectiveFrom,
            EffectiveTo = tariff.EffectiveTo ?? DateTime.Today
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseTariff(CloseTariffViewModel model)
    {
        if (model == null || model.FeeTariffId <= 0) return NotFound();
        var userId = CurrentUserId();
        if (userId == null) return Forbid();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _billingService.CloseTariffAsync(model.FeeTariffId, model.EffectiveTo, userId);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể cập nhật biểu phí.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã cập nhật thời hạn kết thúc biểu phí.";
        return RedirectToAction(nameof(TariffDetails), new { id = model.FeeTariffId });
    }


private async Task PopulateBuildingsAsync(BatchMeterReadingViewModel model)
    {
        var buildings = await _buildingService.GetAllBuildingsAsync();
        model.BuildingOptions = buildings.Select(x => new SelectListItem(
            $"{x.BuildingName} ({x.BuildingCode})", x.BuildingId.ToString(), x.BuildingId == model.BuildingId)).ToList();
    }

        private async Task PopulateBuildingsAsync(BatchGenerateInvoicesViewModel model)
    {
        var buildings = await _buildingService.GetAllBuildingsAsync();
        model.BuildingOptions = buildings.Select(x => new SelectListItem(
            $"{x.BuildingName} ({x.BuildingCode})", x.BuildingId.ToString(), x.BuildingId == model.BuildingId)).ToList();
    }

    private async Task PopulateBuildingsAsync(BatchIssueInvoicesViewModel model)
    {
        var buildings = await _buildingService.GetAllBuildingsAsync();
        model.BuildingOptions = buildings.Select(x => new SelectListItem(
            $"{x.BuildingName} ({x.BuildingCode})", x.BuildingId.ToString(), x.BuildingId == model.BuildingId)).ToList();
    }

    private async Task PopulateBuildingsAsync(FeeTariffEditViewModel model)
    {
        var buildings = await _buildingService.GetAllBuildingsAsync();
        model.BuildingOptions = buildings.Select(x => new SelectListItem(
            $"{x.BuildingName} ({x.BuildingCode})", x.BuildingId.ToString())).ToList();
    }

    private async Task PopulateApartmentsAsync(MeterReadingEditViewModel model)
    {
        var apartments = await _apartmentService.GetAllApartmentsAsync();
        model.ApartmentOptions = apartments.Select(ApartmentOption).ToList();
    }

    private async Task PopulateApartmentsAsync(GenerateInvoiceViewModel model)
    {
        var apartments = await _apartmentService.GetAllApartmentsAsync();
        model.ApartmentOptions = apartments.Select(ApartmentOption).ToList();
    }

    private static SelectListItem ApartmentOption(Apartment apartment) => new(
        $"{apartment.Building?.BuildingName} — {apartment.ApartmentCode}", apartment.ApartmentId.ToString());

    private string? CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private static bool TryDecodeRowVersion(string? token, out byte[] rowVersion)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            rowVersion = [];
            return false;
        }

        try
        {
            rowVersion = Convert.FromBase64String(token.Trim());
            return rowVersion.Length > 0;
        }
        catch (FormatException)
        {
            rowVersion = [];
            return false;
        }
    }

    private static bool TryParseTiers(
        string? text,
        out IReadOnlyList<FeeTariffTierInput> tiers)
    {
        var parsed = new List<FeeTariffTierInput>();
        var lines = (text ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length != 2 ||
                !decimal.TryParse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var rate) ||
                rate < 0)
            {
                tiers = [];
                return false;
            }

            decimal? upper = null;
            if (!string.Equals(parts[0], "*", StringComparison.Ordinal))
            {
                if (!decimal.TryParse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedUpper) ||
                    parsedUpper <= 0)
                {
                    tiers = [];
                    return false;
                }
                upper = parsedUpper;
            }
            parsed.Add(new FeeTariffTierInput(parsed.Count + 1, upper, rate));
        }
        tiers = parsed;
        return parsed.Count > 0;
    }
}
