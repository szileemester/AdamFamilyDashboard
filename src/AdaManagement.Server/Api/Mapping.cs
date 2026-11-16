using System.Linq.Expressions;
using AdaManagement.Server.Domain;
using AdaManagement.Shared;

namespace AdaManagement.Server.Api;

public static class Mapping
{
    /// <summary>
    /// Feladat → DTO lekérdezésben: a kártya jelvényeihez a teendő- és bejegyzésszámok ugyanabban a lekérdezésben
    /// (allekérdezésként) jönnek, így nincs külön kör, és egy útvonalról sem maradhatnak le.
    /// </summary>
    public static readonly Expression<Func<TaskItem, TaskDto>> TaskToDto = t => new TaskDto(
        t.Id, t.Title, t.Notes, t.IsImportant, t.IsUrgent, t.DueDate, t.CreatedAt, t.CompletedAt, t.RecurringTemplateId, t.OccurrenceDate, t.Icon,
        t.Checklist.Count, t.Checklist.Count(i => i.IsDone), t.Story.Count);

    public static ChecklistItemDto ToDto(this TaskChecklistItem i) => new(i.Id, i.Text, i.IsDone);

    public static StoryEntryDto ToDto(this TaskStoryEntry e) => new(e.Id, e.At, e.Text);

    public static TemplateDto ToDto(this RecurringTemplate t) =>
        new(t.Id, t.Title, t.Notes, t.IntervalMonths, t.NextDueDate, t.SuggestedFollowingDueDate(), t.LeadDays, t.IsActive);

    public static CompletionLogDto ToDto(this CompletionLogEntry e) => new(e.Id, e.DueDate, e.CompletedOn, e.Note);

    public static BirthdayDto ToDto(this Birthday b) => new(b.Id, b.Name, b.Month, b.Day, b.Year, b.Notes);

    public static TradespersonDto ToDto(this Tradesperson t) => new(t.Id, t.Occupation, t.Name, t.Phone, t.Email, t.Website, t.Notes);

    public static SettingsDto ToDto(this AppSettings s) => new(s.WeatherLabel, s.Latitude, s.Longitude, s.BirthdayLookaheadDays);

