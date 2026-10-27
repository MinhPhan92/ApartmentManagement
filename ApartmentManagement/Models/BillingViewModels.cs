using System.ComponentModel.DataAnnotations;
using ApartmentManagement.Services;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ApartmentManagement.Models;

public sealed class FeeTariffEditViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn tòa nhà.")]
    public int BuildingId { get; set; }

    [EnumDataType(typeof(FeeChargeType), ErrorMessage = "Loại phí không hợp lệ.")]
    public FeeChargeType ChargeType { get; set; }

    [DataType(DataType.Date)]
    public DateTime EffectiveFrom { get; set; } = DateTime.Today;

    [DataType(DataType.Date)]
    public DateTime? EffectiveTo { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999")]
    public decimal MonthlyRatePerSquareMeter { get; set; }

    [StringLength(2000)]
    public string TierDefinitions { get; set; } = string.Empty;

    public IReadOnlyList<SelectListItem> BuildingOptions { get; set; } = [];
}

public sealed class MeterReadingEditViewModel
{
    [Range(1, int.MaxValue)]
    public int ApartmentId { get; set; }

    [EnumDataType(typeof(FeeChargeType))]
    public FeeChargeType UtilityType { get; set; } = FeeChargeType.Electricity;

    [Range(2000, 2200)]
    public int BillingYear { get; set; } = DateTime.Today.Year;

    [Range(1, 12)]
    public int BillingMonth { get; set; } = DateTime.Today.Month;

    [Range(typeof(decimal), "0", "9999999999999999")]
    public decimal PreviousReading { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999")]
    public decimal CurrentReading { get; set; }

    public IReadOnlyList<SelectListItem> ApartmentOptions { get; set; } = [];
}

public sealed class GenerateInvoiceViewModel
{
    [Range(1, int.MaxValue)]
    public int ApartmentId { get; set; }

    [Range(2000, 2200)]
    public int BillingYear { get; set; } = DateTime.Today.Year;

    [Range(1, 12)]
    public int BillingMonth { get; set; } = DateTime.Today.Month;

    [DataType(DataType.Date)]
    public DateTime DueDate { get; set; } = DateTime.Today.AddMonths(1);

    public IReadOnlyList<SelectListItem> ApartmentOptions { get; set; } = [];
}

public sealed class BillingIndexViewModel
{
    public IReadOnlyList<ApartmentInvoice> Invoices { get; init; } = [];
    public IReadOnlyList<FeeTariff> Tariffs { get; init; } = [];
    public BillingSummaryDto? Summary { get; init; }

    public int? BuildingId { get; set; }
    public int? BillingYear { get; set; }
    public int? BillingMonth { get; set; }
    public ApartmentInvoiceStatus? Status { get; set; }
    public string? PaymentStatus { get; set; }
    public string? SearchTerm { get; set; }

    public IReadOnlyList<SelectListItem> BuildingOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> StatusOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> PaymentStatusOptions { get; set; } = [];
}

public sealed class IssueInvoiceViewModel
{
    public int InvoiceId { get; set; }

    [Required]
    public string RowVersionToken { get; set; } = string.Empty;
}

public sealed class VoidInvoiceViewModel
{
    public int InvoiceId { get; set; }

    [Required(ErrorMessage = "Phiên bản dữ liệu không hợp lệ.")]
    public string RowVersionToken { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Lý do hủy không vượt quá 500 ký tự.")]
    public string? Reason { get; set; }
}

public sealed class CloseTariffViewModel
{
    public int FeeTariffId { get; set; }
    public string BuildingName { get; set; } = string.Empty;
    public FeeChargeType ChargeType { get; set; }
    public DateTime EffectiveFrom { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày kết thúc hiệu lực.")]
    [DataType(DataType.Date)]
    public DateTime EffectiveTo { get; set; } = DateTime.Today;
}

public sealed class FeeTariffDetailsViewModel
{
    public required FeeTariff Tariff { get; init; }
    public int InvoicesCount { get; init; }
    public bool IsActive { get; init; }
    public bool CanClose { get; init; }
}

public sealed class ResidentInvoiceListViewModel
{
    public IReadOnlyList<ApartmentInvoice> Invoices { get; init; } = [];
}

public sealed class ResidentInvoiceDetailsViewModel
{
    public required ApartmentInvoice Invoice { get; init; }
    public PaymentTransaction? Payment { get; init; }
    public bool VietQrConfigured { get; init; }
    public bool VietQrAmountSupported { get; init; }
}

public sealed class PaymentTransactionsViewModel
{
    public IReadOnlyList<PaymentTransaction> Transactions { get; init; } = [];
    public PaymentTransactionStatus? Status { get; set; }
    public string? SearchTerm { get; set; }
    public IReadOnlyList<SelectListItem> StatusOptions { get; set; } = [];

    public int TotalCount { get; init; }
    public int PendingCount { get; init; }
    public int ConfirmedCount { get; init; }
    public decimal TotalConfirmedAmount { get; init; }
}

public sealed class ConfirmPaymentViewModel
{
    public int PaymentTransactionId { get; set; }

    [Required]
    public string RowVersionToken { get; set; } = string.Empty;
}
