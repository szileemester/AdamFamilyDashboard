using System.Text.RegularExpressions;

namespace AdaManagement.Shared;

/// <summary>
/// Elérhetőségek ellenőrzése és linkké alakítása (a szerver ugyanezzel ellenőriz, a kliens ezzel készít linket).
/// </summary>
public static partial class ContactLinks
{
    /// <summary>„szaki.hu” → „https://szaki.hu”. Csak http/https cím lehet; minden más (pl. „javascript:”) null.</summary>
    public static string? WebsiteUrl(string? website)
    {
        if (string.IsNullOrWhiteSpace(website)) return null;
        var s = website.Trim();
        // Saját sémával (http/https) úgy, ahogy van; minden más elé https:// kerül. Így a „://” a cím belsejében
        // (pl. egy lekérdezés-paraméterben) nem zavar, a „javascript:” pedig érvénytelen gépnévvé válik.
        return Http(s) ?? (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? null
            : Http("https://" + s));

        static string? Http(string s) =>
            Uri.TryCreate(s, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            && uri.Host.Contains('.')
                ? uri.AbsoluteUri
                : null;
    }

    /// <summary>A megjelenítéshez: „https://www.szaki.hu/” → „szaki.hu”.</summary>
    public static string WebsiteLabel(string website)
    {
        if (WebsiteUrl(website) is not { } url) return website;
        var uri = new Uri(url);
        var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
        var path = Uri.UnescapeDataString(uri.PathAndQuery).TrimEnd('/'); // „%C3%A1rak” → „árak”
        return host + path;
    }

    /// <summary>Számjegyek, szóköz, +, -, /, zárójel; legalább 6 számjegy.</summary>
    public static bool IsValidPhone(string phone) =>
        PhoneChars().IsMatch(phone.Trim()) && phone.Count(char.IsAsciiDigit) >= 6;

    /// <summary>„+36 30 / 123-4567” → „tel:+36301234567”.</summary>
    public static string PhoneHref(string phone) =>
        "tel:" + string.Concat(phone.Where(c => char.IsAsciiDigit(c) || c == '+'));

    public static bool IsValidEmail(string email) => EmailShape().IsMatch(email.Trim());

    [GeneratedRegex(@"^\+?[0-9 ()/\-]+$")]
    private static partial Regex PhoneChars();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailShape();
}
