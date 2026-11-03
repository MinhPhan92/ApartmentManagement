namespace ApartmentManagement.Models;

public sealed class MonthlyStatementItemDto
{
    public int Stt { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public string ApartmentCode { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;
    public int FloorNumber { get; set; }
    public string ResidentName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public decimal ApartmentArea { get; set; }

    /// <summary>
    /// Phí quản lý thuần diện tích (không bao gồm phí gửi xe).
    /// </summary>
    public decimal PureManagementFee { get; set; }

    /// <summary>
    /// Phí gửi xe (được bóc tách riêng từ phụ phí/breakdown).
    /// </summary>
    public decimal ParkingFee { get; set; }

    public decimal ElectricityFee { get; set; }
    public decimal WaterFee { get; set; }
    public decimal OtherFee { get; set; }

    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingDebt { get; set; }

    public string InvoiceStatus { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public DateTime? LastPaymentDate { get; set; }
    public DateTime DueDate { get; set; }
}

public sealed class MonthlyStatementReportDto
{
    public string ReportTitle { get; set; } = "BẢNG KÊ THU PHÍ & CÔNG NỢ CHI TIẾT";
    public string BuildingFilterName { get; set; } = "Tất cả tòa nhà";
    public string BillingPeriodText { get; set; } = string.Empty;
    public int? BillingYear { get; set; }
    public int? BillingMonth { get; set; }
    public DateTime GeneratedAtLocal { get; set; } = DateTime.Now;
    public string GeneratedBy { get; set; } = string.Empty;

    public List<MonthlyStatementItemDto> Items { get; set; } = new();

    public int TotalInvoices => Items.Count;
    public decimal TotalPureManagementFee => Items.Sum(x => x.PureManagementFee);
    public decimal TotalParkingFee => Items.Sum(x => x.ParkingFee);
    public decimal TotalElectricityFee => Items.Sum(x => x.ElectricityFee);
    public decimal TotalWaterFee => Items.Sum(x => x.WaterFee);
    public decimal TotalOtherFee => Items.Sum(x => x.OtherFee);
    public decimal GrandTotalAmount => Items.Sum(x => x.TotalAmount);
    public decimal GrandTotalPaidAmount => Items.Sum(x => x.PaidAmount);
    public decimal GrandTotalOutstandingDebt => Items.Sum(x => x.OutstandingDebt);
}

public sealed class MonthlyStatementFilterInput
{
    public int? BuildingId { get; set; }
    public int? BillingYear { get; set; }
    public int? BillingMonth { get; set; }
    public ApartmentInvoiceStatus? Status { get; set; }
    public string? PaymentStatus { get; set; }
    public string? SearchTerm { get; set; }
}

