using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Data;
using ApartmentManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Services;

public sealed class FinancialReportService : IFinancialReportService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public FinancialReportService(
        ApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<MonthlyStatementReportDto> GenerateMonthlyStatementAsync(
        MonthlyStatementFilterInput filter,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ApartmentInvoices
            .AsNoTracking()
            .Include(i => i.Apartment)
                .ThenInclude(a => a.Building)
            .Include(i => i.Resident)
                .ThenInclude(r => r.User)
            .Include(i => i.Lines)
            .Include(i => i.PaymentTransactions)
            .AsQueryable();

        if (filter.BuildingId.HasValue)
        {
            query = query.Where(i => i.Apartment.BuildingId == filter.BuildingId.Value);
        }

        if (filter.BillingYear.HasValue)
        {
            query = query.Where(i => i.BillingYear == filter.BillingYear.Value);
        }

        if (filter.BillingMonth.HasValue)
        {
            query = query.Where(i => i.BillingMonth == filter.BillingMonth.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(i => i.Status == filter.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            query = query.Where(i =>
                i.InvoiceCode.Contains(term) ||
                i.Apartment.ApartmentCode.Contains(term) ||
                (i.Resident.User != null && i.Resident.User.FullName != null && i.Resident.User.FullName.Contains(term)) ||
                i.Resident.CitizenId.Contains(term));
        }

        query = query
            .OrderBy(i => i.Apartment.Building != null ? i.Apartment.Building.BuildingName : string.Empty)
            .ThenBy(i => i.Apartment.Floor)
            .ThenBy(i => i.Apartment.ApartmentCode);

        var invoices = await query.ToListAsync(cancellationToken);

        // Lọc paymentStatus trong bộ nhớ dựa trên tổng giao dịch thanh toán xác nhận
        if (!string.IsNullOrWhiteSpace(filter.PaymentStatus))
        {
            if (string.Equals(filter.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                invoices = invoices.Where(i =>
                {
                    var paid = i.PaymentTransactions
                        .Where(t => t.Status == PaymentTransactionStatus.Confirmed)
                        .Sum(t => t.Amount);
                    return paid >= i.TotalAmount && i.TotalAmount > 0;
                }).ToList();
            }
            else if (string.Equals(filter.PaymentStatus, "Unpaid", StringComparison.OrdinalIgnoreCase))
            {
                invoices = invoices.Where(i =>
                {
                    var paid = i.PaymentTransactions
                        .Where(t => t.Status == PaymentTransactionStatus.Confirmed)
                        .Sum(t => t.Amount);
                    return paid < i.TotalAmount;
                }).ToList();
            }
        }

        // Xác định tên tòa nhà phục vụ tiêu đề
        string buildingName = "Tất cả tòa nhà";
        if (filter.BuildingId.HasValue)
        {
            var b = await _context.Buildings
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.BuildingId == filter.BuildingId.Value, cancellationToken);
            if (b != null)
            {
                buildingName = b.BuildingName;
            }
        }

        // Xác định chuỗi kỳ thanh toán
        string periodText = "Tất cả các kỳ";
        if (filter.BillingMonth.HasValue && filter.BillingYear.HasValue)
        {
            periodText = $"Tháng {filter.BillingMonth.Value:D2}/{filter.BillingYear.Value}";
        }
        else if (filter.BillingYear.HasValue)
        {
            periodText = $"Năm {filter.BillingYear.Value}";
        }
        else if (filter.BillingMonth.HasValue)
        {
            periodText = $"Tháng {filter.BillingMonth.Value:D2}";
        }

        var report = new MonthlyStatementReportDto
        {
            BuildingFilterName = buildingName,
            BillingPeriodText = periodText,
            BillingYear = filter.BillingYear,
            BillingMonth = filter.BillingMonth,
            GeneratedAtLocal = DateTime.Now,
            GeneratedBy = _currentUserService.UserId ?? "Ban Quản Lý"
        };

        int stt = 1;
        foreach (var inv in invoices)
        {
            var item = MapToStatementItem(stt++, inv);
            report.Items.Add(item);
        }

        return report;
    }

    public byte[] ExportMonthlyStatementToCsv(MonthlyStatementReportDto report)
    {
        var sb = new StringBuilder();

        // 1. Khối Header Báo cáo
        sb.AppendLine(EscapeCsv(report.ReportTitle));
        sb.AppendLine($"Tòa nhà:,{EscapeCsv(report.BuildingFilterName)}");
        sb.AppendLine($"Kỳ thanh toán:,{EscapeCsv(report.BillingPeriodText)}");
        sb.AppendLine($"Thời điểm xuất:,{report.GeneratedAtLocal:dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine($"Người lập báo cáo:,{EscapeCsv(report.GeneratedBy)}");
        sb.AppendLine($"Tổng số căn hộ lập hóa đơn:,{report.TotalInvoices}");
        sb.AppendLine(); // Dòng trống ngăn cách

        // 2. Header bảng dữ liệu
        sb.AppendLine(string.Join(",", new[]
        {
            "STT",
            "Mã hóa đơn",
            "Mã căn hộ",
            "Tòa nhà",
            "Tầng",
            "Chủ hộ / Cư dân",
            "Số điện thoại",
            "Diện tích (m2)",
            "Phí quản lý (VND)",
            "Phí gửi xe (VND)",
            "Tiền điện (VND)",
            "Tiền nước (VND)",
            "Phí khác (VND)",
            "Tổng phải nộp (VND)",
            "Đã thanh toán (VND)",
            "Còn nợ (VND)",
            "Trạng thái hóa đơn",
            "Tình trạng thanh toán",
            "Ngày thanh toán",
            "Hạn nộp"
        }.Select(EscapeCsv)));

        // 3. Dữ liệu từng căn hộ
        foreach (var it in report.Items)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                it.Stt.ToString(),
                EscapeCsv(it.InvoiceCode),
                EscapeCsv(it.ApartmentCode),
                EscapeCsv(it.BuildingName),
                it.FloorNumber.ToString(),
                EscapeCsv(it.ResidentName),
                EscapeCsv(it.PhoneNumber),
                it.ApartmentArea.ToString("0.##", CultureInfo.InvariantCulture),
                Math.Round(it.PureManagementFee, 0).ToString("0"),
                Math.Round(it.ParkingFee, 0).ToString("0"),
                Math.Round(it.ElectricityFee, 0).ToString("0"),
                Math.Round(it.WaterFee, 0).ToString("0"),
                Math.Round(it.OtherFee, 0).ToString("0"),
                Math.Round(it.TotalAmount, 0).ToString("0"),
                Math.Round(it.PaidAmount, 0).ToString("0"),
                Math.Round(it.OutstandingDebt, 0).ToString("0"),
                EscapeCsv(it.InvoiceStatus),
                EscapeCsv(it.PaymentStatus),
                it.LastPaymentDate.HasValue ? it.LastPaymentDate.Value.ToString("dd/MM/yyyy") : string.Empty,
                it.DueDate.ToString("dd/MM/yyyy")
            }));
        }

        // 4. Dòng Tổng cộng
        var totalArea = report.Items.Sum(x => x.ApartmentArea);
        sb.AppendLine(string.Join(",", new[]
        {
            "TỔNG CỘNG",
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            totalArea.ToString("0.##", CultureInfo.InvariantCulture),
            Math.Round(report.TotalPureManagementFee, 0).ToString("0"),
            Math.Round(report.TotalParkingFee, 0).ToString("0"),
            Math.Round(report.TotalElectricityFee, 0).ToString("0"),
            Math.Round(report.TotalWaterFee, 0).ToString("0"),
            Math.Round(report.TotalOtherFee, 0).ToString("0"),
            Math.Round(report.GrandTotalAmount, 0).ToString("0"),
            Math.Round(report.GrandTotalPaidAmount, 0).ToString("0"),
            Math.Round(report.GrandTotalOutstandingDebt, 0).ToString("0"),
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty
        }));

        // Sử dụng UTF-8 có BOM (0xEF, 0xBB, 0xBF) để Excel nhận diện chuẩn tiếng Việt
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        var preamble = encoding.GetPreamble();
        var contentBytes = encoding.GetBytes(sb.ToString());

        var result = new byte[preamble.Length + contentBytes.Length];
        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(contentBytes, 0, result, preamble.Length, contentBytes.Length);
        return result;
    }

    public string GetCsvFileName(MonthlyStatementReportDto report)
    {
        var cleanBuilding = SanitizeFileName(report.BuildingFilterName);
        var period = report.BillingMonth.HasValue && report.BillingYear.HasValue
            ? $"{report.BillingMonth.Value:D2}_{report.BillingYear.Value}"
            : (report.BillingYear.HasValue ? $"{report.BillingYear.Value}" : "ToanBo");

        var date = report.GeneratedAtLocal.ToString("yyyyMMdd_HHmm");
        return $"BangKeCongNo_{cleanBuilding}_{period}_{date}.csv";
    }

    private static MonthlyStatementItemDto MapToStatementItem(int stt, ApartmentInvoice inv)
    {
        // 1. Tiền điện & tiền nước
        decimal electricityFee = inv.Lines
            .Where(l => l.ChargeType == FeeChargeType.Electricity)
            .Sum(l => l.Amount);

        decimal waterFee = inv.Lines
            .Where(l => l.ChargeType == FeeChargeType.Water)
            .Sum(l => l.Amount);

        // 2. Bóc tách phí quản lý và phí gửi xe
        decimal pureManagementFee = 0m;
        decimal parkingFee = 0m;

        var managementLine = inv.Lines.FirstOrDefault(l => l.ChargeType == FeeChargeType.Management);
        if (managementLine != null)
        {
            // Kiểm tra xem dòng phí quản lý có gộp phí gửi xe không
            // Trong BillingService, khi có phương tiện:
            // managementAmount = expectedAreaAmount + parkingResult.TotalAmount
            // RateBreakdown chứa "; Phí gửi xe: {Amount} VND ({Count} phương tiện)"
            decimal expectedAreaAmount = Math.Round(managementLine.Quantity * managementLine.UnitRate, 0, MidpointRounding.AwayFromZero);

            if (managementLine.Amount > expectedAreaAmount && expectedAreaAmount > 0)
            {
                pureManagementFee = expectedAreaAmount;
                parkingFee = managementLine.Amount - expectedAreaAmount;
            }
            else
            {
                // Thử bóc tách từ breakdown nếu có pattern regex
                var parkingMatch = Regex.Match(managementLine.RateBreakdown ?? string.Empty, @"Phí gửi xe:\s*([\d\.,]+)\s*VND", RegexOptions.IgnoreCase);
                if (parkingMatch.Success && decimal.TryParse(parkingMatch.Groups[1].Value.Replace(".", "").Replace(",", ""), out var parsedParking) && parsedParking > 0 && parsedParking <= managementLine.Amount)
                {
                    parkingFee = parsedParking;
                    pureManagementFee = managementLine.Amount - parsedParking;
                }
                else
                {
                    pureManagementFee = managementLine.Amount;
                    parkingFee = 0m;
                }
            }
        }

        // 3. Phí dịch vụ khác (nếu có các line khác ngoài 3 loại trên)
        decimal otherFee = inv.Lines
            .Where(l => l.ChargeType != FeeChargeType.Management &&
                        l.ChargeType != FeeChargeType.Electricity &&
                        l.ChargeType != FeeChargeType.Water)
            .Sum(l => l.Amount);

        // 4. Thanh toán & Công nợ
        decimal paidAmount = inv.PaymentTransactions
            .Where(t => t.Status == PaymentTransactionStatus.Confirmed)
            .Sum(t => t.Amount);

        decimal debt = Math.Max(0m, inv.TotalAmount - paidAmount);

        // 5. Trạng thái hóa đơn
        string invoiceStatusText = inv.Status switch
        {
            ApartmentInvoiceStatus.Draft => "Bản nháp",
            ApartmentInvoiceStatus.Issued => "Đã phát hành",
            ApartmentInvoiceStatus.Void => "Đã hủy",
            _ => inv.Status.ToString()
        };

        // 6. Trạng thái thanh toán
        string paymentStatusText;
        if (paidAmount >= inv.TotalAmount && inv.TotalAmount > 0)
        {
            paymentStatusText = "Đã thanh toán";
        }
        else if (paidAmount > 0)
        {
            paymentStatusText = "Thanh toán một phần";
        }
        else
        {
            paymentStatusText = "Chưa thanh toán";
        }

        // 7. Ngày thanh toán gần nhất
        DateTime? lastPaymentDate = inv.PaymentTransactions
            .Where(t => t.Status == PaymentTransactionStatus.Confirmed)
            .OrderByDescending(t => t.ConfirmedAtUtc ?? t.CreatedAtUtc)
            .Select(t => t.ConfirmedAtUtc ?? t.CreatedAtUtc)
            .Cast<DateTime?>()
            .FirstOrDefault();

        string residentName = inv.Resident?.User?.FullName ?? inv.Resident?.CitizenId ?? string.Empty;
        string residentPhone = inv.Resident?.User?.PhoneNumber ?? inv.Resident?.EmergencyContact ?? string.Empty;

        return new MonthlyStatementItemDto
        {
            Stt = stt,
            InvoiceCode = inv.InvoiceCode,
            ApartmentCode = inv.Apartment?.ApartmentCode ?? string.Empty,
            BuildingName = inv.Apartment?.Building?.BuildingName ?? string.Empty,
            FloorNumber = inv.Apartment?.Floor ?? 0,
            ResidentName = residentName,
            PhoneNumber = residentPhone,
            ApartmentArea = inv.ApartmentArea,
            PureManagementFee = pureManagementFee,
            ParkingFee = parkingFee,
            ElectricityFee = electricityFee,
            WaterFee = waterFee,
            OtherFee = otherFee,
            TotalAmount = inv.TotalAmount,
            PaidAmount = paidAmount,
            OutstandingDebt = debt,
            InvoiceStatus = invoiceStatusText,
            PaymentStatus = paymentStatusText,
            LastPaymentDate = lastPaymentDate,
            DueDate = inv.DueDate
        };
    }

    /// <summary>
    /// Escape chuỗi tuân thủ nghiêm ngặt RFC 4180:
    /// Nếu chuỗi chứa dấu phẩy, dấu nháy kép hoặc dấu ngắt dòng thì bọc trong cặp nháy kép
    /// và nhân đôi dấu nháy kép nội tại (" -> "").
    /// </summary>
    private static string EscapeCsv(string? field)
    {
        if (string.IsNullOrEmpty(field))
            return string.Empty;

        if (field.Contains(',') || field.Contains('"') || field.Contains('\r') || field.Contains('\n'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Select(ch => invalid.Contains(ch) || ch == ' ' ? '_' : ch).ToArray());
        return Regex.Replace(clean, @"_+", "_").Trim('_');
    }
}
