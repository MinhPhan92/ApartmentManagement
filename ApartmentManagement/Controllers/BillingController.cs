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
    public async Task<IActionResult> Index()
    {
        return View(new BillingIndexViewModel
        {
            Invoices = await _billingService.GetInvoicesAsync(),
            Tariffs = await _billingService.GetTariffsAsync()
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
            tiers), CurrentUserId());
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
            model.CurrentReading), CurrentUserId());
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
        if (!ModelState.IsValid)
        {
            await PopulateApartmentsAsync(model);
            return View(model);
        }
        var result = await _billingService.GenerateDraftAsync(
            model.ApartmentId, model.BillingYear, model.BillingMonth, model.DueDate, CurrentUserId());
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể lập hóa đơn.");
            await PopulateApartmentsAsync(model);
            return View(model);
        }
        return RedirectToAction(nameof(Details), new { id = result.Invoice!.ApartmentInvoiceId });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var invoice = await _billingService.GetInvoiceAsync(id);
        return invoice == null ? NotFound() : View(invoice);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Issue(IssueInvoiceViewModel model)
    {
        if (!TryDecodeRowVersion(model.RowVersionToken, out var rowVersion))
            return BadRequest();
        var result = await _billingService.IssueAsync(model.InvoiceId, CurrentUserId(), rowVersion);
        if (!result.Success) TempData["ErrorMessage"] = result.ErrorMessage;
        else TempData["SuccessMessage"] = "Đã phát hành hóa đơn cho cư dân.";
        return RedirectToAction(nameof(Details), new { id = model.InvoiceId });
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

    private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Authenticated user has no identifier claim.");

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
