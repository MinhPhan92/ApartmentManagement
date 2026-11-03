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
    public IReadOnlyList<PaymentTransaction> Transactions { get; init; } = [];
    public decimal ConfirmedAmount { get; init; }
    public decimal RemainingAmount { get; init; }
    public bool VietQrConfigured { get; init; }
    public bool VietQrAmountSupported { get; init; }
    public bool CanCreatePaymentAttempt { get; init; }
    public VietQrOptions? VietQrOptions { get; init; }
    public bool HasAutoClosedRejections { get; init; }
    public string QrDisclaimerMessage { get; init; } =
        "Mã VietQR chỉ cung cấp thông tin chuyển khoản, không phải bằng chứng thanh toán. Hóa đơn chỉ được ghi nhận đã thanh toán sau khi Ban quản lý / Kế toán kiểm tra và xác nhận.";
    public string ReconciliationWarningMessage { get; init; } =
        "Lưu ý đối soát: Giao dịch ở trạng thái Đã từ chối (bao gồm giao dịch đóng tự động khi hóa đơn đã thanh toán đủ hoặc đã hủy) không đồng nghĩa với việc ngân hàng chắc chắn chưa nhận tiền. Nếu bạn đã chuyển khoản cho mã tham chiếu đó, vui lòng liên hệ Ban quản lý / Kế toán để kiểm tra sao kê và đối soát.";
}

public sealed class ResidentPaymentTransactionDetailsViewModel
{
    public required PaymentTransaction Transaction { get; init; }
    public required ApartmentInvoice Invoice { get; init; }
    public bool VietQrConfigured { get; init; }
    public bool VietQrAmountSupported { get; init; }
    public bool CanShowVietQr { get; init; }
    public VietQrOptions? VietQrOptions { get; init; }
    public bool IsAutoClosedRejection { get; init; }
    public string QrDisclaimerMessage { get; init; } =
        "Mã VietQR chỉ cung cấp thông tin chuyển khoản, không phải bằng chứng thanh toán. Hóa đơn chỉ được ghi nhận đã thanh toán sau khi Ban quản lý / Kế toán kiểm tra và xác nhận.";
    public string ReconciliationWarningMessage { get; init; } =
        "Lưu ý đối soát: Giao dịch bị từ chối hoặc đóng tự động không đồng nghĩa với việc ngân hàng chắc chắn chưa nhận tiền chuyển khoản. Nếu bạn đã chuyển tiền theo mã tham chiếu này, vui lòng liên hệ Ban quản lý / Kế toán để đối soát sao kê.";
}

public sealed class PaymentTransactionsViewModel
{
    public IReadOnlyList<PaymentTransaction> Transactions { get; init; } = [];
    public PaymentTransactionStatus? Status { get; set; }
    public string? SearchTerm { get; set; }
    public string? Reference { get; set; }
    public string? Apartment { get; set; }
    public string? Resident { get; set; }
    [DataType(DataType.Date)]
    public DateTime? FromDate { get; set; }
    [DataType(DataType.Date)]
    public DateTime? ToDate { get; set; }
    public IReadOnlyList<SelectListItem> StatusOptions { get; set; } = [];

    public int CurrentPage { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public int TotalFilteredCount { get; init; }
    public int TotalPages { get; init; } = 1;
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;

    public int TotalCount { get; init; }
    public int PendingCount { get; init; }
    public int ConfirmedCount { get; init; }
    public int RejectedCount { get; init; }
    public decimal TotalConfirmedAmount { get; init; }
    public bool HasAutoClosedRejections { get; init; }
    public string QrDisclaimerMessage { get; init; } =
        "Mã VietQR chỉ cung cấp thông tin chuyển khoản, không phải bằng chứng thanh toán. Nhân viên BQL/Kế toán cần kiểm tra sao kê ngân hàng trước khi xác nhận.";
    public string ReconciliationWarningMessage { get; init; } =
        "Cảnh báo nghiệp vụ đối soát: Giao dịch chuyển sang trạng thái Đã từ chối (do đóng tự động khi hóa đơn đã thanh toán đủ/đã hủy hoặc từ chối thủ công) không đồng nghĩa với việc ngân hàng chắc chắn chưa nhận tiền. BQL/Kế toán cần kiểm tra sao kê và đối soát để phát hiện trường hợp cư dân chuyển khoản nhiều lần; không tự động kết luận tiền chưa về và không tự động hoàn tiền khi chưa có quy trình phê duyệt.";
}

public sealed class PaymentTransactionDetailsViewModel
{
    public required PaymentTransaction Transaction { get; init; }
    public required ApartmentInvoice Invoice { get; init; }
    public IReadOnlyList<PaymentTransaction> InvoiceTransactions { get; init; } = [];
    public string RowVersionToken { get; init; } = string.Empty;
    public bool CanConfirm { get; init; }
    public bool CanReject { get; init; }
    public bool IsAutoClosedRejection { get; init; }
    public string QrDisclaimerMessage { get; init; } =
        "Mã VietQR chỉ cung cấp thông tin chuyển khoản, không phải bằng chứng thanh toán.";
    public string ReconciliationWarningMessage { get; init; } =
        "Cảnh báo nghiệp vụ đối soát: Giao dịch ở trạng thái Đã từ chối (đặc biệt khi đóng tự động do hóa đơn đã Paid/Void) không đồng nghĩa với việc ngân hàng chắc chắn chưa nhận tiền. BQL/Kế toán bắt buộc kiểm tra sao kê ngân hàng để đối soát trước khi kết luận hoặc xử lý khoản tiền thừa; không tự động hoàn tiền khi chưa có quy trình được phê duyệt.";
}

public sealed class ConfirmPaymentViewModel
{
    public int PaymentTransactionId { get; set; }

