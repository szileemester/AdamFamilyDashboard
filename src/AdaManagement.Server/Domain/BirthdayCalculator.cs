using AdaManagement.Shared;

namespace AdaManagement.Server.Domain;

/// <summary>A dashboard „Hamarosan” listája. A dátumszámolás a közös <see cref="BirthdayDates"/>-ben van.</summary>
public static class BirthdayCalculator
{
    public static DateOnly DateInYear(int year, int month, int day) => BirthdayDates.DateInYear(year, month, day);

    public static DateOnly NextOccurrence(int month, int day, DateOnly today) => BirthdayDates.NextOccurrence(month, day, today);

    public static int? TurningAge(int? birthYear, DateOnly occurrence) => BirthdayDates.TurningAge(birthYear, occurrence);

    public static bool IsValid(int month, int day) => BirthdayDates.IsValid(month, day);

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
