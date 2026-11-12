using AdaManagement.Server.Domain;
using AdaManagement.Shared;

namespace AdaManagement.Server.Api;

public static class Mapping
{
    public static TaskDto ToDto(this TaskItem t) =>
        new(t.Id, t.Title, t.Notes, t.IsImportant, t.IsUrgent, t.DueDate, t.CreatedAt, t.CompletedAt, t.RecurringTemplateId, t.OccurrenceDate);

    public static TemplateDto ToDto(this RecurringTemplate t) =>
        new(t.Id, t.Title, t.Notes, t.IntervalMonths, t.NextDueDate, t.SuggestedFollowingDueDate(), t.LeadDays, t.IsActive);

    public static CompletionLogDto ToDto(this CompletionLogEntry e) => new(e.Id, e.DueDate, e.CompletedOn, e.Note);

    public static BirthdayDto ToDto(this Birthday b) => new(b.Id, b.Name, b.Month, b.Day, b.Year, b.Notes);

    public static SettingsDto ToDto(this AppSettings s) => new(s.WeatherLabel, s.Latitude, s.Longitude, s.BirthdayLookaheadDays);

    public static string? Clean(this string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

/// <summary>Egyszerű bemenet-ellenőrzés: hibaüzenetek mezőnként, ValidationProblem-hez.</summary>
public static class Validate
{
    public static Dictionary<string, string[]> Task(TaskEditDto d)
    {
        var e = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(d.Title)) e["Title"] = ["A cím kötelező."];
        else if (d.Title.Length > 200) e["Title"] = ["Legfeljebb 200 karakter."];
        return e;
    }

    public static Dictionary<string, string[]> Template(TemplateEditDto d)
    {
        var e = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(d.Title)) e["Title"] = ["A cím kötelező."];
        if (d.IntervalMonths is < 1 or > 240) e["IntervalMonths"] = ["1 és 240 hónap között."];
        if (d.LeadDays is < 0 or > 365) e["LeadDays"] = ["0 és 365 nap között."];
        return e;
    }

    public static Dictionary<string, string[]> Birthday(BirthdayEditDto d)
    {
        var e = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(d.Name)) e["Name"] = ["A név kötelező."];
        if (!BirthdayCalculator.IsValid(d.Month, d.Day)) e["Day"] = ["Érvénytelen dátum."];
        if (d.Year is { } y && (y < 1900 || y > 2100)) e["Year"] = ["1900 és 2100 között."];
        return e;
    }

    public static Dictionary<string, string[]> Settings(SettingsDto d)
    {
        var e = new Dictionary<string, string[]>();
        if (d.Latitude is < -90 or > 90 || d.Longitude is < -180 or > 180) e["Latitude"] = ["Érvénytelen koordináta."];
        if (d.BirthdayLookaheadDays is < 1 or > 366) e["BirthdayLookaheadDays"] = ["1 és 366 nap között."];
        return e;
    }
}
