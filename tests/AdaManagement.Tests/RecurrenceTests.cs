using AdaManagement.Server.Domain;

namespace AdaManagement.Tests;

public class RecurrenceTests
{
    private static RecurringTemplate Template(DateOnly next, int months)
    {
        var t = new RecurringTemplate { Title = "Olajcsere", IntervalMonths = months };
        t.Reschedule(next);
        return t;
    }

    [Theory]
    [InlineData(1, "2026-02-28")]
    [InlineData(2, "2026-03-31")]
    [InlineData(3, "2026-04-30")]
    [InlineData(12, "2027-01-31")]
    public void Honapvegi_horgony_nem_csuszik(int index, string expected)
    {
        var due = RecurrenceCalculator.DueDate(new DateOnly(2026, 1, 31), 1, index);
        Assert.Equal(DateOnly.Parse(expected), due);
    }

    [Fact]
    public void Advance_a_horgonybol_szamol()
    {
        var t = Template(new DateOnly(2026, 1, 31), 1);
        t.Advance();
        Assert.Equal(new DateOnly(2026, 2, 28), t.NextDueDate);
        t.Advance();
        Assert.Equal(new DateOnly(2026, 3, 31), t.NextDueDate);
    }

    [Fact]
    public void Kesoi_teljesites_nem_tolja_el_az_utemezest()
    {
        // Kazán: március 1-jén esedékes, júniusban lett kész → a következő akkor is jövő március 1.
        var t = Template(new DateOnly(2026, 3, 1), 12);
        t.MoveTo(t.SuggestedFollowingDueDate());
        Assert.Equal(new DateOnly(2027, 3, 1), t.NextDueDate);
    }

    [Fact]
    public void Kezzel_atirt_datum_uj_horgonyt_allit()
    {
        var t = Template(new DateOnly(2026, 1, 31), 1);
        t.MoveTo(new DateOnly(2026, 3, 10));
        Assert.Equal(new DateOnly(2026, 3, 10), t.NextDueDate);
        t.Advance();
        Assert.Equal(new DateOnly(2026, 4, 10), t.NextDueDate);
    }

    [Fact]
    public void Visszavonas_visszaallitja_a_horgonyt_es_az_esedekesseget()
    {
        var t = Template(new DateOnly(2026, 1, 31), 1);
        t.Advance(); // feb 28
        var entry = t.Complete(new DateOnly(2026, 2, 27), t.SuggestedFollowingDueDate(), "kész"); // → márc 31
        Assert.Equal(new DateOnly(2026, 3, 31), t.NextDueDate);

        t.Restore(entry);
        Assert.Equal(new DateOnly(2026, 2, 28), t.NextDueDate);
        t.Advance();
        Assert.Equal(new DateOnly(2026, 3, 31), t.NextDueDate); // a hónapvégi horgony megmaradt
    }

    [Fact]
    public void Visszavonas_kezzel_atirt_datum_utan_is_mukodik()
    {
        var t = Template(new DateOnly(2026, 3, 1), 12);
        var entry = t.Complete(new DateOnly(2026, 6, 1), new DateOnly(2027, 6, 1), null); // új horgony
        t.Restore(entry);
        Assert.Equal(new DateOnly(2026, 3, 1), t.NextDueDate);
        Assert.Equal(new DateOnly(2027, 3, 1), t.SuggestedFollowingDueDate());
    }

    [Fact]
    public void Ervenytelen_intervallum_hibat_dob() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => RecurrenceCalculator.DueDate(new DateOnly(2026, 1, 1), 0, 1));
}

