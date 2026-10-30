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
        if (buildingId.HasValue && buildingId.Value <= 0)
            return Task.FromResult(new List<FeeTariff>());

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

    public Task<FeeTariff?> GetTariffAsync(int tariffId)
    {
        if (tariffId <= 0) return Task.FromResult<FeeTariff?>(null);
        return _context.FeeTariffs.AsNoTracking()
            .Include(x => x.Building)
            .Include(x => x.Tiers)
            .Include(x => x.CreatedByUser)
            .Include(x => x.InvoiceLines)
            .FirstOrDefaultAsync(x => x.FeeTariffId == tariffId);
    }

    public async Task<BillingOperationResult> CloseTariffAsync(
        int tariffId,
        DateTime effectiveTo,
        string actorUserId)
    {
        if (tariffId <= 0) return Failure("Không tìm thấy biểu phí.");
        if (string.IsNullOrWhiteSpace(actorUserId)) return Failure("Người thực hiện không hợp lệ.");

        var tariff = await _context.FeeTariffs.FirstOrDefaultAsync(x => x.FeeTariffId == tariffId);
        if (tariff == null) return Failure("Không tìm thấy biểu phí.");

        var closeDate = effectiveTo.Date;
        if (closeDate < tariff.EffectiveFrom.Date)
            return Failure("Ngày kết thúc hiệu lực phải từ ngày bắt đầu hiệu lực trở đi.");

        var overlaps = await _context.FeeTariffs.AnyAsync(x =>
            x.FeeTariffId != tariffId &&
            x.BuildingId == tariff.BuildingId &&
            x.ChargeType == tariff.ChargeType &&
            x.EffectiveFrom <= closeDate &&
            (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= tariff.EffectiveFrom.Date));
        if (overlaps)
            return Failure("Khoảng thời gian hiệu lực mới bị trùng lặp với biểu phí khác.");

        tariff.EffectiveTo = closeDate;
        try
        {
            await _context.SaveChangesAsync();
            return new BillingOperationResult(true, null, null);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Lỗi khi cập nhật thời hạn biểu phí {TariffId}", tariffId);
            return Failure("Không thể cập nhật biểu phí. Vui lòng thử lại.");
        }
    }

    public Task<List<ApartmentInvoice>> GetInvoicesAsync(InvoiceFilterCriteria? criteria = null)
    {
        var query = InvoiceQuery();
        if (criteria != null)
        {
            if (criteria.BuildingId.HasValue && criteria.BuildingId.Value > 0)
                query = query.Where(x => x.Apartment.BuildingId == criteria.BuildingId.Value);

            if (criteria.Year.HasValue && criteria.Year.Value > 0)
                query = query.Where(x => x.BillingYear == criteria.Year.Value);

            if (criteria.Month.HasValue && criteria.Month.Value > 0)
                query = query.Where(x => x.BillingMonth == criteria.Month.Value);

            if (criteria.Status.HasValue)
                query = query.Where(x => x.Status == criteria.Status.Value);

            if (criteria.IsPaid.HasValue)
            {
                if (criteria.IsPaid.Value)
                    query = query.Where(x => x.PaymentTransactions.Any(p => p.Status == PaymentTransactionStatus.Confirmed));
                else
                    query = query.Where(x => !x.PaymentTransactions.Any(p => p.Status == PaymentTransactionStatus.Confirmed));
            }

            if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
            {
                var term = criteria.SearchTerm.Trim().ToLower();
                query = query.Where(x =>
                    x.InvoiceCode.ToLower().Contains(term) ||
                    x.Apartment.ApartmentCode.ToLower().Contains(term) ||
                    (x.Resident.User != null && x.Resident.User.FullName != null && x.Resident.User.FullName.ToLower().Contains(term)));
            }
        }

        return query.OrderByDescending(x => x.BillingYear)
            .ThenByDescending(x => x.BillingMonth)
            .ThenBy(x => x.Apartment.ApartmentCode)
            .ToListAsync();
    }

    public async Task<BillingSummaryDto> GetBillingSummaryAsync(
        int? buildingId = null,
        int? year = null,
        int? month = null)
    {
        var query = _context.ApartmentInvoices.AsNoTracking();
        if (buildingId.HasValue && buildingId.Value > 0)
            query = query.Where(x => x.Apartment.BuildingId == buildingId.Value);
        if (year.HasValue && year.Value > 0)
            query = query.Where(x => x.BillingYear == year.Value);
        if (month.HasValue && month.Value > 0)
            query = query.Where(x => x.BillingMonth == month.Value);

        var list = await query
            .Select(x => new
            {
                x.ApartmentInvoiceId,
                x.Status,
                x.TotalAmount,
                ConfirmedAmount = x.PaymentTransactions
                    .Where(p => p.Status == PaymentTransactionStatus.Confirmed)
                    .Sum(p => (decimal?)p.Amount) ?? 0m
            })
            .ToListAsync();

        var totalInvoiced = list.Where(x => x.Status == ApartmentInvoiceStatus.Issued).Sum(x => x.TotalAmount);
        var totalCollected = list.Where(x => x.Status == ApartmentInvoiceStatus.Issued).Sum(x => x.ConfirmedAmount);
        var totalReceivable = Math.Max(0m, totalInvoiced - totalCollected);

        var draftCount = list.Count(x => x.Status == ApartmentInvoiceStatus.Draft);
        var issuedCount = list.Count(x => x.Status == ApartmentInvoiceStatus.Issued);
        var paidCount = list.Count(x => x.Status == ApartmentInvoiceStatus.Issued && x.ConfirmedAmount >= x.TotalAmount && x.TotalAmount > 0);
        var unpaidCount = list.Count(x => x.Status == ApartmentInvoiceStatus.Issued && x.ConfirmedAmount < x.TotalAmount);
        var voidCount = list.Count(x => x.Status == ApartmentInvoiceStatus.Void);

        return new BillingSummaryDto(
            TotalInvoiced: totalInvoiced,
            TotalCollected: totalCollected,
            TotalReceivable: totalReceivable,
            TotalInvoices: list.Count,
            DraftCount: draftCount,
            IssuedCount: issuedCount,
            PaidCount: paidCount,
            UnpaidCount: unpaidCount,
            VoidCount: voidCount);
    }


    public Task<List<ApartmentInvoice>> GetResidentInvoicesAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Task.FromResult(new List<ApartmentInvoice>());

        return InvoiceQuery().Where(x => x.Resident.UserId == userId &&
                x.Status == ApartmentInvoiceStatus.Issued)
            .OrderByDescending(x => x.BillingYear)
            .ThenByDescending(x => x.BillingMonth)
            .ToListAsync();
    }

    public Task<ApartmentInvoice?> GetResidentInvoiceAsync(string userId, int invoiceId)
    {
        if (invoiceId <= 0 || string.IsNullOrWhiteSpace(userId))
            return Task.FromResult<ApartmentInvoice?>(null);

        return InvoiceQuery().FirstOrDefaultAsync(x =>
            x.ApartmentInvoiceId == invoiceId &&
            x.Resident.UserId == userId &&
            x.Status == ApartmentInvoiceStatus.Issued);
    }

    public Task<ApartmentInvoice?> GetInvoiceAsync(int invoiceId)
    {
        if (invoiceId <= 0)
            return Task.FromResult<ApartmentInvoice?>(null);

        return InvoiceQuery().FirstOrDefaultAsync(x => x.ApartmentInvoiceId == invoiceId);
    }

    public async Task<BillingOperationResult> CreateTariffAsync(
        FeeTariffInput input,
        string actorUserId)
    {
        if (input == null) return Failure("Dữ liệu biểu phí không hợp lệ.");
        if (string.IsNullOrWhiteSpace(actorUserId)) return Failure("Người thực hiện không hợp lệ.");

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
            if (exception.InnerException?.Message.Contains("IX_FeeTariffs_BuildingId_ChargeType_EffectiveFrom", StringComparison.OrdinalIgnoreCase) == true ||
                exception.Message.Contains("IX_FeeTariffs_BuildingId_ChargeType_EffectiveFrom", StringComparison.OrdinalIgnoreCase))
            {
                return Failure("Thời hạn biểu phí bị trùng với cấu hình hiện có.");
            }
            return Failure("Không thể lưu biểu phí. Kiểm tra dữ liệu và thử lại.");
        }
    }

    public async Task<BillingOperationResult> RecordReadingAsync(
        MeterReadingInput input,
        string actorUserId)
    {
        if (input == null) return Failure("Dữ liệu chỉ số không hợp lệ.");
        if (string.IsNullOrWhiteSpace(actorUserId)) return Failure("Người thực hiện không hợp lệ.");
        if (input.ApartmentId <= 0) return Failure("Vui lòng chọn căn hộ.");
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
            if (exception.InnerException?.Message.Contains("IX_UtilityMeterReadings_ApartmentId_UtilityType_BillingYear_BillingMonth", StringComparison.OrdinalIgnoreCase) == true ||
                exception.Message.Contains("IX_UtilityMeterReadings_ApartmentId_UtilityType_BillingYear_BillingMonth", StringComparison.OrdinalIgnoreCase))
            {
                return Failure("Kỳ này đã có chỉ số. Không thể ghi trùng.");
            }
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
        if (apartmentId <= 0)
            return Failure("Vui lòng chọn căn hộ.");
        if (string.IsNullOrWhiteSpace(actorUserId))
            return Failure("Người thực hiện không hợp lệ.");
        if (year is < 2000 or > 2200 || month is < 1 or > 12)
            return Failure("Kỳ hóa đơn không hợp lệ.");
        if (dueDate.Date < new DateTime(year, month, 1).AddMonths(1))
            return Failure("Hạn thanh toán phải từ ngày đầu tiên sau kỳ tính phí trở đi.");

        var existing = await _context.ApartmentInvoices.AnyAsync(x =>
            x.ApartmentId == apartmentId && x.BillingYear == year && x.BillingMonth == month);
        if (existing) return Failure("Căn hộ đã có hóa đơn trong kỳ này.", BillingFailureReason.AlreadyExists);

        var periodStart = new DateTime(year, month, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);
        var apartment = await _context.Apartments
            .Include(x => x.Building)
            .FirstOrDefaultAsync(x => x.ApartmentId == apartmentId);
        if (apartment == null) return Failure("Không tìm thấy căn hộ.");
        if (apartment.Area <= 0) return Failure("Diện tích căn hộ phải lớn hơn 0 để tính phí quản lý.", BillingFailureReason.InvalidArea);

        var activeOwners = await _context.ApartmentResidents
            .Where(x => x.ApartmentId == apartmentId && x.IsOwner &&
                x.MoveInDate <= periodEnd && (!x.MoveOutDate.HasValue || x.MoveOutDate.Value >= periodEnd))
            .Select(x => x.Resident)
            .ToListAsync();
        if (activeOwners.Count == 0)
            return Failure("Không có chủ hộ đang cư trú tại thời điểm chốt kỳ.", BillingFailureReason.MissingOrInvalidOccupancy);
        if (activeOwners.Count > 1)
            return Failure("Căn hộ có nhiều Chủ hộ đang cư trú tại ngày chốt kỳ; cần chuẩn hóa dữ liệu trước khi lập hóa đơn.", BillingFailureReason.MissingOrInvalidOccupancy);
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
            return Failure("Cần có biểu phí quản lý, điện và nước hiệu lực trong toàn bộ kỳ tính phí.", BillingFailureReason.MissingTariff);

        var readings = await _context.UtilityMeterReadings.Where(x =>
                x.ApartmentId == apartmentId && x.BillingYear == year && x.BillingMonth == month)
            .ToListAsync();
        var electricityReading = readings.FirstOrDefault(x => x.UtilityType == FeeChargeType.Electricity);
        var waterReading = readings.FirstOrDefault(x => x.UtilityType == FeeChargeType.Water);
        if (electricityReading == null || waterReading == null)
            return Failure("Cần ghi đủ chỉ số điện và nước trước khi lập hóa đơn.", BillingFailureReason.MissingMeterReading);

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
            return Failure($"Cấu hình biểu phí chưa hợp lệ: {exception.Message}", BillingFailureReason.TariffCalculationError);
        }
        catch (OverflowException)
        {
            return Failure("Giá trị tính phí vượt giới hạn số học cho phép.", BillingFailureReason.TariffCalculationError);
        }

        var parkingResult = await CalculateApartmentParkingFeeAsync(
            apartment.ApartmentId,
            apartment.BuildingId,
            year,
            month);

        var managementDescription = "Phí quản lý theo diện tích (tính đủ tháng)";
        var managementRateBreakdown = $"{apartment.Area:N2} m² × {managementTariff.MonthlyRatePerSquareMeter:N4} VND/m²/tháng";

        if (parkingResult.TotalAmount > 0)
        {
            managementAmount += parkingResult.TotalAmount;
            managementDescription += $"\n[Bao gồm] {parkingResult.SummaryDescription}";
            managementRateBreakdown += $"; Phí gửi xe: {parkingResult.TotalAmount:N0} VND ({parkingResult.ActiveVehicleCount} phương tiện)";
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
                    Description = managementDescription,
                    Quantity = apartment.Area,
                    UnitRate = managementTariff.MonthlyRatePerSquareMeter,
                    Amount = managementAmount,
                    SourceTariffId = managementTariff.FeeTariffId,
                    RateBreakdown = managementRateBreakdown
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
            return Failure("Tổng hóa đơn vượt giới hạn lưu trữ cho phép.", BillingFailureReason.TariffCalculationError);

        _context.ApartmentInvoices.Add(invoice);
        try
        {
            await _context.SaveChangesAsync();
            return new BillingOperationResult(true, null, invoice);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Không thể lập hóa đơn căn hộ {ApartmentId}, kỳ {Year}-{Month}", apartmentId, year, month);
            if (exception.InnerException?.Message.Contains("IX_ApartmentInvoices_ApartmentId_BillingYear_BillingMonth", StringComparison.OrdinalIgnoreCase) == true ||
                exception.Message.Contains("IX_ApartmentInvoices_ApartmentId_BillingYear_BillingMonth", StringComparison.OrdinalIgnoreCase))
            {
                return Failure("Căn hộ đã có hóa đơn trong kỳ này.", BillingFailureReason.AlreadyExists);
            }
            return Failure("Không thể lưu hóa đơn do lỗi cơ sở dữ liệu.", BillingFailureReason.PersistenceError);
        }
    }

    public async Task<BatchBillingOperationResult> GenerateBuildingDraftsAsync(
        int buildingId,
        int year,
        int month,
        DateTime dueDate,
        string actorUserId)
    {
        if (buildingId <= 0)
            return new BatchBillingOperationResult(false, "Vui lòng chọn tòa nhà.", 0, 0, 0, 0, 0, []);
        if (string.IsNullOrWhiteSpace(actorUserId))
            return new BatchBillingOperationResult(false, "Người thực hiện không hợp lệ.", 0, 0, 0, 0, 0, []);
        if (year is < 2000 or > 2200 || month is < 1 or > 12)
            return new BatchBillingOperationResult(false, "Kỳ hóa đơn không hợp lệ.", 0, 0, 0, 0, 0, []);

        var periodStart = new DateTime(year, month, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);
        if (dueDate.Date < periodStart.AddMonths(1))
            return new BatchBillingOperationResult(false, "Hạn thanh toán phải từ ngày đầu tiên sau kỳ tính phí trở đi.", 0, 0, 0, 0, 0, []);

        var buildingExists = await _context.Buildings.AnyAsync(x => x.BuildingId == buildingId);
        if (!buildingExists)
            return new BatchBillingOperationResult(false, "Không tìm thấy tòa nhà.", 0, 0, 0, 0, 0, []);

        var tariffs = await _context.FeeTariffs.Include(x => x.Tiers)
            .Where(x => x.BuildingId == buildingId &&
                x.EffectiveFrom <= periodStart &&
                (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= periodEnd))
            .ToListAsync();

        FeeTariff? FindTariff(FeeChargeType type)
        {
            var matches = tariffs.Where(x => x.ChargeType == type).ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        if (FindTariff(FeeChargeType.Management) == null ||
            FindTariff(FeeChargeType.Electricity) == null ||
            FindTariff(FeeChargeType.Water) == null)
        {
            return new BatchBillingOperationResult(
                false,
                "Cần có biểu phí quản lý, điện và nước hiệu lực trong toàn bộ kỳ tính phí.",
                0, 0, 0, 0, 0, []);
        }

        var apartments = await _context.Apartments
            .Where(x => x.BuildingId == buildingId)
            .OrderBy(x => x.ApartmentCode)
            .ToListAsync();

        if (apartments.Count == 0)
        {
            return new BatchBillingOperationResult(true, null, 0, 0, 0, 0, 0, []);
        }

        var items = new List<BatchBillingItemResult>(apartments.Count);

        foreach (var apt in apartments)
        {
            var existingInvoice = await _context.ApartmentInvoices.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ApartmentId == apt.ApartmentId && x.BillingYear == year && x.BillingMonth == month);
            if (existingInvoice != null)
            {
                items.Add(new BatchBillingItemResult(
                    apt.ApartmentId,
                    apt.ApartmentCode,
                    BatchBillingItemStatus.AlreadyExists,
                    "Căn hộ đã có hóa đơn trong kỳ này.",
                    existingInvoice.ApartmentInvoiceId,
                    existingInvoice.InvoiceCode,
                    existingInvoice.TotalAmount));
                continue;
            }

            try
            {
                var draftResult = await GenerateDraftAsync(apt.ApartmentId, year, month, dueDate, actorUserId);
                if (draftResult.Success)
                {
                    items.Add(new BatchBillingItemResult(
                        apt.ApartmentId,
                        apt.ApartmentCode,
                        BatchBillingItemStatus.Created,
                        "Đã tạo hóa đơn nháp thành công.",
                        draftResult.Invoice!.ApartmentInvoiceId,
                        draftResult.Invoice.InvoiceCode,
                        draftResult.Invoice.TotalAmount));
                }
                else
                {
                    _context.ChangeTracker.Clear();
                    var error = draftResult.ErrorMessage ?? "Không thể lập hóa đơn.";

                    var isAlreadyExistsCandidate = draftResult.FailureReason == BillingFailureReason.AlreadyExists ||
                        string.Equals(error, "Căn hộ đã có hóa đơn trong kỳ này.", StringComparison.OrdinalIgnoreCase);

                    var isPersistenceErrorCandidate = draftResult.FailureReason == BillingFailureReason.PersistenceError;

                    if (isAlreadyExistsCandidate || isPersistenceErrorCandidate)
                    {
                        try
                        {
                            var recheckInvoice = await _context.ApartmentInvoices.AsNoTracking()
                                .FirstOrDefaultAsync(x => x.ApartmentId == apt.ApartmentId && x.BillingYear == year && x.BillingMonth == month);

                            if (recheckInvoice != null)
                            {
                                items.Add(new BatchBillingItemResult(
                                    apt.ApartmentId,
                                    apt.ApartmentCode,
                                    BatchBillingItemStatus.AlreadyExists,
                                    "Căn hộ đã có hóa đơn trong kỳ này.",
                                    recheckInvoice.ApartmentInvoiceId,
                                    recheckInvoice.InvoiceCode,
                                    recheckInvoice.TotalAmount));
                                continue;
                            }
                            else
                            {
                                items.Add(new BatchBillingItemResult(
                                    apt.ApartmentId,
                                    apt.ApartmentCode,
                                    BatchBillingItemStatus.Failed,
                                    error));
                                continue;
                            }
                        }
                        catch (Exception verifyEx)
                        {
                            _logger.LogError(verifyEx, "Lỗi khi xác minh hóa đơn tồn tại cho căn hộ {ApartmentId}, kỳ {Year}-{Month}", apt.ApartmentId, year, month);
                            items.Add(new BatchBillingItemResult(
                                apt.ApartmentId,
                                apt.ApartmentCode,
                                BatchBillingItemStatus.Failed,
                                "Không thể kiểm tra trạng thái hóa đơn do lỗi kết nối cơ sở dữ liệu."));
                            continue;
                        }
                    }

                    var isSkipped = draftResult.FailureReason is BillingFailureReason.MissingOrInvalidOccupancy
                        or BillingFailureReason.MissingMeterReading
                        or BillingFailureReason.InvalidArea ||
                        error is "Không có chủ hộ đang cư trú tại thời điểm chốt kỳ."
                            or "Căn hộ có nhiều Chủ hộ đang cư trú tại ngày chốt kỳ; cần chuẩn hóa dữ liệu trước khi lập hóa đơn."
                            or "Cần ghi đủ chỉ số điện và nước trước khi lập hóa đơn."
                            or "Diện tích căn hộ phải lớn hơn 0 để tính phí quản lý.";

                    var status = isSkipped ? BatchBillingItemStatus.Skipped : BatchBillingItemStatus.Failed;

                    items.Add(new BatchBillingItemResult(
                        apt.ApartmentId,
                        apt.ApartmentCode,
                        status,
                        error));
                }
            }
            catch (Exception ex)
            {
                _context.ChangeTracker.Clear();
                _logger.LogError(ex, "Lỗi bất ngờ khi sinh hóa đơn căn hộ {ApartmentId}", apt.ApartmentId);
                items.Add(new BatchBillingItemResult(
                    apt.ApartmentId,
                    apt.ApartmentCode,
                    BatchBillingItemStatus.Failed,
                    $"Lỗi xử lý: {ex.Message}"));
            }
        }

        var createdCount = items.Count(x => x.Status == BatchBillingItemStatus.Created);
        var alreadyExistsCount = items.Count(x => x.Status == BatchBillingItemStatus.AlreadyExists);
        var skippedCount = items.Count(x => x.Status == BatchBillingItemStatus.Skipped);
        var failedCount = items.Count(x => x.Status == BatchBillingItemStatus.Failed);

        return new BatchBillingOperationResult(
            Success: true,
            ErrorMessage: null,
            TotalScanned: apartments.Count,
            CreatedCount: createdCount,
            AlreadyExistsCount: alreadyExistsCount,
            SkippedCount: skippedCount,
            FailedCount: failedCount,
            Items: items);
    }

