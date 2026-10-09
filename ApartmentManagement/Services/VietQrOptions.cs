using System.Text;
using System.Globalization;

namespace ApartmentManagement.Services;

public sealed class VietQrOptions
{
    public string BankBin { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;

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