public class OccurrenceGeneratorTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 8, 0, 0, DateTimeKind.Utc);

    private static RecurringTemplate Template(int id, DateOnly next, int leadDays = 14, bool active = true)
    {
        var t = new RecurringTemplate { Id = id, Title = $"T{id}", IntervalMonths = 12, LeadDays = leadDays, IsActive = active };
        t.Reschedule(next);
        return t;
    }

    [Fact]
    public void LeadDays_hataron_general()
    {
        var today = new DateOnly(2026, 11, 12);
        var created = OccurrenceGenerator.TasksToCreate(today, Now,
            [Template(1, today.AddDays(14)), Template(2, today.AddDays(15))], []);

        var task = Assert.Single(created);
        Assert.Equal(1, task.RecurringTemplateId);
        Assert.True(task.IsImportant);
        Assert.False(task.IsUrgent);
        Assert.Equal(today.AddDays(14), task.DueDate);
        Assert.Equal(today.AddDays(14), task.OccurrenceDate);
    }

    [Fact]
    public void Lejart_esedekessegre_is_general()
    {
        var today = new DateOnly(2026, 11, 12);
        var created = OccurrenceGenerator.TasksToCreate(today, Now, [Template(1, today.AddDays(-40))], []);
        Assert.Single(created);
    }

    [Fact]
    public void Idempotens_ha_mar_van_feladat_az_alkalomhoz()
    {
        var today = new DateOnly(2026, 11, 12);
        var t = Template(1, today.AddDays(3));
        var created = OccurrenceGenerator.TasksToCreate(today, Now, [t], [(1, t.NextDueDate)]);
        Assert.Empty(created);
    }

    [Fact]
    public void Kihagyas_utan_a_kovetkezo_alkalom_meg_nem_generalodik()
    {
        var today = new DateOnly(2026, 11, 12);
        var t = Template(1, today.AddDays(3));
        t.Advance(); // törlés = kihagyás → +12 hónap
        Assert.Empty(OccurrenceGenerator.TasksToCreate(today, Now, [t], [(1, today.AddDays(3))]));
    }

    [Fact]
    public void Inaktiv_sablonra_nem_general()
    {
        var today = new DateOnly(2026, 11, 12);
        Assert.Empty(OccurrenceGenerator.TasksToCreate(today, Now, [Template(1, today, active: false)], []));
    }
}

public class BirthdayTests
{
    [Fact]
    public void Feb29_nem_szokoevben_feb28()
    {
        Assert.Equal(new DateOnly(2027, 2, 28), BirthdayCalculator.NextOccurrence(2, 29, new DateOnly(2027, 1, 10)));
        Assert.Equal(new DateOnly(2028, 2, 29), BirthdayCalculator.NextOccurrence(2, 29, new DateOnly(2028, 1, 10)));
    }

    [Fact]
    public void Ev_atfordulas_decemberbol_januarba()
    {
        var today = new DateOnly(2026, 12, 20);
        var b = new Birthday { Id = 1, Name = "Anna", Month = 1, Day = 5, Year = 1990 };
        var (_, date, daysUntil, age) = Assert.Single(BirthdayCalculator.Upcoming([b], today, 30));
        Assert.Equal(new DateOnly(2027, 1, 5), date);
        Assert.Equal(16, daysUntil);
        Assert.Equal(37, age);
    }

    [Fact]
    public void Mai_szuletesnap_0_nap_es_ev_nelkul_nincs_eletkor()
    {
        var today = new DateOnly(2026, 11, 12);
        var b = new Birthday { Id = 1, Name = "Béla", Month = 11, Day = 12 };
        var (_, _, daysUntil, age) = Assert.Single(BirthdayCalculator.Upcoming([b], today, 30));
        Assert.Equal(0, daysUntil);
        Assert.Null(age);
    }

    [Fact]
    public void Elorejelzesen_kivul_nem_jelenik_meg()
    {
        var today = new DateOnly(2026, 11, 12);
        var b = new Birthday { Id = 1, Name = "Csaba", Month = 12, Day = 13 };
        Assert.Empty(BirthdayCalculator.Upcoming([b], today, 30));
        Assert.Single(BirthdayCalculator.Upcoming([b], today, 31));
    }
}
