using MudBlazor;

namespace AdaManagement.Client.Components;

public record QuadrantInfo(string Id, string Title, string ShortTitle, string Subtitle, bool Important, bool Urgent)
{
    /// <summary>A negyed jelölése ikonokkal: ★ = fontos, ⚡ = sürgős, hold = egyik sem.</summary>
    public IEnumerable<string> FlagIcons =>
        Important || Urgent
            ? new[] { Important ? Icons.Material.Outlined.StarOutline : null, Urgent ? Icons.Material.Outlined.Bolt : null }.OfType<string>()
            : [Icons.Material.Outlined.Bedtime];

    public string ColorClass => $"q-color-{Id}";
}

public static class Quadrants
{
    public static readonly QuadrantInfo[] All =
    [
        new("q1", "Csináld most", "Most", "Fontos és sürgős", true, true),
        new("q2", "Tervezd be", "Terv", "Fontos, nem sürgős", true, false),
        new("q3", "Gyorsan / add ki", "Gyors", "Sürgős, nem fontos", false, true),
        new("q4", "Ráér", "Ráér", "Se nem fontos, se nem sürgős", false, false),
    ];

    public static QuadrantInfo Of(bool important, bool urgent) =>
        All.First(q => q.Important == important && q.Urgent == urgent);

    public static QuadrantInfo ById(string id) => All.First(q => q.Id == id);
}

public static class Format
{
    // A „ma” mindenhol budapesti idő szerint (mint a szerveren), akkor is, ha az eszköz más időzónában van.
    private static readonly TimeZoneInfo? Budapest = FindBudapest();

    private static TimeZoneInfo? FindBudapest()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Budapest"); }
        catch { return null; }
    }

    /// <summary>Budapesti helyi idő.</summary>
    public static DateTime Now => Budapest is null ? DateTime.Now : TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Budapest);

    /// <summary>Budapesti mai nap.</summary>
    public static DateOnly Today => DateOnly.FromDateTime(Now);

    /// <summary>Szervertől kapott UTC időpont budapesti helyi időben (a JSON-ból jövő „Unspecified” is UTC-nek számít).</summary>
    public static DateTime ToLocal(DateTime utc) =>
        Budapest is null ? DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime()
                         : TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Budapest);

    public static string Date(DateOnly d) => d.ToString("yyyy. MM. dd.");

    public static string Relative(int days) => days switch
    {
        0 => "ma",
        1 => "holnap",
        -1 => "tegnap",
        > 1 => $"{days} nap múlva",
        _ => $"{-days} napja lejárt",
    };

    public static DateTime? ToDateTime(DateOnly? d) => d?.ToDateTime(TimeOnly.MinValue);

    public static DateOnly? ToDateOnly(DateTime? d) => d is null ? null : DateOnly.FromDateTime(d.Value);
}
