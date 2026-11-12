namespace AdaManagement.Server.Domain;

public static class RecurrenceCalculator
{
    /// <summary>
    /// A k-adik esedékesség a horgonytól: Anchor + k·N hónap.
    /// A DateOnly.AddMonths a hónap végére vág (jan 31 + 1 hónap = feb 28/29), de mivel mindig a horgonyból
    /// számolunk, a következő alkalom újra 31-e lesz, ha az a hónapban létezik.
    /// </summary>
    public static DateOnly DueDate(DateOnly anchor, int intervalMonths, int index)
    {
        if (intervalMonths < 1) throw new ArgumentOutOfRangeException(nameof(intervalMonths));
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        return anchor.AddMonths(checked(intervalMonths * index));
    }
}
