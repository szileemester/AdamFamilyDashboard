using System.Text.RegularExpressions;

namespace AdaManagement.Server.Domain;

/// <summary>
/// Egyszeri import CSV-ből vagy vCardból.
/// CSV soronként: „Név;Dátum” (vessző is lehet elválasztó). Dátum: ÉÉÉÉ-HH-NN, ÉÉÉÉ.HH.NN, HH-NN, HH.NN vagy --HH-NN.
/// vCard: FN és BDAY mezők.
/// </summary>
public static partial class BirthdayImportParser
{
    public record ParsedBirthday(string Name, int Month, int Day, int? Year);

    public static (List<ParsedBirthday> Items, int Skipped) Parse(string content)
    {
        return content.Contains("BEGIN:VCARD", StringComparison.OrdinalIgnoreCase)
            ? ParseVCard(content)
            : ParseCsv(content);
    }

    private static (List<ParsedBirthday>, int) ParseCsv(string content)
    {
        var items = new List<ParsedBirthday>();
        var skipped = 0;
        foreach (var raw in content.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var parts = line.Split(line.Contains(';') ? ';' : ',', 2);
            if (parts.Length == 2 && TryParseDate(parts[1].Trim().Trim('"'), out var m, out var d, out var y) && parts[0].Trim('"', ' ').Length > 0)
                items.Add(new ParsedBirthday(parts[0].Trim('"', ' '), m, d, y));
            else
                skipped++; // fejléc vagy hibás sor
        }
        return (items, skipped);
    }

    private static (List<ParsedBirthday>, int) ParseVCard(string content)
    {
        var items = new List<ParsedBirthday>();
        var skipped = 0;
        foreach (Match card in VCardRegex().Matches(content))
        {
            var body = card.Value;
            var name = FieldRegex("FN").Match(body) is { Success: true } fn ? fn.Groups[1].Value.Trim() : null;
            var bday = FieldRegex("BDAY").Match(body) is { Success: true } bd ? bd.Groups[1].Value.Trim() : null;
            if (!string.IsNullOrEmpty(name) && bday is not null && TryParseDate(bday, out var m, out var d, out var y))
                items.Add(new ParsedBirthday(name, m, d, y));
            else
                skipped++;
        }
        return (items, skipped);
    }

    public static bool TryParseDate(string s, out int month, out int day, out int? year)
    {
        month = day = 0;
        year = null;
        var match = DateRegex().Match(s);
        if (!match.Success) return false;

        if (match.Groups["y"].Success) year = int.Parse(match.Groups["y"].Value);
        month = int.Parse(match.Groups["m"].Value);
        day = int.Parse(match.Groups["d"].Value);
        return BirthdayCalculator.IsValid(month, day);
    }

    [GeneratedRegex(@"^(?:(?<y>\d{4})[-.]?|--)?(?<m>\d{1,2})[-.]?(?<d>\d{1,2})\.?$")]
    private static partial Regex DateRegex();

    [GeneratedRegex(@"BEGIN:VCARD.*?END:VCARD", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex VCardRegex();

    private static Regex FieldRegex(string field) =>
        new($@"^{field}(?:;[^:\r\n]*)?:(.*)$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
}