    public static string? Clean(this string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

/// <summary>
/// Egyszerű bemenet-ellenőrzés: hibaüzenetek mezőnként, ValidationProblem-hez. A hosszkorlátok az adatbázis
/// oszlopaihoz igazodnak (így 400 jön az 500 helyett), a dátumok egy értelmes tartományban maradnak
/// (a szélsőséges évek a hónap-/napszámolásnál túlcsordulnának).
/// </summary>
public static class Validate
{
    public static readonly DateOnly MinDate = new(1900, 1, 1);
    public static readonly DateOnly MaxDate = new(2199, 12, 31);

    public const int TitleMax = 200;
    public const int NotesMax = 2000;
    public const int ShortNotesMax = 1000;
    public const int ChecklistTextMax = TaskStoryLimits.ChecklistTextMax;
    public const int ChecklistMaxItems = TaskStoryLimits.ChecklistMaxItems;
    public const int StoryTextMax = TaskStoryLimits.StoryTextMax;

    public static Dictionary<string, string[]> Task(TaskEditDto d)
    {
        var e = new Dictionary<string, string[]>();
        Text(e, "Title", d.Title, TitleMax, required: "A cím kötelező.");
        Text(e, "Notes", d.Notes, NotesMax);
        if (d.Icon is { Length: > 40 }) e["Icon"] = ["Érvénytelen ikon."];
        Date(e, "DueDate", d.DueDate);
        return e;
    }

    public static Dictionary<string, string[]> ChecklistItem(ChecklistItemEditDto d)
    {
        var e = new Dictionary<string, string[]>();
        Text(e, "Text", d.Text, ChecklistTextMax, required: "A teendő szövege kötelező.");
        return e;
    }

    public static Dictionary<string, string[]> StoryEntry(StoryEntryEditDto d)
    {
        var e = new Dictionary<string, string[]>();
        Text(e, "Text", d.Text, StoryTextMax, required: "A bejegyzés szövege kötelező.");
        Date(e, "At", DateOnly.FromDateTime(d.At.UtcDateTime));
        return e;
    }

    public static Dictionary<string, string[]> Template(TemplateEditDto d)
    {
        var e = new Dictionary<string, string[]>();
        Text(e, "Title", d.Title, TitleMax, required: "A cím kötelező.");
        Text(e, "Notes", d.Notes, NotesMax);
        if (d.IntervalMonths is < 1 or > 240) e["IntervalMonths"] = ["1 és 240 hónap között."];
        if (d.LeadDays is < 0 or > 365) e["LeadDays"] = ["0 és 365 nap között."];
        Date(e, "NextDueDate", d.NextDueDate);
        return e;
    }

    public static Dictionary<string, string[]> Completion(CompleteOccurrenceDto d)
    {
        var e = new Dictionary<string, string[]>();
        Text(e, "Note", d.Note, ShortNotesMax);
        Date(e, "CompletedOn", d.CompletedOn);
        Date(e, "NextDueDate", d.NextDueDate);
        return e;
    }

    public static Dictionary<string, string[]> Birthday(BirthdayEditDto d, DateOnly today)
    {
        var e = new Dictionary<string, string[]>();
        Text(e, "Name", d.Name, TitleMax, required: "A név kötelező.");
        Text(e, "Notes", d.Notes, ShortNotesMax);
        if (!BirthdayCalculator.IsValid(d.Month, d.Day)) e["Day"] = ["Érvénytelen dátum."];
        if (d.Year is { } y && (y < 1900 || y > today.Year)) e["Year"] = [$"1900 és {today.Year} között."];
        return e;
    }

    public static Dictionary<string, string[]> Tradesperson(TradespersonEditDto d)
    {
        var e = new Dictionary<string, string[]>();
        Text(e, "Occupation", d.Occupation, 100, required: "A foglalkozás kötelező.");
        Text(e, "Name", d.Name, TitleMax, required: "A név kötelező.");
        Text(e, "Notes", d.Notes, NotesMax);
        if (d.Phone.Clean() is { } phone && (phone.Length > 50 || !ContactLinks.IsValidPhone(phone)))
            e["Phone"] = ["Érvénytelen telefonszám."];
        if (d.Email.Clean() is { } email && (email.Length > 254 || !ContactLinks.IsValidEmail(email)))
            e["Email"] = ["Érvénytelen e-mail-cím."];
        if (d.Website.Clean() is { } web && (web.Length > 500 || ContactLinks.WebsiteUrl(web) is null))
            e["Website"] = ["Érvénytelen webcím."];
        return e;
    }

    public static Dictionary<string, string[]> Settings(SettingsDto d)
    {
        var e = new Dictionary<string, string[]>();
        if (!double.IsFinite(d.Latitude) || !double.IsFinite(d.Longitude) || d.Latitude is < -90 or > 90 || d.Longitude is < -180 or > 180)
            e["Latitude"] = ["Érvénytelen koordináta."];
        Text(e, "WeatherLabel", d.WeatherLabel, TitleMax);
        if (d.BirthdayLookaheadDays is < 1 or > 366) e["BirthdayLookaheadDays"] = ["1 és 366 nap között."];
        return e;
    }

    private static void Text(Dictionary<string, string[]> e, string field, string? value, int max, string? required = null)
    {
        if (required is not null && string.IsNullOrWhiteSpace(value)) e[field] = [required];
        else if (value is not null && value.Trim().Length > max) e[field] = [$"Legfeljebb {max} karakter."];
    }

    private static void Date(Dictionary<string, string[]> e, string field, DateOnly? value)
    {
        if (value is { } d && (d < MinDate || d > MaxDate)) e[field] = [$"{MinDate.Year} és {MaxDate.Year} közötti dátum legyen."];
    }
}
