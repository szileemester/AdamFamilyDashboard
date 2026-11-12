using MudBlazor;

namespace AdaManagement.Client.Components;

public record QuadrantInfo(string Id, string Title, string ShortTitle, string Subtitle, bool Important, bool Urgent, string CssClass)
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
        new("q1", "Csináld most", "Most", "Fontos és sürgős", true, true, "quadrant-q1"),
        new("q2", "Tervezd be", "Terv", "Fontos, nem sürgős", true, false, "quadrant-q2"),
        new("q3", "Gyorsan / add ki", "Gyors", "Sürgős, nem fontos", false, true, "quadrant-q3"),
        new("q4", "Ráér", "Ráér", "Se nem fontos, se nem sürgős", false, false, "quadrant-q4"),
    ];

    public static QuadrantInfo Of(bool important, bool urgent) =>
        All.First(q => q.Important == important && q.Urgent == urgent);

    public static QuadrantInfo ById(string id) => All.First(q => q.Id == id);
}

public static class Format
{
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
