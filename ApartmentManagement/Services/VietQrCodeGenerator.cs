using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using QRCoder;

namespace ApartmentManagement.Services;

public sealed class VietQrCodeGenerator : IVietQrCodeGenerator
{
    public const decimal MaximumVndAmount = 9_999_999_999_999m;
    private readonly VietQrOptions _options;

    public VietQrCodeGenerator(IOptions<VietQrOptions> options) =>
        _options = options.Value;

    public byte[] GeneratePng(string reference, decimal amount)
    {
        if (!_options.IsConfigured)
            throw new InvalidOperationException("Chưa cấu hình tài khoản nhận VietQR.");
        if (string.IsNullOrWhiteSpace(reference) || reference.Length > 25 ||
            reference.Any(c => !char.IsAsciiLetterOrDigit(c)))
            throw new ArgumentException("Mã tham chiếu VietQR không hợp lệ.", nameof(reference));
        if (amount <= 0 || decimal.Round(amount, 0) != amount ||
            amount > MaximumVndAmount)
            throw new ArgumentOutOfRangeException(nameof(amount), "Số tiền VietQR phải là VND nguyên dương.");

        var merchantAccount = Tlv("00", "A000000727") +
                              Tlv("01", _options.BankBin) +
                              Tlv("02", _options.AccountNumber) +
                              Tlv("03", "QRIBFTTA");
        var additionalData = Tlv("08", reference);
        var accountName = _options.NormalizedAccountName
            ?? throw new InvalidOperationException("Tên tài khoản VietQR chưa hợp lệ.");
        var payload = Tlv("00", "01") +
                      Tlv("01", "12") +
                      Tlv("38", merchantAccount) +
                      Tlv("53", "704") +
                      Tlv("54", amount.ToString("0", CultureInfo.InvariantCulture)) +
                      Tlv("58", "VN") +
                      Tlv("59", accountName) +
                      Tlv("62", additionalData) +
                      "6304";
        payload += Crc16(payload).ToString("X4", CultureInfo.InvariantCulture);

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(8);
    }

    private static string Tlv(string tag, string value)
    {
        var length = Encoding.UTF8.GetByteCount(value);
        if (length > 99) throw new ArgumentOutOfRangeException(nameof(value), "Giá trị TLV quá dài.");
        return tag + length.ToString("D2", CultureInfo.InvariantCulture) + value;
    }

    private static ushort Crc16(string value)
    {
        ushort crc = 0xFFFF;
        foreach (var valueByte in Encoding.ASCII.GetBytes(value))
        {
            crc ^= (ushort)(valueByte << 8);
            for (var bit = 0; bit < 8; bit++)
                crc = (ushort)((crc & 0x8000) != 0 ? (crc << 1) ^ 0x1021 : crc << 1);
        }

        return crc;
    }
}
