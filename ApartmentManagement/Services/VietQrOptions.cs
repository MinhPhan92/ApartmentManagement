using System.Text;
using System.Globalization;

namespace ApartmentManagement.Services;

public sealed class VietQrOptions
{
    public string BankBin { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;

    public string BankName => BankBin switch
    {
        "970415" => "VietinBank (Ngân hàng TMCP Công Thương Việt Nam)",
        "970436" => "Vietcombank (Ngân hàng TMCP Ngoại Thương Việt Nam)",
        "970422" => "MB Bank (Ngân hàng TMCP Quân Đội)",
        "970405" => "Agribank (Ngân hàng Nông nghiệp & PTNT)",
        "970418" => "BIDV (Ngân hàng TMCP Đầu tư và Phát triển)",
        "970407" => "Techcombank (Ngân hàng TMCP Kỹ Thương)",
        "970423" => "TPBank (Ngân hàng TMCP Tiên Phong)",
        "970432" => "VPBank (Ngân hàng TMCP Việt Nam Thịnh Vượng)",
        "970441" => "VIB (Ngân hàng TMCP Quốc Tế)",
        "970416" => "ACB (Ngân hàng TMCP Á Châu)",
        _ => string.IsNullOrWhiteSpace(BankBin) ? "Chưa xác định" : $"Ngân hàng (BIN: {BankBin})"
    };

    public string? NormalizedAccountName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(AccountName)) return null;
            var decomposed = AccountName.Trim()
                .Replace('Đ', 'D')
                .Replace('đ', 'd')
                .Normalize(NormalizationForm.FormD);
            var normalized = new string(decomposed
                .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                .ToArray())
                .Normalize(NormalizationForm.FormC)
                .ToUpperInvariant();
            return normalized.Length is > 0 and <= 25 && normalized.All(char.IsAscii)
                ? normalized
                : null;
        }
    }

    public bool IsConfigured =>
        BankBin is { Length: 6 } &&
        BankBin.All(char.IsAsciiDigit) &&
        AccountNumber is { Length: > 0 and <= 19 } &&
        AccountNumber.All(char.IsAsciiLetterOrDigit) &&
        NormalizedAccountName != null;
}

public interface IVietQrCodeGenerator
{
    byte[] GeneratePng(string reference, decimal amount);
}
