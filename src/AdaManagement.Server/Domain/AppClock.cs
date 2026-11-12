namespace AdaManagement.Server.Domain;

/// <summary>A „ma” mindig budapesti idő szerint, függetlenül a szerver időzónájától.</summary>
public class AppClock(TimeProvider timeProvider)
{
    private static readonly TimeZoneInfo Budapest = TimeZoneInfo.FindSystemTimeZoneById("Europe/Budapest");

    public DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(UtcNow, Budapest));
}
