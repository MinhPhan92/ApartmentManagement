using ApartmentManagement.Data;
using ApartmentManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Services;

public sealed class BillingService : IBillingService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<BillingService> _logger;

    public BillingService(ApplicationDbContext context, ILogger<BillingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task<List<FeeTariff>> GetTariffsAsync(int? buildingId = null)
    {
        var query = _context.FeeTariffs.AsNoTracking()
            .Include(x => x.Building)
            .Include(x => x.Tiers)
            .OrderBy(x => x.Building.BuildingName)
            .ThenBy(x => x.ChargeType)
            .ThenByDescending(x => x.EffectiveFrom);
        return (buildingId.HasValue
                ? query.Where(x => x.BuildingId == buildingId.Value)
                : query)
            .ToListAsync();
    }

    public Task<List<ApartmentInvoice>> GetInvoicesAsync() =>
        InvoiceQuery().OrderByDescending(x => x.BillingYear)
            .ThenByDescending(x => x.BillingMonth)
            .ThenBy(x => x.Apartment.ApartmentCode)
            .ToListAsync();

    public Task<List<ApartmentInvoice>> GetResidentInvoicesAsync(string userId) =>
        InvoiceQuery().Where(x => x.Resident.UserId == userId &&
                x.Status == ApartmentInvoiceStatus.Issued)
            .OrderByDescending(x => x.BillingYear)
            .ThenByDescending(x => x.BillingMonth)
            .ToListAsync();

    public Task<ApartmentInvoice?> GetResidentInvoiceAsync(string userId, int invoiceId) =>
        InvoiceQuery().FirstOrDefaultAsync(x =>
            x.ApartmentInvoiceId == invoiceId &&
            x.Resident.UserId == userId &&
            x.Status == ApartmentInvoiceStatus.Issued);

    public Task<ApartmentInvoice?> GetInvoiceAsync(int invoiceId) =>
        InvoiceQuery().FirstOrDefaultAsync(x => x.ApartmentInvoiceId == invoiceId);

    public async Task<BillingOperationResult> CreateTariffAsync(
        FeeTariffInput input,
        string actorUserId)
    {
        var error = ValidateTariff(input);
        if (error != null) return Failure(error);
        if (!await _context.Buildings.AnyAsync(x => x.BuildingId == input.BuildingId))
            return Failure("Không tìm thấy tòa nhà.");

        var start = input.EffectiveFrom.Date;
        var end = input.EffectiveTo?.Date;
        var overlaps = await _context.FeeTariffs.AnyAsync(x =>
            x.BuildingId == input.BuildingId && x.ChargeType == input.ChargeType &&
            (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= start) &&
            (!end.HasValue || x.EffectiveFrom <= end.Value));
        if (overlaps) return Failure("Thời hạn biểu phí bị trùng với cấu hình hiện có.");

        var tariff = new FeeTariff
        {
            BuildingId = input.BuildingId,
            ChargeType = input.ChargeType,
            EffectiveFrom = start,
            EffectiveTo = end,
            MonthlyRatePerSquareMeter = input.MonthlyRatePerSquareMeter,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = actorUserId,
            Tiers = input.Tiers.OrderBy(x => x.TierOrder)
                .Select(x => new FeeTariffTier
                {
                    TierOrder = x.TierOrder,
                    UpperConsumption = x.UpperConsumption,
                    UnitRate = x.UnitRate
                }).ToList()
        };
        _context.FeeTariffs.Add(tariff);
        try
        {
            await _context.SaveChangesAsync();
            return new BillingOperationResult(true, null, null);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Không thể lưu biểu phí cho tòa nhà {BuildingId}", input.BuildingId);
            return Failure("Không thể lưu biểu phí. Kiểm tra dữ liệu và thử lại.");
        }
    }

    public async Task<BillingOperationResult> RecordReadingAsync(
        MeterReadingInput input,
        string actorUserId)
    {
        if (input.UtilityType is not (FeeChargeType.Electricity or FeeChargeType.Water))
            return Failure("Chỉ ghi nhận chỉ số điện hoặc nước.");
        if (input.BillingYear is < 2000 or > 2200 || input.BillingMonth is < 1 or > 12)
            return Failure("Kỳ ghi chỉ số không hợp lệ.");
        if (input.PreviousReading < 0 || input.CurrentReading < input.PreviousReading ||
            !FitsPrecision(input.PreviousReading, 4) || !FitsPrecision(input.CurrentReading, 4))
            return Failure("Chỉ số hiện tại phải lớn hơn hoặc bằng chỉ số trước và không âm.");
        if (!await _context.Apartments.AnyAsync(x => x.ApartmentId == input.ApartmentId))
            return Failure("Không tìm thấy căn hộ.");
        if (await _context.UtilityMeterReadings.AnyAsync(x =>
                x.ApartmentId == input.ApartmentId &&
                x.UtilityType == input.UtilityType &&
                x.BillingYear == input.BillingYear &&
                x.BillingMonth == input.BillingMonth))
            return Failure("Kỳ này đã có chỉ số. Không thể ghi trùng.");
        if (await _context.ApartmentInvoices.AnyAsync(x =>
                x.ApartmentId == input.ApartmentId &&
                x.BillingYear == input.BillingYear &&
                x.BillingMonth == input.BillingMonth &&
                x.Status != ApartmentInvoiceStatus.Void))
            return Failure("Không thể thay đổi chỉ số sau khi đã lập hóa đơn.");
        var precedingReading = await _context.UtilityMeterReadings
            .Where(x => x.ApartmentId == input.ApartmentId &&
                x.UtilityType == input.UtilityType &&
                (x.BillingYear < input.BillingYear ||
                 x.BillingYear == input.BillingYear && x.BillingMonth < input.BillingMonth))
            .OrderByDescending(x => x.BillingYear)
            .ThenByDescending(x => x.BillingMonth)
            .FirstOrDefaultAsync();
        if (precedingReading != null && input.PreviousReading != precedingReading.CurrentReading)
            return Failure("Chỉ số đầu kỳ phải khớp với chỉ số cuối kỳ gần nhất đã ghi.");
        var followingReading = await _context.UtilityMeterReadings
            .Where(x => x.ApartmentId == input.ApartmentId &&
                x.UtilityType == input.UtilityType &&
                (x.BillingYear > input.BillingYear ||
                 x.BillingYear == input.BillingYear && x.BillingMonth > input.BillingMonth))
            .OrderBy(x => x.BillingYear)
            .ThenBy(x => x.BillingMonth)
            .FirstOrDefaultAsync();
        if (followingReading != null && input.CurrentReading != followingReading.PreviousReading)
            return Failure("Chỉ số cuối kỳ phải khớp với chỉ số đầu kỳ gần nhất đã ghi cho kỳ sau.");

        _context.UtilityMeterReadings.Add(new UtilityMeterReading
        {
            ApartmentId = input.ApartmentId,
            UtilityType = input.UtilityType,
            BillingYear = input.BillingYear,
            BillingMonth = input.BillingMonth,
            PreviousReading = input.PreviousReading,
            CurrentReading = input.CurrentReading,
            ReadAtUtc = DateTime.UtcNow,
            RecordedByUserId = actorUserId
        });
        try
        {
            await _context.SaveChangesAsync();
            return new BillingOperationResult(true, null, null);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Không thể lưu chỉ số căn hộ {ApartmentId}", input.ApartmentId);
            return Failure("Không thể lưu chỉ số. Có thể chỉ số kỳ này vừa được ghi đồng thời.");
        }
    }

    public async Task<BillingOperationResult> GenerateDraftAsync(
        int apartmentId,
        int year,
        int month,
        DateTime dueDate,
        string actorUserId)
    {
        if (year is < 2000 or > 2200 || month is < 1 or > 12)
            return Failure("Kỳ hóa đơn không hợp lệ.");
        if (dueDate.Date < new DateTime(year, month, 1).AddMonths(1))
            return Failure("Hạn thanh toán phải từ ngày đầu tiên sau kỳ tính phí trở đi.");

        var existing = await _context.ApartmentInvoices.AnyAsync(x =>
            x.ApartmentId == apartmentId && x.BillingYear == year && x.BillingMonth == month);
        if (existing) return Failure("Căn hộ đã có hóa đơn trong kỳ này.");

        var periodStart = new DateTime(year, month, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);
        var apartment = await _context.Apartments
            .Include(x => x.Building)
            .FirstOrDefaultAsync(x => x.ApartmentId == apartmentId);
        if (apartment == null) return Failure("Không tìm thấy căn hộ.");

        var activeOwners = await _context.ApartmentResidents
            .Where(x => x.ApartmentId == apartmentId && x.IsOwner &&
                x.MoveInDate <= periodEnd && (!x.MoveOutDate.HasValue || x.MoveOutDate.Value >= periodEnd))
            .Select(x => x.Resident)
            .ToListAsync();
        if (activeOwners.Count == 0)
            return Failure("Không có chủ hộ đang cư trú tại thời điểm chốt kỳ.");
        if (activeOwners.Count > 1)
            return Failure("Căn hộ có nhiều Chủ hộ đang cư trú tại ngày chốt kỳ; cần chuẩn hóa dữ liệu trước khi lập hóa đơn.");
        var owner = activeOwners[0];

        var tariffs = await _context.FeeTariffs.Include(x => x.Tiers)
            .Where(x => x.BuildingId == apartment.BuildingId &&
                x.EffectiveFrom <= periodStart &&
                (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= periodEnd))
            .ToListAsync();
        FeeTariff? FindTariff(FeeChargeType type)
        {
            var matches = tariffs.Where(x => x.ChargeType == type).ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        var managementTariff = FindTariff(FeeChargeType.Management);
        var electricityTariff = FindTariff(FeeChargeType.Electricity);
        var waterTariff = FindTariff(FeeChargeType.Water);
        if (managementTariff == null || electricityTariff == null || waterTariff == null)
            return Failure("Cần có biểu phí quản lý, điện và nước hiệu lực trong toàn bộ kỳ tính phí.");

        var readings = await _context.UtilityMeterReadings.Where(x =>
                x.ApartmentId == apartmentId && x.BillingYear == year && x.BillingMonth == month)
            .ToListAsync();
        var electricityReading = readings.SingleOrDefault(x => x.UtilityType == FeeChargeType.Electricity);
        var waterReading = readings.SingleOrDefault(x => x.UtilityType == FeeChargeType.Water);
        if (electricityReading == null || waterReading == null)
            return Failure("Cần ghi đủ chỉ số điện và nước trước khi lập hóa đơn.");

        decimal managementAmount;
        decimal electricityAmount;
        decimal waterAmount;
        try
        {
            managementAmount = RoundCurrency(apartment.Area * managementTariff.MonthlyRatePerSquareMeter);
            electricityAmount = ProgressiveTariffCalculator.Calculate(
                electricityReading.CurrentReading - electricityReading.PreviousReading,
                electricityTariff.Tiers.ToList());
            waterAmount = ProgressiveTariffCalculator.Calculate(
                waterReading.CurrentReading - waterReading.PreviousReading,
                waterTariff.Tiers.ToList());
        }
        catch (ArgumentException exception)
        {
            return Failure($"Cấu hình biểu phí chưa hợp lệ: {exception.Message}");
        }
        catch (OverflowException)
        {
            return Failure("Giá trị tính phí vượt giới hạn số học cho phép.");
        }

        var invoice = new ApartmentInvoice
        {
            InvoiceCode = $"INV-{year:D4}{month:D2}-{apartmentId:D6}-{Guid.NewGuid():N}"[..40],
            ApartmentId = apartmentId,
            ResidentId = owner.ResidentId,
            BillingYear = year,
            BillingMonth = month,
            DueDate = dueDate.Date,
            ApartmentArea = apartment.Area,
            TotalAmount = managementAmount + electricityAmount + waterAmount,
            Status = ApartmentInvoiceStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = actorUserId,
            Lines =
            [
                new ApartmentInvoiceLine
                {
                    ChargeType = FeeChargeType.Management,
                    Description = "Phí quản lý theo diện tích (tính đủ tháng)",
                    Quantity = apartment.Area,
                    UnitRate = managementTariff.MonthlyRatePerSquareMeter,
                    Amount = managementAmount,
                    SourceTariffId = managementTariff.FeeTariffId,
                    RateBreakdown = $"{apartment.Area:N2} m² × {managementTariff.MonthlyRatePerSquareMeter:N4} VND/m²/tháng"
                },
                new ApartmentInvoiceLine
                {
                    ChargeType = FeeChargeType.Electricity,
                    Description = "Điện sinh hoạt theo bậc lũy tiến",
                    Quantity = electricityReading.CurrentReading - electricityReading.PreviousReading,
                    UnitRate = AverageRate(electricityAmount, electricityReading.CurrentReading - electricityReading.PreviousReading),
                    Amount = electricityAmount,
                    PreviousReading = electricityReading.PreviousReading,
                    CurrentReading = electricityReading.CurrentReading,
                    SourceTariffId = electricityTariff.FeeTariffId,
                    SourceMeterReadingId = electricityReading.UtilityMeterReadingId,
                    RateBreakdown = DescribeTierSchedule(electricityTariff.Tiers, "kWh")
                },
                new ApartmentInvoiceLine
                {
                    ChargeType = FeeChargeType.Water,
                    Description = "Nước sinh hoạt theo bậc lũy tiến",
                    Quantity = waterReading.CurrentReading - waterReading.PreviousReading,
                    UnitRate = AverageRate(waterAmount, waterReading.CurrentReading - waterReading.PreviousReading),
                    Amount = waterAmount,
                    PreviousReading = waterReading.PreviousReading,
                    CurrentReading = waterReading.CurrentReading,
                    SourceTariffId = waterTariff.FeeTariffId,
                    SourceMeterReadingId = waterReading.UtilityMeterReadingId,
                    RateBreakdown = DescribeTierSchedule(waterTariff.Tiers, "m³")
                }
            ]
        };
        if (invoice.TotalAmount > 9999999999999999.99m)
            return Failure("Tổng hóa đơn vượt giới hạn lưu trữ cho phép.");

        _context.ApartmentInvoices.Add(invoice);
        try
        {
            await _context.SaveChangesAsync();
            return new BillingOperationResult(true, null, invoice);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Không thể lập hóa đơn căn hộ {ApartmentId}, kỳ {Year}-{Month}", apartmentId, year, month);
            return Failure("Không thể lập hóa đơn. Có thể hóa đơn kỳ này vừa được tạo đồng thời.");
        }
    }

    public async Task<BillingOperationResult> IssueAsync(
        int invoiceId,
        string actorUserId,
        byte[] rowVersion)
    {
        var invoice = await _context.ApartmentInvoices
            .FirstOrDefaultAsync(x => x.ApartmentInvoiceId == invoiceId);
        if (invoice == null) return Failure("Không tìm thấy hóa đơn.");
        if (invoice.Status != ApartmentInvoiceStatus.Draft)
            return Failure("Chỉ có thể phát hành hóa đơn ở trạng thái nháp.");
        if (_context.Database.IsRelational() &&
            (rowVersion.Length == 0 || !invoice.RowVersion.AsSpan().SequenceEqual(rowVersion)))
            return Failure("Hóa đơn đã thay đổi đồng thời. Tải lại dữ liệu trước khi thử lại.");

        invoice.Status = ApartmentInvoiceStatus.Issued;
        invoice.IssuedAtUtc = DateTime.UtcNow;
        invoice.IssuedByUserId = actorUserId;
        if (_context.Database.IsRelational())
            _context.Entry(invoice).Property(x => x.RowVersion).OriginalValue = rowVersion;
        try
        {
            await _context.SaveChangesAsync();
            return new BillingOperationResult(true, null, invoice);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            _logger.LogInformation(exception, "Xung đột phát hành hóa đơn {InvoiceId}", invoiceId);
            return Failure("Hóa đơn đã thay đổi đồng thời. Tải lại dữ liệu trước khi thử lại.");
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Không thể phát hành hóa đơn {InvoiceId}", invoiceId);
            return Failure("Không thể phát hành hóa đơn. Vui lòng thử lại.");
        }
    }

    private IQueryable<ApartmentInvoice> InvoiceQuery() =>
        _context.ApartmentInvoices.AsNoTracking()
            .Include(x => x.Apartment).ThenInclude(x => x.Building)
            .Include(x => x.Resident).ThenInclude(x => x.User)
            .Include(x => x.Lines);

    private static string? ValidateTariff(FeeTariffInput input)
    {
        if (!Enum.IsDefined(input.ChargeType)) return "Loại biểu phí không hợp lệ.";
        if (input.BuildingId <= 0) return "Vui lòng chọn tòa nhà.";
        if (input.EffectiveFrom == default || input.EffectiveFrom.TimeOfDay != TimeSpan.Zero ||
            input.EffectiveTo.HasValue && input.EffectiveTo.Value.Date < input.EffectiveFrom.Date)
            return "Khoảng thời gian hiệu lực không hợp lệ.";
        if (input.MonthlyRatePerSquareMeter < 0 ||
            !FitsPrecision(input.MonthlyRatePerSquareMeter, 4))
            return "Đơn giá quản lý phải không âm và có tối đa 4 chữ số thập phân.";
        if (input.ChargeType == FeeChargeType.Management)
            return input.Tiers.Count == 0 ? null : "Biểu phí quản lý theo diện tích không dùng bậc tiêu thụ.";
        if (input.MonthlyRatePerSquareMeter != 0)
            return "Đơn giá theo diện tích chỉ áp dụng cho phí quản lý.";
        if (!ValidTiers(input.Tiers)) return "Các bậc giá phải liên tục, tăng dần, tối đa 20 bậc và chỉ bậc cuối được không giới hạn.";
        return null;
    }

    private static bool ValidTiers(IReadOnlyList<FeeTariffTierInput> tiers)
    {
        if (tiers.Count is 0 or > 20) return false;
        decimal previous = 0;
        for (var i = 0; i < tiers.Count; i++)
        {
            var tier = tiers[i];
            if (tier.TierOrder != i + 1 || tier.UnitRate < 0 ||
                !FitsPrecision(tier.UnitRate, 4)) return false;
            if (tier.UpperConsumption.HasValue)
            {
                if (tier.UpperConsumption.Value <= previous || i == tiers.Count - 1 ||
                    !FitsPrecision(tier.UpperConsumption.Value, 4)) return false;
                previous = tier.UpperConsumption.Value;
            }
            else if (i != tiers.Count - 1)
            {
                return false;
            }
        }
        return !tiers[^1].UpperConsumption.HasValue;
    }

    private static decimal AverageRate(decimal amount, decimal quantity) =>
        quantity == 0 ? 0 : decimal.Round(amount / quantity, 4, MidpointRounding.AwayFromZero);

    private static bool FitsPrecision(decimal value, int decimalPlaces) =>
        decimal.Round(value, decimalPlaces) == value;

    private static string DescribeTierSchedule(
        IEnumerable<FeeTariffTier> sourceTiers,
        string unit)
    {
        decimal lower = 0;
        var tiers = sourceTiers.OrderBy(x => x.TierOrder).Select(tier =>
        {
            var upper = tier.UpperConsumption;
            var interval = upper.HasValue ? $"{lower:N4}–{upper.Value:N4}" : $">{lower:N4}";
            lower = upper ?? lower;
            return $"{interval} {unit} @ {tier.UnitRate:N4} VND/{unit}";
        });
        return string.Join("; ", tiers);
    }

    private static decimal RoundCurrency(decimal amount) =>
        decimal.Round(amount, 0, MidpointRounding.AwayFromZero);

    private static BillingOperationResult Failure(string message) => new(false, message, null);
}
