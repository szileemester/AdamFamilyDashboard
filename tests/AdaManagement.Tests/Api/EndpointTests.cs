using System.Net;
using System.Net.Http.Json;
using AdaManagement.Shared;

namespace AdaManagement.Tests.Api;

/// <summary>
/// Végpont-tesztek valódi SQL Serveren (Docker kell hozzá). Minden teszt a saját maga létrehozta adatokat nézi,
/// mert az adatbázis a tesztek között közös.
/// </summary>
[Collection(ApiCollection.Name)]
public class EndpointTests(ApiFixture fx)
{
    private static TemplateEditDto TemplateDto(string title, DateOnly next, int months = 6, int leadDays = 14, bool active = true) =>
        new(title, null, months, next, leadDays, active);

    private static TaskEditDto TaskDto(string title, DateOnly? due = null) => new(title, null, false, false, due, null);

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..Math.Min(prefix.Length + 9, 200)];

    /// <summary>Sablon, amelynek az alkalma azonnal bekerül a mátrixba (esedékesség 3 nap múlva, 14 nap előre).</summary>
    private async Task<(HttpClient Api, DateOnly Today, TemplateDto Template, TaskDto Task)> GeneratedAsync(string title, int months = 6)
    {
        var api = await fx.OwnerAsync();
        var today = (await api.Dashboard()).Today;
        var template = await api.Post<TemplateDto>("api/templates", TemplateDto(Unique(title), today.AddDays(3), months));
        var task = (await api.Dashboard()).OpenTasks.Single(t => t.RecurringTemplateId == template.Id);
        return (api, today, template, task);
    }

    private static async Task<List<CompletionLogDto>> Log(HttpClient api, int templateId) =>
        await api.Get<List<CompletionLogDto>>($"api/templates/{templateId}/log");

    // ---------- Hozzáférés, CSRF, biztonság ----------

    [Fact]
    public async Task Bejelentkezes_nelkul_az_api_401()
    {
        var r = await fx.Anonymous().GetAsync("api/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Csrf_fejlec_nelkul_a_modositas_400()
    {
        var api = await fx.OwnerAsync();
        api.DefaultRequestHeaders.Remove("X-Ada-Client");
        var r = await api.PostAsJsonAsync("api/tasks", TaskDto("csrf"));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Theory]
    [InlineData("/%09/evil.com", "/")]
    [InlineData("//evil.com", "/")]
    [InlineData("/%5Cevil.com", "/")]
    [InlineData("https://evil.com/", "/")]
    [InlineData("/kesz", "/kesz")]
    public async Task Bejelentkezes_utan_csak_sajat_oldalra_iranyit(string returnUrl, string expected)
    {
        var r = await fx.Anonymous().GetAsync($"/account/dev-login?returnUrl={returnUrl}");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal(expected, r.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Idegen_oldalrol_nem_lehet_kileptetni()
    {
        var api = await fx.OwnerAsync();
        var foreign = new HttpRequestMessage(HttpMethod.Post, "/account/logout");
        foreign.Headers.Add("Origin", "https://evil.example");
        Assert.Equal(HttpStatusCode.BadRequest, (await api.SendAsync(foreign)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.GetAsync("api/me")).StatusCode); // még bent van

        var own = new HttpRequestMessage(HttpMethod.Post, "/account/logout");
        own.Headers.Add("Origin", "http://localhost");
        var r = await api.SendAsync(own);
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAsync("api/me")).StatusCode);
    }

    [Fact]
    public async Task Biztonsagi_fejlecek_es_ismeretlen_api_utvonal()
    {
        var api = await fx.OwnerAsync();
        var r = await api.GetAsync("api/nincs-ilyen");
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.Equal("frame-ancestors 'none'", r.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("DENY", r.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("nosniff", r.Headers.GetValues("X-Content-Type-Options").Single());
    }

    // ---------- Feladatok ----------

    [Fact]
    public async Task Feladat_kesz_visszanyitas_es_torles()
    {
        var api = await fx.OwnerAsync();
        var title = Unique("Számla befizetés");
        var task = await api.Post<TaskDto>("api/tasks", TaskDto(title));
        Assert.Contains((await api.Dashboard()).OpenTasks, t => t.Id == task.Id);

        await api.Post<TaskDto>($"api/tasks/{task.Id}/complete");
        Assert.DoesNotContain((await api.Dashboard()).OpenTasks, t => t.Id == task.Id);
        var found = await api.Get<List<TaskDto>>($"api/tasks/completed?q={Uri.EscapeDataString(title)}");
        Assert.Equal(task.Id, Assert.Single(found).Id);

        await api.Post<TaskDto>($"api/tasks/{task.Id}/reopen");
        Assert.Contains((await api.Dashboard()).OpenTasks, t => t.Id == task.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await api.DeleteAsync($"api/tasks/{task.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.PostAsync($"api/tasks/{task.Id}/complete", null)).StatusCode);
    }

    // ---------- Ismétlődő: generálás ----------

    [Fact]
    public async Task Generalas_a_fontos_nem_surgos_negyedbe_es_idempotens()
    {
        var (api, today, template, task) = await GeneratedAsync("Kazán");
        Assert.True(task.IsImportant);
        Assert.False(task.IsUrgent);
        Assert.Equal(today.AddDays(3), task.DueDate);
        Assert.Equal(today.AddDays(3), task.OccurrenceDate);

        Assert.Single((await api.Dashboard()).OpenTasks, t => t.RecurringTemplateId == template.Id);
        Assert.Contains((await api.Dashboard()).Maintenance, m => m.TemplateId == template.Id && m.DaysUntil == 3);
    }

    [Fact]
    public async Task Esedekesseg_elott_LeadDays_nappal_meg_nem_general()
    {
        var api = await fx.OwnerAsync();
        var today = (await api.Dashboard()).Today;
        var template = await api.Post<TemplateDto>("api/templates", TemplateDto(Unique("Távoli"), today.AddDays(15), leadDays: 14));
        Assert.DoesNotContain((await api.Dashboard()).OpenTasks, t => t.RecurringTemplateId == template.Id);
    }

    // ---------- Ismétlődő: kész, visszavonás ----------

    [Fact]
    public async Task Sima_kesz_lepteti_a_sablont_a_visszanyitas_visszaallitja()
    {
        var (api, today, template, task) = await GeneratedAsync("Olajcsere");

        await api.Post<TaskDto>($"api/tasks/{task.Id}/complete");
        var advanced = await api.Get<TemplateDto>($"api/templates/{template.Id}");
        Assert.Equal(today.AddDays(3).AddMonths(6), advanced.NextDueDate);
        Assert.Single(await Log(api, template.Id));

        await api.Post<TaskDto>($"api/tasks/{task.Id}/reopen");
        var restored = await api.Get<TemplateDto>($"api/templates/{template.Id}");
        Assert.Equal(today.AddDays(3), restored.NextDueDate);
        Assert.Equal(template.SuggestedFollowingDueDate, restored.SuggestedFollowingDueDate);
        Assert.Empty(await Log(api, template.Id));
        Assert.Single((await api.Dashboard()).OpenTasks, t => t.RecurringTemplateId == template.Id);
    }

    [Fact]
    public async Task Teljesites_ablakbol_es_visszavonas()
    {
        var (api, today, template, task) = await GeneratedAsync("Szűrőcsere");

        var done = await api.Post<TemplateDto>($"api/templates/{template.Id}/complete",
            new CompleteOccurrenceDto(today, template.SuggestedFollowingDueDate, "kész, minden rendben"));
        Assert.Equal(template.SuggestedFollowingDueDate, done.NextDueDate);
        Assert.DoesNotContain((await api.Dashboard()).OpenTasks, t => t.Id == task.Id);
        Assert.Equal("kész, minden rendben", Assert.Single(await Log(api, template.Id)).Note);

        var undone = await api.Post<TemplateDto>($"api/templates/{template.Id}/undo-last");
        Assert.Equal(today.AddDays(3), undone.NextDueDate);
        Assert.Empty(await Log(api, template.Id));
        Assert.Contains((await api.Dashboard()).OpenTasks, t => t.Id == task.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await api.PostAsync($"api/templates/{template.Id}/undo-last", null)).StatusCode);
    }

    [Fact]
    public async Task Visszavonas_torli_a_kozben_legeneralt_kovetkezo_alkalmat()
    {
        var (api, today, template, task) = await GeneratedAsync("Havi", months: 1);

        // A következő alkalom kézzel közelre (10 nap) → a dashboard azonnal legenerálja.
        await api.Post<TemplateDto>($"api/templates/{template.Id}/complete", new CompleteOccurrenceDto(today, today.AddDays(10), null));
        var newer = (await api.Dashboard()).OpenTasks.Single(t => t.RecurringTemplateId == template.Id);
        Assert.Equal(today.AddDays(10), newer.OccurrenceDate);

        await api.Post<TemplateDto>($"api/templates/{template.Id}/undo-last");
        var open = (await api.Dashboard()).OpenTasks.Where(t => t.RecurringTemplateId == template.Id).ToList();
        Assert.Equal(task.Id, Assert.Single(open).Id);
    }

    [Fact]
    public async Task Generalt_feladat_torlese_kihagyja_az_alkalmat_naplo_nelkul()
    {
        var (api, today, template, task) = await GeneratedAsync("Kihagyható");

        await api.DeleteAsync($"api/tasks/{task.Id}");
        var skipped = await api.Get<TemplateDto>($"api/templates/{template.Id}");
        Assert.Equal(today.AddDays(3).AddMonths(6), skipped.NextDueDate);
        Assert.Empty(await Log(api, template.Id));
        Assert.DoesNotContain((await api.Dashboard()).OpenTasks, t => t.RecurringTemplateId == template.Id);
    }

    [Fact]
    public async Task Teljesitesnel_a_kovetkezo_datum_kesobbi_legyen()
    {
        var (api, today, template, _) = await GeneratedAsync("Rossz dátum");
        var r = await api.PostAsJsonAsync($"api/templates/{template.Id}/complete", new CompleteOccurrenceDto(today, template.NextDueDate, null));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    // ---------- Ismétlődő: törlés, visszaállítás, végleges törlés ----------

    [Fact]
    public async Task Torolt_sablon_alkalma_rejtve_visszaallitva_ujra_latszik()
    {
        var (api, _, template, task) = await GeneratedAsync("Törölhető");

        await api.DeleteAsync($"api/templates/{template.Id}");
        Assert.Equal(HttpStatusCode.NotFound, (await api.GetAsync($"api/templates/{template.Id}")).StatusCode);
        Assert.DoesNotContain((await api.Dashboard()).OpenTasks, t => t.Id == task.Id);
        Assert.DoesNotContain((await api.Dashboard()).Maintenance, m => m.TemplateId == template.Id);

        await api.Post<TemplateDto>($"api/templates/{template.Id}/restore");
        Assert.Contains((await api.Dashboard()).OpenTasks, t => t.Id == task.Id);

        // Visszaállítás után rendesen teljesíthető (nem ragad be).
        await api.Post<TaskDto>($"api/tasks/{task.Id}/complete");
        Assert.True((await api.Get<TemplateDto>($"api/templates/{template.Id}")).NextDueDate > task.OccurrenceDate);
    }

    [Fact]
    public async Task Egy_nap_utan_vegleges_torles_a_kesz_feladat_megmarad()
    {
        var (api, today, template, task) = await GeneratedAsync("Végleg törölt");
        // Egy kész alkalom (előzmény) + egy nyitott: a teljesítés kézzel közeli következő dátummal.
        await api.Post<TemplateDto>($"api/templates/{template.Id}/complete", new CompleteOccurrenceDto(today, today.AddDays(5), null));
        var open = (await api.Dashboard()).OpenTasks.Single(t => t.RecurringTemplateId == template.Id);

        await api.DeleteAsync($"api/templates/{template.Id}");
        fx.Clock.Advance(TimeSpan.FromDays(1.5));
        await api.Dashboard(); // a dashboard lekérése végzi a végleges törlést

        Assert.Equal(HttpStatusCode.NotFound, (await api.PostAsync($"api/templates/{template.Id}/restore", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.PostAsync($"api/tasks/{open.Id}/complete", null)).StatusCode);
        var history = await api.Get<List<TaskDto>>($"api/tasks/completed?q={Uri.EscapeDataString(task.Title)}");
        var kept = Assert.Single(history);
        Assert.Equal(task.Id, kept.Id);
        Assert.Null(kept.RecurringTemplateId);
    }

    // ---------- Ismétlődő: szerkesztés ----------

    [Fact]
    public async Task Szerkesztes_cimvaltas_a_nyitott_feladatra_is_atmegy()
    {
        var (api, today, template, task) = await GeneratedAsync("Régi cím");
        var newTitle = Unique("Új cím");
        await api.Put<TemplateDto>($"api/templates/{template.Id}", TemplateDto(newTitle, template.NextDueDate) with { LeadDays = template.LeadDays });
        Assert.Equal(newTitle, (await api.Dashboard()).OpenTasks.Single(t => t.Id == task.Id).Title);

        // Közeli új dátum: a nyitott feladat vele mozog.
        await api.Put<TemplateDto>($"api/templates/{template.Id}", TemplateDto(newTitle, today.AddDays(6)));
        var moved = (await api.Dashboard()).OpenTasks.Single(t => t.Id == task.Id);
        Assert.Equal(today.AddDays(6), moved.DueDate);
        Assert.Equal(today.AddDays(6), moved.OccurrenceDate);
    }

    [Fact]
    public async Task Szerkesztes_messzi_datumra_kiveszi_a_matrixbol()
    {
        var (api, today, template, task) = await GeneratedAsync("Messzire");
        await api.Put<TemplateDto>($"api/templates/{template.Id}", TemplateDto(template.Title, today.AddDays(200)));
        Assert.DoesNotContain((await api.Dashboard()).OpenTasks, t => t.RecurringTemplateId == template.Id);
    }

    [Fact]
    public async Task Szerkesztes_mar_teljesitett_alkalom_napjara_400_es_nem_500()
    {
        var (api, today, template, _) = await GeneratedAsync("Ütköző");
        var doneOn = template.NextDueDate;
        await api.Post<TemplateDto>($"api/templates/{template.Id}/complete", new CompleteOccurrenceDto(today, today.AddDays(40), null));

        var r = await api.PutAsJsonAsync($"api/templates/{template.Id}", TemplateDto(template.Title, doneOn));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Csak_intervallum_valtasnal_a_honap_vegi_nap_megmarad()
    {
        var api = await fx.OwnerAsync();
        var template = await api.Post<TemplateDto>("api/templates", TemplateDto(Unique("Hónap vége"), new DateOnly(2030, 1, 31), months: 1, leadDays: 0));
        var feb = await api.Post<TemplateDto>($"api/templates/{template.Id}/complete",
            new CompleteOccurrenceDto(new DateOnly(2030, 1, 31), template.SuggestedFollowingDueDate, null));
        Assert.Equal(new DateOnly(2030, 2, 28), feb.NextDueDate);

        var changed = await api.Put<TemplateDto>($"api/templates/{template.Id}", TemplateDto(template.Title, feb.NextDueDate, months: 2, leadDays: 0));
        Assert.Equal(new DateOnly(2030, 2, 28), changed.NextDueDate);
        Assert.Equal(new DateOnly(2030, 4, 30), changed.SuggestedFollowingDueDate);
    }

    // ---------- Születésnapok, beállítások ----------

    [Fact]
    public async Task Kozeledo_szuletesnap_eletkorral_a_dashboardon()
    {
        var api = await fx.OwnerAsync();
        var today = (await api.Dashboard()).Today;
        var date = today.AddDays(5);
        var b = await api.Post<BirthdayDto>("api/birthdays", new BirthdayEditDto(Unique("Anna"), date.Month, date.Day, date.Year - 40, null));

        var upcoming = Assert.Single((await api.Dashboard()).Birthdays, x => x.Id == b.Id);
        Assert.Equal(5, upcoming.DaysUntil);
        Assert.Equal(40, upcoming.TurningAge);
    }

    [Fact]
    public async Task Beallitasok_mentese_es_visszaolvasasa()
    {
        var api = await fx.OwnerAsync();
        var original = await api.Get<SettingsDto>("api/settings");
        try
        {
            var saved = await api.Put<SettingsDto>("api/settings", new SettingsDto("Szeged", 46.253, 20.1414, 45));
            Assert.Equal(saved, await api.Get<SettingsDto>("api/settings"));
            Assert.Equal(45, (await api.Dashboard()).Settings.BirthdayLookaheadDays);
        }
        finally
        {
            await api.Put<SettingsDto>("api/settings", original);
        }
    }

    // ---------- Szakik ----------

    [Fact]
    public async Task Szaki_felvetel_modositas_torles()
    {
        var api = await fx.OwnerAsync();
        var name = Unique("Kovács Béla");
        var created = await api.Post<TradespersonDto>("api/tradespeople",
            new TradespersonEditDto("  Villanyszerelő ", name, "+36 30 123 4567", "bela@pelda.hu", "villany-bela.hu", "  "));

        Assert.Equal("Villanyszerelő", created.Occupation); // levágva
        Assert.Null(created.Notes);                         // üres → null
        Assert.Contains(await api.Get<List<TradespersonDto>>("api/tradespeople"), t => t.Id == created.Id);

        var updated = await api.Put<TradespersonDto>($"api/tradespeople/{created.Id}",
            new TradespersonEditDto("Vízszerelő", name, null, null, null, "Gyors és pontos"));
        Assert.Equal(("Vízszerelő", (string?)null, "Gyors és pontos"), (updated.Occupation, updated.Phone, updated.Notes));

        Assert.Equal(HttpStatusCode.NoContent, (await api.DeleteAsync($"api/tradespeople/{created.Id}")).StatusCode);
        Assert.DoesNotContain(await api.Get<List<TradespersonDto>>("api/tradespeople"), t => t.Id == created.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await api.DeleteAsync($"api/tradespeople/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Szakik_bejelentkezes_nelkul_nem_erhetok_el()
    {
        var r = await fx.Anonymous().GetAsync("api/tradespeople");
        Assert.NotEqual(HttpStatusCode.OK, r.StatusCode);
    }

    // ---------- Bemenet-ellenőrzés: 400, nem 500 ----------

    public static TheoryData<string, string, object> InvalidInputs => new()
    {
        { "POST", "api/tasks", new TaskEditDto(new string('a', 201), null, false, false, null, null) },
        { "POST", "api/tasks", new TaskEditDto("ok", new string('b', 2001), false, false, null, null) },
        { "POST", "api/tasks", new TaskEditDto("ok", null, false, false, new DateOnly(9999, 12, 31), null) },
        { "POST", "api/tasks", new TaskEditDto("ok", null, false, false, new DateOnly(1, 1, 1), null) },
        { "POST", "api/templates", new TemplateEditDto("ok", null, 6, new DateOnly(9999, 1, 1), 14, true) },
        { "POST", "api/templates", new TemplateEditDto(new string('t', 201), null, 6, new DateOnly(2030, 1, 1), 14, true) },
        { "POST", "api/templates", new TemplateEditDto("ok", null, 0, new DateOnly(2030, 1, 1), 14, true) },
        { "POST", "api/birthdays", new BirthdayEditDto("Jövőbeli", 5, 1, 2999, null) },
        { "POST", "api/birthdays", new BirthdayEditDto("Rossz nap", 2, 30, null, null) },
        { "PUT", "api/settings", new SettingsDto(new string('h', 201), 47.5, 19.04, 30) },
        { "PUT", "api/settings", new SettingsDto("Budapest", 95, 19.04, 30) },
        { "POST", "api/tradespeople", new TradespersonEditDto(" ", "Béla", null, null, null, null) },
        { "POST", "api/tradespeople", new TradespersonEditDto("Festő", "", null, null, null, null) },
        { "POST", "api/tradespeople", new TradespersonEditDto(new string('f', 101), "Béla", null, null, null, null) },
        { "POST", "api/tradespeople", new TradespersonEditDto("Festő", "Béla", "hívj fel", null, null, null) },
        { "POST", "api/tradespeople", new TradespersonEditDto("Festő", "Béla", null, "nem-email", null, null) },
        { "POST", "api/tradespeople", new TradespersonEditDto("Festő", "Béla", null, null, "javascript:alert(1)", null) },
    };

    [Theory]
    [MemberData(nameof(InvalidInputs))]
    public async Task Hibas_bemenet_400(string method, string url, object body)
    {
        var api = await fx.OwnerAsync();
        var content = JsonContent.Create(body, body.GetType());
        var r = method == "PUT" ? await api.PutAsync(url, content) : await api.PostAsync(url, content);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Tul_hosszu_teljesitesi_megjegyzes_400()
    {
        var (api, today, template, _) = await GeneratedAsync("Hosszú megjegyzés");
        var r = await api.PostAsJsonAsync($"api/templates/{template.Id}/complete",
            new CompleteOccurrenceDto(today, template.SuggestedFollowingDueDate, new string('n', 1001)));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }
}
