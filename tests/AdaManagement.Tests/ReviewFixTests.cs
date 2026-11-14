using AdaManagement.Server.Api;
using AdaManagement.Server.Domain;
using AdaManagement.Shared;

namespace AdaManagement.Tests;

/// <summary>A teljes kódátnézés után pótolt esetek: intervallumváltás, visszavonás tartalék-ága, budapesti nap határa,
/// születésnapi életkor, LeadDays = 0 és a bemenet-ellenőrzés.</summary>
public class ReviewFixTests
{
    private static RecurringTemplate Template(DateOnly next, int months)
    {
        var t = new RecurringTemplate { Title = "Olajcsere", IntervalMonths = months };
        t.Reschedule(next);
        return t;
    }

    // ---------- Intervallum váltása változatlan dátummal ----------

    [Fact]
    public void Intervallum_valtas_megtartja_a_honap_vegi_napot()
    {
        var t = Template(new DateOnly(2026, 1, 31), 1);
        t.Advance(); // feb 28 (31-éről levágva)
        t.ChangeInterval(2);

        Assert.Equal(new DateOnly(2026, 2, 28), t.NextDueDate);
        Assert.Equal(2, t.IntervalMonths);
        t.Advance();
        Assert.Equal(new DateOnly(2026, 4, 30), t.NextDueDate);
        t.Advance();
        t.Advance();
        Assert.Equal(new DateOnly(2026, 8, 31), t.NextDueDate); // újra 31-e, nem ragadt 28-ra
    }

    [Fact]
    public void Intervallum_valtas_honap_kozepen_uj_horgonyt_allit()
    {
        var t = Template(new DateOnly(2026, 1, 15), 1);
        t.Advance();
        t.Advance(); // márc 15
        t.ChangeInterval(3);

        Assert.Equal(new DateOnly(2026, 3, 15), t.NextDueDate);
        Assert.Equal(new DateOnly(2026, 6, 15), t.SuggestedFollowingDueDate());
    }

    // ---------- Visszavonás ----------

    [Fact]
    public void Visszavonas_regi_bejegyzesnel_legalabb_az_esedekesseg_visszaall()
    {
        var t = Template(new DateOnly(2026, 5, 1), 6);
        var entry = new CompletionLogEntry { DueDate = new DateOnly(2026, 5, 1), CompletedOn = new DateOnly(2026, 5, 2) }; // nincs Previous*
        t.MoveTo(t.SuggestedFollowingDueDate());

        t.Restore(entry);

        Assert.Equal(new DateOnly(2026, 5, 1), t.NextDueDate);
        Assert.Equal(new DateOnly(2026, 11, 1), t.SuggestedFollowingDueDate());
    }

    [Fact]
    public void Visszavonas_intervallum_valtas_utan_is_a_regi_esedekesseget_adja()
    {
        var t = Template(new DateOnly(2026, 5, 1), 6);
        var entry = t.Complete(new DateOnly(2026, 5, 1), t.SuggestedFollowingDueDate(), null);
        t.ChangeInterval(12);

        t.Restore(entry);

        Assert.Equal(new DateOnly(2026, 5, 1), t.NextDueDate);
    }

    // ---------- Generálás ----------

    [Fact]
    public void LeadDays_0_csak_az_esedekesseg_napjan_general()
    {
        var t = Template(new DateOnly(2026, 6, 10), 12);
        t.Id = 1;
        t.LeadDays = 0;

        Assert.Empty(OccurrenceGenerator.TasksToCreate(new DateOnly(2026, 6, 9), DateTime.UtcNow, [t], []));
        Assert.Single(OccurrenceGenerator.TasksToCreate(new DateOnly(2026, 6, 10), DateTime.UtcNow, [t], []));
    }

    // ---------- Budapesti „ma” ----------

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Theory]
    [InlineData("2026-03-28T22:59:00Z", "2026-03-28")] // télen UTC+1
    [InlineData("2026-03-28T23:00:00Z", "2026-03-29")]
    [InlineData("2026-07-01T21:59:00Z", "2026-07-01")] // nyáron UTC+2
    [InlineData("2026-07-01T22:00:00Z", "2026-07-02")]
    [InlineData("2026-10-25T22:59:00Z", "2026-10-25")] // az óraátállítás napján már újra UTC+1
    [InlineData("2026-10-25T23:00:00Z", "2026-10-26")]
    public void A_mai_nap_budapesti_ido_szerint(string utc, string expected)
    {
        var clock = new AppClock(new FixedTime(DateTimeOffset.Parse(utc)));
        Assert.Equal(DateOnly.Parse(expected), clock.Today);
    }

    // ---------- Születésnap ----------

    [Theory]
    [InlineData(2025, 2026, 1)]
    [InlineData(2026, 2026, null)] // a születés éve: még nincs „0 éves lesz”
    [InlineData(2030, 2026, null)] // hibás, jövőbeli év: nincs negatív életkor
    [InlineData(null, 2026, null)]
    public void Eletkor_sosem_negativ(int? birthYear, int occurrenceYear, int? expected) =>
        Assert.Equal(expected, BirthdayDates.TurningAge(birthYear, new DateOnly(occurrenceYear, 6, 1)));

    [Fact]
    public void Feb29_szokoevben_feb29_kovetkezo_nem_szokoevben_feb28()
    {
        Assert.Equal(new DateOnly(2028, 2, 29), BirthdayDates.NextOccurrence(2, 29, new DateOnly(2028, 1, 10)));
        Assert.Equal(new DateOnly(2029, 2, 28), BirthdayDates.NextOccurrence(2, 29, new DateOnly(2028, 3, 1)));
    }

    // ---------- Bemenet-ellenőrzés ----------

    [Fact]
    public void Tul_hosszu_szoveg_es_szelsoseges_datum_hibat_ad()
    {
        var e = Validate.Task(new TaskEditDto(new string('a', 201), new string('b', 2001), false, false, new DateOnly(1, 1, 1), null));
        Assert.Contains("Title", e.Keys);
        Assert.Contains("Notes", e.Keys);
        Assert.Contains("DueDate", e.Keys);

        var t = Validate.Template(new TemplateEditDto("Kazán", null, 12, new DateOnly(9999, 12, 31), 14, true));
        Assert.Contains("NextDueDate", t.Keys);

        var c = Validate.Completion(new CompleteOccurrenceDto(new DateOnly(2026, 1, 1), DateOnly.MaxValue, new string('x', 1001)));
        Assert.Contains("NextDueDate", c.Keys);
        Assert.Contains("Note", c.Keys);
    }

    [Fact]
    public void Ervenyes_bemenet_atmegy()
    {
        Assert.Empty(Validate.Task(new TaskEditDto("Olajcsere", null, true, false, new DateOnly(2026, 12, 1), null)));
        Assert.Empty(Validate.Template(new TemplateEditDto("Kazán", "éves", 12, new DateOnly(2027, 3, 1), 14, true)));
        Assert.Empty(Validate.Birthday(new BirthdayEditDto("Anna", 2, 29, 1990, null), new DateOnly(2026, 11, 14)));
        Assert.Empty(Validate.Settings(new SettingsDto("Budapest", 47.5, 19.04, 30)));
    }

    [Fact]
    public void Jovobeli_szuletesi_ev_es_nem_szam_koordinata_hibas()
    {
        Assert.Contains("Year", Validate.Birthday(new BirthdayEditDto("Anna", 5, 1, 2027, null), new DateOnly(2026, 11, 14)).Keys);
        Assert.Contains("Latitude", Validate.Settings(new SettingsDto("X", double.NaN, 19, 30)).Keys);
    }
}
