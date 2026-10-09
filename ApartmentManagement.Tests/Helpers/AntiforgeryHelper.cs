using System.Text.RegularExpressions;

namespace ApartmentManagement.Tests.Helpers;

public static partial class AntiforgeryHelper
{
    public static async Task<(string Cookie, string Token)> ExtractAsync(
        HttpClient client,
        string formUrl)
    {
        using var response = await client.GetAsync(formUrl);
        var html = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Không thể tải form antiforgery ({(int)response.StatusCode}): {html}");
        }
        var inputMatch = AntiforgeryInputRegex().Match(html);
        if (!inputMatch.Success)
        {
            throw new InvalidOperationException("Không tìm thấy antiforgery input trong HTML.");
        }

        var valueMatch = ValueRegex().Match(inputMatch.Value);
        if (!valueMatch.Success)
        {
            throw new InvalidOperationException("Không tìm thấy giá trị antiforgery token.");
        }

        var cookie = response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.FirstOrDefault(value => value.Contains(".AspNetCore.Antiforgery", StringComparison.Ordinal))
            : null;

        if (cookie == null)
        {
            throw new InvalidOperationException("Không tìm thấy antiforgery cookie.");
        }

        return (cookie.Split(';')[0], valueMatch.Groups[1].Value);
    }

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex AntiforgeryInputRegex();

    [GeneratedRegex("value=\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex ValueRegex();
}
