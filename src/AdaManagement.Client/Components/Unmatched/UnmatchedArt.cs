using System.Globalization;
using AdaManagement.Shared.Tournaments;

namespace AdaManagement.Client.Components.Unmatched;

/// <summary>
/// Az Unmatched képregényborító geometriája (az Adaminator UnmatchedPage-éből): a villám, a két csapatmező
/// és a VS-robbanás clip-path sokszögei. A villám nem díszítés a csatlakozáson, hanem maga a csatlakozás:
/// mindkét csapat a széléig vágódik, így egy lista határozza meg az elválasztót és a két mezőt is.
/// Mobilon a csapatok egymás alatt (vízszintes villám), asztalon egymás mellett (függőleges villám).
/// </summary>
public static class UnmatchedArt
{
    /// <summary>A villám középvonala: [hossz menti %, keresztirányú %].</summary>
    private static readonly (double Along, double Centre)[] Bolt = [(0, 57), (20, 45), (38, 58), (56, 44), (76, 57), (100, 45)];

    private const double BoltHalf = 5.2, BoltKeyline = 1.7, BoltReach = BoltHalf + BoltKeyline, ContentMargin = 2;

    /// <summary>A csapat tartalma ekkora részt foglalhat: a villám szűkebb oldala szabja meg.</summary>
    public static readonly string ContentExtent = Pct(Math.Min(
        Bolt.Min(b => b.Centre) - BoltReach,
        100 - (Bolt.Max(b => b.Centre) + BoltReach)) - ContentMargin);

    private static string Pct(double v) => v.ToString("0.#", CultureInfo.InvariantCulture) + "%";

    private static string Point(bool vertical, double along, double cross) =>
        vertical ? $"{Pct(cross)} {Pct(along)}" : $"{Pct(along)} {Pct(cross)}";

    private static IEnumerable<string> Edge(bool vertical, double offset) =>
        Bolt.Select(b => Point(vertical, b.Along, b.Centre + offset));

    /// <summary>A villám egyik oldalán minden, az adott oldal két távoli sarkával lezárva.</summary>
    public static string FieldClip(bool vertical, bool leading)
    {
        var outer = leading ? 0 : 100;
        var corners = new[] { Point(vertical, 100, outer), Point(vertical, 0, outer) };
        return $"polygon({string.Join(", ", Edge(vertical, leading ? -BoltReach : BoltReach).Concat(corners))})";
    }

    /// <summary>Maga a villámszalag: le az egyik szélén, vissza a másikon.</summary>
    public static string BoltClip(bool vertical) =>
        $"polygon({string.Join(", ", Edge(vertical, -BoltHalf).Concat(Edge(vertical, BoltHalf).Reverse()))})";

    /// <summary>A robbanás tüskéinek hossz-szorzói, hogy rajzoltnak hasson, ne ikonnak.</summary>
    private static readonly double[] Spikes = [1, 0.84, 1.06, 0.79, 1.02, 0.88, 1.1, 0.81, 1.04, 0.86, 1.08, 0.83];
    private const double BurstCore = 0.31;

    private static string BurstPoint(double turn, double radius)
    {
        var angle = turn * Math.PI * 2 - Math.PI / 2;
        return $"{Pct(50 + Math.Cos(angle) * radius * 100)} {Pct(50 + Math.Sin(angle) * radius * 100)}";
    }

    public static readonly string BurstClip = $"polygon({string.Join(", ", Spikes.SelectMany((length, i) => new[]
    {
        BurstPoint((double)i / Spikes.Length, 0.5 * length),
        BurstPoint((i + 0.5) / Spikes.Length, BurstCore),
    }))})";

    /// <summary>A két csapat és játékosaik.</summary>
    public sealed record Team(bool Leading, string Label, string[] Players, string CssClass);

    public static readonly Team[] Teams =
    [
        new(true, "Fiúk", ["Márk", "Ádám"], "fiuk"),
        new(false, "Lányok", ["Berni", "Reni"], "lanyok"),
    ];

    public static readonly string[] AllPlayers = Teams.SelectMany(t => t.Players).ToArray();

    /// <summary>A wwwroot/characters képeinek fájlnevei kiterjesztés nélkül; a megjelenített név aláhúzás helyett szóközzel.</summary>
    private static readonly string[] Files =
    [
        "Achilles", "Alice", "Ancient_Leshen", "Bigfoot", "Black_Beard", "Bloody_Mary", "Chupakabra", "Dracula",
        "Eredin", "Genie", "Geralt", "Hamlet", "Hoiduni", "Invisible_Man", "Jekyll_Hyde", "King_Arthur",
        "Loki", "Medusa", "Oda_Nobunaga", "Pandora", "Philippa", "Robin_Hood", "Shakespeare", "Sherlock_Holmes",
        "Sinbad", "Siri", "Sun_Wukong", "Titania", "Tomoe_Gozen", "Triss_Yennefer", "Wayward_Sisters", "Yennega",
    ];

    private static readonly Dictionary<string, string> ArtByName =
        Files.ToDictionary(f => f.Replace('_', ' '), f => $"characters/{f}.webp");

    public static readonly string[] CharacterNames = ArtByName.Keys.Order(StringComparer.Ordinal).ToArray();

    /// <summary>A karakter képe, vagy null, ha nincs ilyen (átnevezték vagy törölték a választás óta).</summary>
    public static string? ArtFor(string? character) =>
        character is not null && ArtByName.TryGetValue(character, out var url) ? url : null;
}
