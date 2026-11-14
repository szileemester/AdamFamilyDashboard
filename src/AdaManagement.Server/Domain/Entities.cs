namespace AdaManagement.Server.Domain;

public class TaskItem
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public string? Notes { get; set; }
    public bool IsImportant { get; set; }
    public bool IsUrgent { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>Kézzel választott ikon kulcsa (a kliens ikonkatalógusából); null = automatikus a cím alapján.</summary>
    public string? Icon { get; set; }

    /// <summary>Ha ismétlődő sablonból generálódott: a sablon azonosítója.</summary>
    public int? RecurringTemplateId { get; set; }
    public RecurringTemplate? RecurringTemplate { get; set; }

    /// <summary>Melyik esedékességhez (alkalomhoz) tartozik. A (sablon, alkalom) pár egyedi.</summary>
    public DateOnly? OccurrenceDate { get; set; }

    public bool IsOpen => CompletedAt is null;
}

public class RecurringTemplate
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public string? Notes { get; set; }
    public int IntervalMonths { get; set; }

    /// <summary>A horgonydátum: minden esedékesség ebből számolódik (Anchor + k·N hónap), így nincs hónapvégi csúszás.</summary>
    public DateOnly AnchorDate { get; set; }
    public int OccurrenceIndex { get; set; }
    public DateOnly NextDueDate { get; set; }
    public int LeadDays { get; set; } = 14;
    public bool IsActive { get; set; } = true;

    /// <summary>Törlés időpontja. A törölt sablon egy napig visszaállítható (Visszavonás), utána véglegesen törlődik.</summary>
    public DateTime? DeletedAt { get; set; }

    public List<CompletionLogEntry> Log { get; set; } = [];

    /// <summary>Új horgonyt állít: a megadott dátum lesz a következő esedékesség.</summary>
    public void Reschedule(DateOnly nextDueDate)
    {
        AnchorDate = nextDueDate;
        OccurrenceIndex = 0;
        NextDueDate = nextDueDate;
    }

    /// <summary>
    /// Intervallum-váltás változatlan következő esedékességgel. Ha a horgony napja (pl. 31.) a mostani dátumon a hónap
    /// vége miatt le volt vágva (feb 28), olyan korábbi horgonyt keres ugyanazzal a nappal, amelyből az új intervallummal
    /// pont a mostani esedékesség jön ki – így a későbbi alkalmak újra 31-ére esnek. Ha nincs ilyen, a mostani dátum az új horgony.
    /// </summary>
    public void ChangeInterval(int intervalMonths)
    {
        var due = NextDueDate;
        var day = AnchorDate.Day;
        IntervalMonths = intervalMonths;
        if (day > due.Day)
        {
            for (var k = 1; k <= 24; k++)
            {
                var month = due.AddMonths(-intervalMonths * k);
                if (DateTime.DaysInMonth(month.Year, month.Month) < day) continue;
                var anchor = new DateOnly(month.Year, month.Month, day);
                if (RecurrenceCalculator.DueDate(anchor, intervalMonths, k) != due) continue;
                AnchorDate = anchor;
                OccurrenceIndex = k;
                return;
            }
        }
        Reschedule(due);
    }

    /// <summary>A jelenlegi alkalom utáni esedékesség, ha nem írod át kézzel.</summary>
    public DateOnly SuggestedFollowingDueDate() =>
        RecurrenceCalculator.DueDate(AnchorDate, IntervalMonths, OccurrenceIndex + 1);

    /// <summary>Előre lép a következő alkalomra (fix ütemezés szerint).</summary>
    public void Advance()
    {
        OccurrenceIndex++;
        NextDueDate = RecurrenceCalculator.DueDate(AnchorDate, IntervalMonths, OccurrenceIndex);
    }

    /// <summary>Teljesítés: naplóbejegyzés a mostani ütemezéssel (a visszavonáshoz), majd továbblépés.</summary>
    public CompletionLogEntry Complete(DateOnly completedOn, DateOnly nextDueDate, string? note)
    {
        var entry = new CompletionLogEntry
        {
            RecurringTemplateId = Id,
            DueDate = NextDueDate,
            CompletedOn = completedOn,
            Note = note,
            PreviousAnchorDate = AnchorDate,
            PreviousOccurrenceIndex = OccurrenceIndex,
        };
        MoveTo(nextDueDate);
        return entry;
    }

    /// <summary>Visszavonás: a teljesítés előtti ütemezés visszaállítása.</summary>
    public void Restore(CompletionLogEntry entry)
    {
        if (entry.PreviousAnchorDate is { } anchor && entry.PreviousOccurrenceIndex is { } index)
        {
            AnchorDate = anchor;
            OccurrenceIndex = index;
            NextDueDate = RecurrenceCalculator.DueDate(anchor, IntervalMonths, index);
            if (NextDueDate == entry.DueDate) return;
        }
        // Régi bejegyzés vagy közben módosított intervallum: legalább az esedékesség álljon vissza.
        Reschedule(entry.DueDate);
    }

    /// <summary>Az aktuális alkalom lezárása: ha a felajánlott dátum marad, a horgonyból lép tovább, különben új horgony.</summary>
    public void MoveTo(DateOnly nextDueDate)
    {
        if (nextDueDate == SuggestedFollowingDueDate())
            Advance();
        else
            Reschedule(nextDueDate);
    }
}

public class CompletionLogEntry
{
    public int Id { get; set; }
    public int RecurringTemplateId { get; set; }
    public DateOnly DueDate { get; set; }
    public DateOnly CompletedOn { get; set; }
    public string? Note { get; set; }

    /// <summary>A sablon ütemezése a teljesítés előtt – ebből áll vissza a visszavonás.</summary>
    public DateOnly? PreviousAnchorDate { get; set; }
    public int? PreviousOccurrenceIndex { get; set; }
}

public class Birthday
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int Month { get; set; }
    public int Day { get; set; }
    public int? Year { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Megbízható szakember („szaki”): foglalkozás, név és elérhetőségek.</summary>
public class Tradesperson
{
    public int Id { get; set; }
    public required string Occupation { get; set; }
    public required string Name { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Egyetlen sor az alkalmazás beállításaival.</summary>
public class AppSettings
{
    public const int SingletonId = 1;
    public int Id { get; set; } = SingletonId;
    public string WeatherLabel { get; set; } = "Budapest";
    public double Latitude { get; set; } = 47.4979;
    public double Longitude { get; set; } = 19.0402;
    public int BirthdayLookaheadDays { get; set; } = 30;
}