public async Task<BatchMeterReadingViewModel> GetBatchMeterReadingSheetAsync(
        int buildingId, int billingYear, int billingMonth)
    {
        var model = new BatchMeterReadingViewModel
        {
            BuildingId = buildingId,
            BillingYear = billingYear,
            BillingMonth = billingMonth,
            ReadingDate = DateTime.Today
        };

        if (buildingId <= 0)
        {
            return model;
        }

        var building = await _context.Buildings.AsNoTracking().FirstOrDefaultAsync(x => x.BuildingId == buildingId);
        if (building == null)
        {
            return model;
        }
        model.BuildingName = building.BuildingName;

        var apartments = await _context.Apartments.AsNoTracking()
            .Where(x => x.BuildingId == buildingId)
            .OrderBy(x => x.ApartmentCode)
            .ToListAsync();

        if (apartments.Count == 0)
        {
            return model;
        }

        var apartmentIds = apartments.Select(a => a.ApartmentId).ToList();

        var lockedApartmentIds = (await _context.ApartmentInvoices.AsNoTracking()
            .Where(x => apartmentIds.Contains(x.ApartmentId) &&
                        x.BillingYear == billingYear &&
                        x.BillingMonth == billingMonth &&
                        x.Status != ApartmentInvoiceStatus.Void)
            .Select(x => x.ApartmentId)
            .ToListAsync())
            .ToHashSet();

        var currentReadings = await _context.UtilityMeterReadings.AsNoTracking()
            .Where(x => apartmentIds.Contains(x.ApartmentId) &&
                        x.BillingYear == billingYear &&
                        x.BillingMonth == billingMonth)
            .ToListAsync();

        var precedingReadings = await _context.UtilityMeterReadings.AsNoTracking()
            .Where(x => apartmentIds.Contains(x.ApartmentId) &&
                        (x.BillingYear < billingYear ||
                         (x.BillingYear == billingYear && x.BillingMonth < billingMonth)))
            .ToListAsync();

        var precedingLookup = precedingReadings
            .GroupBy(x => (x.ApartmentId, x.UtilityType))
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.BillingYear).ThenByDescending(x => x.BillingMonth).First());

        var rows = new List<BatchMeterReadingRowItem>(apartments.Count);

        foreach (var apt in apartments)
        {
            var isLocked = lockedApartmentIds.Contains(apt.ApartmentId);
            var currElec = currentReadings.FirstOrDefault(x => x.ApartmentId == apt.ApartmentId && x.UtilityType == FeeChargeType.Electricity);
            var currWater = currentReadings.FirstOrDefault(x => x.ApartmentId == apt.ApartmentId && x.UtilityType == FeeChargeType.Water);

            precedingLookup.TryGetValue((apt.ApartmentId, FeeChargeType.Electricity), out var precElec);
            precedingLookup.TryGetValue((apt.ApartmentId, FeeChargeType.Water), out var precWater);

            decimal oldElec = currElec != null ? currElec.PreviousReading : (precElec?.CurrentReading ?? 0m);
            decimal oldWater = currWater != null ? currWater.PreviousReading : (precWater?.CurrentReading ?? 0m);

            var row = new BatchMeterReadingRowItem
            {
                ApartmentId = apt.ApartmentId,
                ApartmentCode = apt.ApartmentCode,
                ElectricityOldReading = oldElec,
                ElectricityNewReading = currElec?.CurrentReading,
                WaterOldReading = oldWater,
                WaterNewReading = currWater?.CurrentReading,
                HasExistingReading = currElec != null || currWater != null,
                IsInvoiceLocked = isLocked,
                StatusNote = isLocked ? "Đã có hóa đơn (khóa)" : (currElec != null || currWater != null ? "Đã có chỉ số" : "Chưa có chỉ số")
            };

            rows.Add(row);
        }

        model.Rows = rows;
        return model;
    }

    public async Task<BatchMeterReadingOperationResult> SaveBatchMeterReadingsAsync(
        BatchMeterReadingSaveInput input,
        string actorUserId)
    {
        if (input == null)
            return new BatchMeterReadingOperationResult(false, "Dữ liệu chỉ số không hợp lệ.", 0, 0, 0, 0, []);
        if (string.IsNullOrWhiteSpace(actorUserId))
            return new BatchMeterReadingOperationResult(false, "Người thực hiện không hợp lệ.", 0, 0, 0, 0, []);
        if (input.BuildingId <= 0)
            return new BatchMeterReadingOperationResult(false, "Vui lòng chọn tòa nhà.", 0, 0, 0, 0, []);
        if (input.BillingYear is < 2000 or > 2200 || input.BillingMonth is < 1 or > 12)
            return new BatchMeterReadingOperationResult(false, "Kỳ ghi chỉ số không hợp lệ.", 0, 0, 0, 0, []);

        if (input.Items == null || input.Items.Count == 0)
            return new BatchMeterReadingOperationResult(true, null, 0, 0, 0, 0, []);

        var items = new List<BatchMeterReadingItemResult>(input.Items.Count);
        int savedCount = 0;
        int skippedCount = 0;
        int failedCount = 0;

        foreach (var row in input.Items)
        {
            if (!row.ElectricityNewReading.HasValue && !row.WaterNewReading.HasValue)
            {
                items.Add(new BatchMeterReadingItemResult(row.ApartmentId, row.ApartmentCode, true, "Bỏ qua (chưa nhập chỉ số)"));
                skippedCount++;
                continue;
            }

            var isLocked = await _context.ApartmentInvoices.AnyAsync(x =>
                x.ApartmentId == row.ApartmentId &&
                x.BillingYear == input.BillingYear &&
                x.BillingMonth == input.BillingMonth &&
                x.Status != ApartmentInvoiceStatus.Void);

            if (isLocked)
            {
                items.Add(new BatchMeterReadingItemResult(row.ApartmentId, row.ApartmentCode, false, "Không thể thay đổi chỉ số sau khi đã lập hóa đơn."));
                failedCount++;
                continue;
            }

            var apartmentExists = await _context.Apartments.AnyAsync(x => x.ApartmentId == row.ApartmentId);
            if (!apartmentExists)
            {
                items.Add(new BatchMeterReadingItemResult(row.ApartmentId, row.ApartmentCode, false, "Không tìm thấy căn hộ."));
                failedCount++;
                continue;
            }

            if (row.ElectricityNewReading.HasValue)
            {
                var val = row.ElectricityNewReading.Value;
                if (val < 0 || !FitsPrecision(val, 4))
                {
                    items.Add(new BatchMeterReadingItemResult(row.ApartmentId, row.ApartmentCode, false, "Chỉ số điện không hợp lệ (không âm, tối đa 4 chữ số thập phân)."));
                    failedCount++;
                    continue;
                }
                if (val < row.ElectricityOldReading)
                {
                    items.Add(new BatchMeterReadingItemResult(row.ApartmentId, row.ApartmentCode, false, "Chỉ số điện mới phải lớn hơn hoặc bằng chỉ số cũ."));
                    failedCount++;
                    continue;
                }

                var following = await _context.UtilityMeterReadings
                    .Where(x => x.ApartmentId == row.ApartmentId &&
                                x.UtilityType == FeeChargeType.Electricity &&
                                (x.BillingYear > input.BillingYear ||
                                 (x.BillingYear == input.BillingYear && x.BillingMonth > input.BillingMonth)))
                    .OrderBy(x => x.BillingYear)
                    .ThenBy(x => x.BillingMonth)
                    .FirstOrDefaultAsync();
                if (following != null && val != following.PreviousReading)
                {
                    items.Add(new BatchMeterReadingItemResult(row.ApartmentId, row.ApartmentCode, false, "Chỉ số điện cuối kỳ phải khớp với chỉ số đầu kỳ đã ghi cho kỳ sau."));
                    failedCount++;
                    continue;
                }
            }

            if (row.WaterNewReading.HasValue)
            {
                var val = row.WaterNewReading.Value;
                if (val < 0 || !FitsPrecision(val, 4))
                {
                    items.Add(new BatchMeterReadingItemResult(row.ApartmentId, row.ApartmentCode, false, "Chỉ số nước không hợp lệ (không âm, tối đa 4 chữ số thập phân)."));
                    failedCount++;
                    continue;
                }
                if (val < row.WaterOldReading)
                {
                    items.Add(new BatchMeterReadingItemResult(row.ApartmentId, row.ApartmentCode, false, "Chỉ số nước mới phải lớn hơn hoặc bằng chỉ số cũ."));
                    failedCount++;
                    continue;
                }

                var following = await _context.UtilityMeterReadings
                    .Where(x => x.ApartmentId == row.ApartmentId &&
                                x.UtilityType == FeeChargeType.Water &&
                                (x.BillingYear > input.BillingYear ||
                                 (x.BillingYear == input.BillingYear && x.BillingMonth > input.BillingMonth)))
                    .OrderBy(x => x.BillingYear)
                    .ThenBy(x => x.BillingMonth)
                    .FirstOrDefaultAsync();
                if (following != null && val != following.PreviousReading)
                {
                    items.Add(new BatchMeterReadingItemResult(row.ApartmentId, row.ApartmentCode, false, "Chỉ số nước cuối kỳ phải khớp với chỉ số đầu kỳ đã ghi cho kỳ sau."));
                    failedCount++;
                    continue;
                }
            }

            try
            {
                if (row.ElectricityNewReading.HasValue)
                {
                    var existingElec = await _context.UtilityMeterReadings.FirstOrDefaultAsync(x =>
                        x.ApartmentId == row.ApartmentId &&
                        x.UtilityType == FeeChargeType.Electricity &&
                        x.BillingYear == input.BillingYear &&
                        x.BillingMonth == input.BillingMonth);

                    if (existingElec != null)
                    {
                        existingElec.PreviousReading = row.ElectricityOldReading;
                        existingElec.CurrentReading = row.ElectricityNewReading.Value;
                        existingElec.ReadAtUtc = DateTime.UtcNow;
                        existingElec.RecordedByUserId = actorUserId;
                    }
                    else
                    {
                        _context.UtilityMeterReadings.Add(new UtilityMeterReading
                        {
                            ApartmentId = row.ApartmentId,
                            UtilityType = FeeChargeType.Electricity,
                            BillingYear = input.BillingYear,
                            BillingMonth = input.BillingMonth,
                            PreviousReading = row.ElectricityOldReading,
                            CurrentReading = row.ElectricityNewReading.Value,
                            ReadAtUtc = DateTime.UtcNow,
                            RecordedByUserId = actorUserId
                        });
                    }
                }

                if (row.WaterNewReading.HasValue)
                {
                    var existingWater = await _context.UtilityMeterReadings.FirstOrDefaultAsync(x =>
                        x.ApartmentId == row.ApartmentId &&
                        x.UtilityType == FeeChargeType.Water &&
                        x.BillingYear == input.BillingYear &&
                        x.BillingMonth == input.BillingMonth);

                    if (existingWater != null)
                    {
                        existingWater.PreviousReading = row.WaterOldReading;
                        existingWater.CurrentReading = row.WaterNewReading.Value;
                        existingWater.ReadAtUtc = DateTime.UtcNow;
                        existingWater.RecordedByUserId = actorUserId;
                    }
                    else
                    {
                        _context.UtilityMeterReadings.Add(new UtilityMeterReading
                        {
                            ApartmentId = row.ApartmentId,
                            UtilityType = FeeChargeType.Water,
                            BillingYear = input.BillingYear,
                            BillingMonth = input.BillingMonth,
                            PreviousReading = row.WaterOldReading,
                            CurrentReading = row.WaterNewReading.Value,
                            ReadAtUtc = DateTime.UtcNow,
                            RecordedByUserId = actorUserId
                        });
                    }
                }

                await _context.SaveChangesAsync();
                items.Add(new BatchMeterReadingItemResult(row.ApartmentId, row.ApartmentCode, true, "Đã lưu chỉ số thành công."));
                savedCount++;
            }
            catch (DbUpdateException ex)
            {
                _context.ChangeTracker.Clear();
                _logger.LogWarning(ex, "DbUpdateException when saving batch meter reading for apt {ApartmentId}", row.ApartmentId);
                items.Add(new BatchMeterReadingItemResult(row.ApartmentId, row.ApartmentCode, false, "Lỗi cơ sở dữ liệu khi lưu chỉ số."));
                failedCount++;
            }
            catch (Exception ex)
            {
                _context.ChangeTracker.Clear();
                _logger.LogError(ex, "Unexpected error saving batch meter reading for apt {ApartmentId}", row.ApartmentId);
                items.Add(new BatchMeterReadingItemResult(row.ApartmentId, row.ApartmentCode, false, $"Lỗi phát sinh: {ex.Message}"));
                failedCount++;
            }
        }

        return new BatchMeterReadingOperationResult(
            Success: true,
            ErrorMessage: null,
            TotalProcessed: input.Items.Count,
            SavedCount: savedCount,
            SkippedCount: skippedCount,
            FailedCount: failedCount,
            Items: items);
    }

        public async Task<BillingOperationResult> IssueAsync(
        int invoiceId,
        string actorUserId,
        byte[] rowVersion)
    {
        if (invoiceId <= 0) return Failure("Không tìm thấy hóa đơn.");
        if (string.IsNullOrWhiteSpace(actorUserId)) return Failure("Người thực hiện không hợp lệ.");

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

    public async Task<List<DraftInvoiceDto>> GetDraftInvoicesForBatchIssueAsync(int buildingId, int year, int month)
    {
        if (buildingId <= 0 || year is < 2000 or > 2200 || month is < 1 or > 12)
            return [];

        var invoices = await _context.ApartmentInvoices.AsNoTracking()
            .Include(x => x.Apartment)
            .Include(x => x.Resident).ThenInclude(r => r.User)
            .Include(x => x.Lines)
            .Where(x => x.Apartment.BuildingId == buildingId &&
                        x.BillingYear == year &&
                        x.BillingMonth == month &&
                        x.Status == ApartmentInvoiceStatus.Draft)
            .OrderBy(x => x.Apartment.ApartmentCode)
            .ToListAsync();

        return invoices.Select(inv =>
        {
            var mgmtFee = inv.Lines.FirstOrDefault(l => l.ChargeType == FeeChargeType.Management)?.Amount ?? 0m;
            var elecFee = inv.Lines.FirstOrDefault(l => l.ChargeType == FeeChargeType.Electricity)?.Amount ?? 0m;
            var waterFee = inv.Lines.FirstOrDefault(l => l.ChargeType == FeeChargeType.Water)?.Amount ?? 0m;
            var residentName = inv.Resident?.User?.FullName ?? inv.Resident?.CitizenId ?? "Chủ hộ";
            var token = Convert.ToBase64String(inv.RowVersion);

            return new DraftInvoiceDto(
                inv.ApartmentInvoiceId,
                inv.InvoiceCode,
                inv.Apartment?.ApartmentCode ?? string.Empty,
                residentName,
                mgmtFee,
                elecFee,
                waterFee,
                inv.TotalAmount,
                inv.RowVersion,
                token);
        }).ToList();
    }

    public async Task<BatchIssueOperationResult> IssueBuildingInvoicesAsync(BatchIssueInput input, string actorUserId)
    {
        if (input == null || input.Invoices.Count == 0)
            return new BatchIssueOperationResult(false, "Vui lòng chọn ít nhất một hóa đơn để phát hành.", 0, 0, 0, 0, []);

        if (string.IsNullOrWhiteSpace(actorUserId))
            return new BatchIssueOperationResult(false, "Người thực hiện không hợp lệ.", 0, 0, 0, 0, []);

        var items = new List<BatchIssueItemResult>(input.Invoices.Count);
        int issuedCount = 0;
        int alreadyIssuedCount = 0;
        int failedCount = 0;

        foreach (var item in input.Invoices)
        {
            var invoice = await _context.ApartmentInvoices
                .Include(x => x.Apartment)
                .FirstOrDefaultAsync(x => x.ApartmentInvoiceId == item.InvoiceId);

            if (invoice == null)
            {
                items.Add(new BatchIssueItemResult(
                    item.InvoiceId,
                    $"#INV-{item.InvoiceId}",
                    "—",
                    BatchIssueItemStatus.Failed,
                    "Không tìm thấy hóa đơn trong hệ thống.",
                    0));
                failedCount++;
                continue;
            }

            var aptCode = invoice.Apartment?.ApartmentCode ?? string.Empty;
            var invCode = invoice.InvoiceCode;
            var totalAmount = invoice.TotalAmount;

            if (invoice.Status == ApartmentInvoiceStatus.Issued)
            {
                items.Add(new BatchIssueItemResult(
                    invoice.ApartmentInvoiceId,
                    invCode,
                    aptCode,
                    BatchIssueItemStatus.AlreadyIssued,
                    "Hóa đơn đã được phát hành trước đó.",
                    totalAmount));
                alreadyIssuedCount++;
                continue;
            }

            if (invoice.Status != ApartmentInvoiceStatus.Draft)
            {
                items.Add(new BatchIssueItemResult(
                    invoice.ApartmentInvoiceId,
                    invCode,
                    aptCode,
                    BatchIssueItemStatus.InvalidStatus,
                    $"Không thể phát hành hóa đơn ở trạng thái {invoice.Status}.",
                    totalAmount));
                failedCount++;
                continue;
            }

            if (_context.Database.IsRelational() &&
                item.RowVersion != null &&
                item.RowVersion.Length > 0 &&
                !invoice.RowVersion.AsSpan().SequenceEqual(item.RowVersion))
            {
                _context.ChangeTracker.Clear();
                items.Add(new BatchIssueItemResult(
                    invoice.ApartmentInvoiceId,
                    invCode,
                    aptCode,
                    BatchIssueItemStatus.ConcurrencyConflict,
                    "Hóa đơn đã bị sửa đổi đồng thời bởi thao tác khác. Vui lòng tải lại.",
                    totalAmount));
                failedCount++;
                continue;
            }

            invoice.Status = ApartmentInvoiceStatus.Issued;
            invoice.IssuedAtUtc = DateTime.UtcNow;
            invoice.IssuedByUserId = actorUserId;

            if (_context.Database.IsRelational() && item.RowVersion != null && item.RowVersion.Length > 0)
            {
                _context.Entry(invoice).Property(x => x.RowVersion).OriginalValue = item.RowVersion;
            }

            try
            {
                await _context.SaveChangesAsync();
                items.Add(new BatchIssueItemResult(
                    invoice.ApartmentInvoiceId,
                    invCode,
                    aptCode,
                    BatchIssueItemStatus.Issued,
                    "Đã phát hành hóa đơn thành công.",
                    totalAmount));
                issuedCount++;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _context.ChangeTracker.Clear();
                _logger.LogInformation(ex, "Xung đột đồng thời khi phát hành hóa đơn {InvoiceId}", invoice.ApartmentInvoiceId);
                items.Add(new BatchIssueItemResult(
                    invoice.ApartmentInvoiceId,
                    invCode,
                    aptCode,
                    BatchIssueItemStatus.ConcurrencyConflict,
                    "Hóa đơn đã bị thay đổi đồng thời. Vui lòng thử lại.",
                    totalAmount));
                failedCount++;
            }
            catch (Exception ex)
            {
                _context.ChangeTracker.Clear();
                _logger.LogError(ex, "Lỗi khi phát hành hóa đơn {InvoiceId}", invoice.ApartmentInvoiceId);
                items.Add(new BatchIssueItemResult(
                    invoice.ApartmentInvoiceId,
                    invCode,
                    aptCode,
                    BatchIssueItemStatus.Failed,
                    $"Lỗi lưu dữ liệu: {ex.Message}",
                    totalAmount));
                failedCount++;
            }
        }

        return new BatchIssueOperationResult(
            Success: true,
            ErrorMessage: null,
            TotalSelected: input.Invoices.Count,
            IssuedCount: issuedCount,
            AlreadyIssuedCount: alreadyIssuedCount,
            FailedCount: failedCount,
            Items: items);
    }

    public async Task<BillingOperationResult> VoidAsync(
        int invoiceId,
        string actorUserId,
        byte[] rowVersion,
        string? reason = null)
    {
        if (invoiceId <= 0) return Failure("Không tìm thấy hóa đơn.");
        if (string.IsNullOrWhiteSpace(actorUserId)) return Failure("Người thực hiện không hợp lệ.");

        var invoice = await _context.ApartmentInvoices
            .Include(x => x.PaymentTransactions)
            .FirstOrDefaultAsync(x => x.ApartmentInvoiceId == invoiceId);
        if (invoice == null) return Failure("Không tìm thấy hóa đơn.");
        if (invoice.Status == ApartmentInvoiceStatus.Void)
            return Failure("Hóa đơn đã ở trạng thái hủy.");

        var hasConfirmedPayment = invoice.PaymentTransactions.Any(x => x.Status == PaymentTransactionStatus.Confirmed);
        if (hasConfirmedPayment)
            return Failure("Hóa đơn đã có giao dịch thanh toán được xác nhận. Không thể hủy hóa đơn đã hoàn tất thanh toán.");

        if (_context.Database.IsRelational() &&
            (rowVersion.Length == 0 || !invoice.RowVersion.AsSpan().SequenceEqual(rowVersion)))
            return Failure("Hóa đơn đã thay đổi đồng thời. Tải lại dữ liệu trước khi thử lại.");

        var pendingPayments = invoice.PaymentTransactions.Where(x => x.Status == PaymentTransactionStatus.Pending).ToList();
        if (pendingPayments.Count > 0)
        {
            _context.PaymentTransactions.RemoveRange(pendingPayments);
        }

        invoice.Status = ApartmentInvoiceStatus.Void;
        if (_context.Database.IsRelational())
            _context.Entry(invoice).Property(x => x.RowVersion).OriginalValue = rowVersion;

        try
        {
            await _context.SaveChangesAsync();
            return new BillingOperationResult(true, null, invoice);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            _logger.LogInformation(exception, "Xung đột khi hủy hóa đơn {InvoiceId}", invoiceId);
            return Failure("Hóa đơn đã thay đổi đồng thời. Tải lại dữ liệu trước khi thử lại.");
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Không thể hủy hóa đơn {InvoiceId}", invoiceId);
            return Failure("Không thể hủy hóa đơn. Vui lòng kiểm tra ràng buộc dữ liệu và thử lại.");
        }
    }

    private IQueryable<ApartmentInvoice> InvoiceQuery() =>
        _context.ApartmentInvoices.AsNoTracking()
            .Include(x => x.Apartment).ThenInclude(x => x.Building)
            .Include(x => x.Resident).ThenInclude(x => x.User)
            .Include(x => x.Lines)
            .Include(x => x.PaymentTransactions).ThenInclude(x => x.ConfirmedByUser);


    /// <summary>
    /// Tính toán chi phí gửi xe của căn hộ trong kỳ hóa đơn xác định.
    /// </summary>
    public async Task<ParkingFeeCalculationResult> CalculateApartmentParkingFeeAsync(
        int apartmentId,
        int buildingId,
        int billingYear,
        int billingMonth,
        CancellationToken cancellationToken = default)
    {
        var periodStart = new DateTime(billingYear, billingMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1).AddTicks(-1);

        // 1. Quét danh sách xe hợp lệ của căn hộ trong kỳ tính phí
        // Điều kiện: Xe thuộc căn hộ, đã được duyệt và chưa bị hủy trước ngày đầu kỳ
        var vehicles = await _context.Vehicles
            .AsNoTracking()
            .Include(v => v.ParkingCard)
            .Where(v => v.ApartmentId == apartmentId
                     && (v.Status == "Đã duyệt" || (v.ParkingCard != null && v.ParkingCard.Status == "Active"))
                     && v.CreatedAt <= periodEnd)
            .ToListAsync(cancellationToken);

        if (vehicles.Count == 0)
        {
            return new ParkingFeeCalculationResult(
                TotalAmount: 0m,
                ActiveVehicleCount: 0,
                SummaryDescription: "Không có phương tiện đăng ký",
                Items: Array.Empty<VehicleParkingChargeItem>());
        }

        // 2. Tra cứu đơn giá gửi xe theo biểu phí hoặc cấu hình mặc định theo phân loại xe
        // (Áp dụng đơn giá chuẩn đô thị: Xe máy: 100k, Ô tô: 1.200k, Xe đạp/điện: 50k nếu chưa có bảng tariff riêng)
        var items = new List<VehicleParkingChargeItem>();
        decimal totalAmount = 0m;

        foreach (var vehicle in vehicles)
        {
            var vehicleType = string.IsNullOrWhiteSpace(vehicle.VehicleType)
                ? "Xe máy"
                : vehicle.VehicleType.Trim();

            decimal monthlyRate = vehicleType.ToLowerInvariant() switch
            {
                var t when t.Contains("ô tô") || t.Contains("oto") || t.Contains("car") => 1_200_000m,
                var t when t.Contains("đạp") || t.Contains("bicycle") => 50_000m,
                _ => 100_000m // Mặc định là xe máy
            };

            totalAmount += monthlyRate;
            items.Add(new VehicleParkingChargeItem(
                vehicle.VehicleId,
                vehicle.LicensePlate,
                vehicleType,
                monthlyRate));
        }

        // 3. Gom nhóm tạo diễn giải tóm tắt (Ví dụ: "1 Ô tô (1.200.000 ₫), 2 Xe máy (200.000 ₫)")
        var summaryParts = items
            .GroupBy(i => i.VehicleType)
            .Select(g => $"{g.Count()} {g.Key} ({g.Sum(x => x.MonthlyRate):N0} ₫)");

        var summaryDescription = $"Phí gửi xe tháng {billingMonth:D2}/{billingYear}: {string.Join(", ", summaryParts)}";

        return new ParkingFeeCalculationResult(
            TotalAmount: Math.Round(totalAmount, 0, MidpointRounding.AwayFromZero),
            ActiveVehicleCount: items.Count,
            SummaryDescription: summaryDescription,
            Items: items);
    }

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

    private static BillingOperationResult Failure(
        string message,
        BillingFailureReason reason = BillingFailureReason.InvalidInput) =>
        new(false, message, null, reason);
}
