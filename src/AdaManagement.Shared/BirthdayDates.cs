namespace AdaManagement.Shared;

/// <summary>Születésnap-dátumok – a szerver (dashboard) és a kliens (Születésnapok oldal) ugyanezt használja.</summary>
public static class BirthdayDates
{
    /// <summary>A születésnap dátuma az adott évben. Feb 29 nem szökőévben → Feb 28.</summary>
    public static DateOnly DateInYear(int year, int month, int day) =>
        month == 2 && day == 29 && !DateTime.IsLeapYear(year)
            ? new DateOnly(year, 2, 28)
            : new DateOnly(year, month, day);

    /// <summary>A legközelebbi előfordulás ma vagy később.</summary>
    public static DateOnly NextOccurrence(int month, int day, DateOnly today)
    {
        var thisYear = DateInYear(today.Year, month, day);
        return thisYear >= today ? thisYear : DateInYear(today.Year + 1, month, day);
    }

    /// <summary>Hány éves lesz az adott előforduláskor. Ismeretlen vagy jövőbeli születési évnél nincs életkor.</summary>
    public static int? TurningAge(int? birthYear, DateOnly occurrence) =>
        birthYear is { } y && occurrence.Year - y >= 1 ? occurrence.Year - y : null;

    public static bool IsValid(int month, int day) =>
        month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(2024, month); // 2024 szökőév: a feb 29 érvényes
}