    [Required(ErrorMessage = "Phiên bản dữ liệu không hợp lệ.")]
    public string RowVersionToken { get; set; } = string.Empty;

    public int? ReturnInvoiceId { get; set; }
    public bool ReturnToDetails { get; set; }
}

public sealed class RejectPaymentViewModel
{
    public int PaymentTransactionId { get; set; }

    [Required(ErrorMessage = "Phiên bản dữ liệu không hợp lệ.")]
    public string RowVersionToken { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập lý do từ chối giao dịch.")]
    [StringLength(500, ErrorMessage = "Lý do từ chối không được vượt quá 500 ký tự.")]
    public string Reason { get; set; } = string.Empty;

    public int? ReturnInvoiceId { get; set; }
    public bool ReturnToDetails { get; set; }
}

public static class PaymentDisplayHelper
{
    public static bool IsAutoClosedRejectionReason(string? reason) =>
        !string.IsNullOrWhiteSpace(reason) &&
        (reason.StartsWith("Hóa đơn đã được thanh toán đủ qua giao dịch", StringComparison.OrdinalIgnoreCase) ||
         reason.StartsWith("Hóa đơn đã bị hủy bởi BQL", StringComparison.OrdinalIgnoreCase));
}

public sealed class BatchGenerateInvoicesViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn tòa nhà.")]
    [Display(Name = "Tòa nhà")]
    public int BuildingId { get; set; }

    [Range(2000, 2200, ErrorMessage = "Năm tính phí từ 2000 đến 2200.")]
    [Display(Name = "Năm tính phí")]
    public int BillingYear { get; set; } = DateTime.Today.Year;

    [Range(1, 12, ErrorMessage = "Tháng tính phí từ 1 đến 12.")]
    [Display(Name = "Tháng tính phí")]
    public int BillingMonth { get; set; } = DateTime.Today.Month;

    [DataType(DataType.Date)]
    [Display(Name = "Hạn thanh toán")]
    public DateTime DueDate { get; set; } = DateTime.Today.AddMonths(1);

    public IReadOnlyList<SelectListItem> BuildingOptions { get; set; } = [];

    public BatchBillingOperationResult? Result { get; set; }
}

public sealed class BatchMeterReadingRowItem
{
    public int ApartmentId { get; set; }
    public string ApartmentCode { get; set; } = string.Empty;
    public decimal ElectricityOldReading { get; set; }
    public decimal? ElectricityNewReading { get; set; }
    public decimal WaterOldReading { get; set; }
    public decimal? WaterNewReading { get; set; }
    public bool HasExistingReading { get; set; }
    public bool IsInvoiceLocked { get; set; }
    public string? StatusNote { get; set; }
}

public sealed class BatchMeterReadingViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn tòa nhà.")]
    [Display(Name = "Tòa nhà")]
    public int BuildingId { get; set; }

    public string BuildingName { get; set; } = string.Empty;

    [Range(2000, 2200, ErrorMessage = "Năm từ 2000 đến 2200.")]
    [Display(Name = "Năm")]
    public int BillingYear { get; set; } = DateTime.Today.Year;

    [Range(1, 12, ErrorMessage = "Tháng từ 1 đến 12.")]
    [Display(Name = "Tháng")]
    public int BillingMonth { get; set; } = DateTime.Today.Month;

    [DataType(DataType.Date)]
    [Display(Name = "Ngày ghi chỉ số")]
    public DateTime ReadingDate { get; set; } = DateTime.Today;

    public List<BatchMeterReadingRowItem> Rows { get; set; } = [];

    public IReadOnlyList<SelectListItem> BuildingOptions { get; set; } = [];

    public BatchMeterReadingOperationResult? Result { get; set; }
}

public sealed class BatchIssueItemViewModel
{
    public bool IsSelected { get; set; } = true;
    public int InvoiceId { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public string ApartmentCode { get; set; } = string.Empty;
    public string ResidentName { get; set; } = string.Empty;
    public decimal ManagementFee { get; set; }
    public decimal ElectricityFee { get; set; }
    public decimal WaterFee { get; set; }
    public decimal TotalAmount { get; set; }
    public string RowVersionToken { get; set; } = string.Empty;
}

public sealed class BatchIssueInvoicesViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn tòa nhà.")]
    [Display(Name = "Tòa nhà")]
    public int BuildingId { get; set; }

    [Range(2000, 2200, ErrorMessage = "Năm tính phí từ 2000 đến 2200.")]
    [Display(Name = "Năm tính phí")]
    public int BillingYear { get; set; } = DateTime.Today.Year;

    [Range(1, 12, ErrorMessage = "Tháng tính phí từ 1 đến 12.")]
    [Display(Name = "Tháng tính phí")]
    public int BillingMonth { get; set; } = DateTime.Today.Month;

    public IReadOnlyList<SelectListItem> BuildingOptions { get; set; } = [];

    public List<BatchIssueItemViewModel> DraftInvoices { get; set; } = [];

    public BatchIssueOperationResult? Result { get; set; }
}
