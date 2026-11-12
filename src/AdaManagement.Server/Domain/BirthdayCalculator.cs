namespace AdaManagement.Server.Domain;

public static class BirthdayCalculator
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

    public static int? TurningAge(int? birthYear, DateOnly occurrence) =>
        birthYear is null ? null : occurrence.Year - birthYear.Value;

    public static bool IsValid(int month, int day) =>
        month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(2024, month); // 2024 szökőév: a feb 29 érvényes

    public static IReadOnlyList<(Birthday Birthday, DateOnly Date, int DaysUntil, int? TurningAge)> Upcoming(
        IEnumerable<Birthday> birthdays, DateOnly today, int lookaheadDays) =>
        birthdays
            .Select(b =>
            {
                var date = NextOccurrence(b.Month, b.Day, today);
                return (Birthday: b, Date: date, DaysUntil: date.DayNumber - today.DayNumber, TurningAge: TurningAge(b.Year, date));
            })
            .Where(x => x.DaysUntil <= lookaheadDays)
            .OrderBy(x => x.DaysUntil).ThenBy(x => x.Birthday.Name)
            .ToList();
}
