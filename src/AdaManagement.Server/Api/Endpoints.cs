using System.Security.Claims;
using AdaManagement.Server.Auth;
using AdaManagement.Server.Data;
using AdaManagement.Server.Domain;
using AdaManagement.Shared;
using Microsoft.EntityFrameworkCore;

namespace AdaManagement.Server.Api;

public static class Endpoints
{
    public static void MapApi(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization(AuthSetup.Policy);

        api.MapGet("/me", (ClaimsPrincipal user) =>
            new MeDto(user.FindFirstValue(ClaimTypes.Email)!, user.FindFirstValue(ClaimTypes.Name)));

        api.MapGet("/dashboard", GetDashboard);
        MapTasks(api.MapGroup("/tasks"));
        MapTemplates(api.MapGroup("/templates"));
        MapBirthdays(api.MapGroup("/birthdays"));
        MapSettings(api.MapGroup("/settings"));

        // Ismeretlen /api útvonal ne az index.html-t kapja.
        app.MapFallback("/api/{**rest}", () => Results.NotFound());
    }

    // ---------- Dashboard ----------

    private static async Task<DashboardDto> GetDashboard(AppDbContext db, AppClock clock, CancellationToken ct)
    {
        await GenerateOccurrencesAsync(db, clock, ct);
        var today = clock.Today;
        var settings = await db.Settings.SingleAsync(ct);

        var open = await db.Tasks.Where(t => t.CompletedAt == null)
            .OrderBy(t => t.DueDate == null).ThenBy(t => t.DueDate).ThenBy(t => t.CreatedAt)
            .ToListAsync(ct);

        var birthdays = BirthdayCalculator.Upcoming(await db.Birthdays.ToListAsync(ct), today, settings.BirthdayLookaheadDays)
            .Select(x => new UpcomingBirthdayDto(x.Birthday.Id, x.Birthday.Name, x.Date, x.DaysUntil, x.TurningAge))
            .ToList();

        // A „Közelgő” sávban azok a karbantartások, amelyek már bekerültek a mátrixba (LeadDays-en belül vannak).
        var maintenance = (await db.RecurringTemplates.Where(t => t.IsActive).ToListAsync(ct))
            .Where(t => t.NextDueDate.AddDays(-t.LeadDays) <= today)
            .OrderBy(t => t.NextDueDate)
            .Select(t => new UpcomingMaintenanceDto(t.Id, t.Title, t.NextDueDate, t.NextDueDate.DayNumber - today.DayNumber))
            .ToList();

        return new DashboardDto(today, open.Select(t => t.ToDto()).ToList(), birthdays, maintenance, settings.ToDto());
    }

    public static async Task GenerateOccurrencesAsync(AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var templates = await db.RecurringTemplates.Where(t => t.IsActive).ToListAsync(ct);
        if (templates.Count == 0) return;

        var existing = (await db.Tasks
                .Where(t => t.RecurringTemplateId != null && t.OccurrenceDate != null)
                .Select(t => new { t.RecurringTemplateId, t.OccurrenceDate })
                .ToListAsync(ct))
            .Select(x => (x.RecurringTemplateId!.Value, x.OccurrenceDate!.Value));

        var toCreate = OccurrenceGenerator.TasksToCreate(clock.Today, clock.UtcNow, templates, existing);
        if (toCreate.Count == 0) return;

        db.Tasks.AddRange(toCreate);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Két párhuzamos dashboard-kérés ugyanazt próbálta létrehozni: az egyedi index megfogta, ez rendben van.
            foreach (var t in toCreate) db.Entry(t).State = EntityState.Detached;
        }
    }

    // ---------- Feladatok ----------

    private static void MapTasks(RouteGroupBuilder g)
    {
        g.MapGet("/completed", async (AppDbContext db, string? q, CancellationToken ct) =>
        {
            var query = db.Tasks.Where(t => t.CompletedAt != null);
            if (!string.IsNullOrWhiteSpace(q)) query = query.Where(t => t.Title.Contains(q) || (t.Notes != null && t.Notes.Contains(q)));
            var list = await query.OrderByDescending(t => t.CompletedAt).Take(200).ToListAsync(ct);
            return list.Select(t => t.ToDto());
        });

        g.MapPost("/", async (TaskEditDto dto, AppDbContext db, AppClock clock, CancellationToken ct) =>
        {
            if (Validate.Task(dto) is { Count: > 0 } errors) return Results.ValidationProblem(errors);
            var task = new TaskItem
            {
                Title = dto.Title.Trim(), Notes = dto.Notes.Clean(), IsImportant = dto.IsImportant, IsUrgent = dto.IsUrgent,
                DueDate = dto.DueDate, CreatedAt = clock.UtcNow,
            };
            db.Tasks.Add(task);
            await db.SaveChangesAsync(ct);
            return Results.Ok(task.ToDto());
        });

        g.MapPut("/{id:int}", async (int id, TaskEditDto dto, AppDbContext db, CancellationToken ct) =>
        {
            if (Validate.Task(dto) is { Count: > 0 } errors) return Results.ValidationProblem(errors);
            var task = await db.Tasks.FindAsync([id], ct);
            if (task is null) return Results.NotFound();
            task.Title = dto.Title.Trim();
            task.Notes = dto.Notes.Clean();
            task.IsImportant = dto.IsImportant;
            task.IsUrgent = dto.IsUrgent;
            task.DueDate = dto.DueDate;
            await db.SaveChangesAsync(ct);
            return Results.Ok(task.ToDto());
        });

        // Sima feladat kipipálása. Generált feladatnál az alapértelmezett (fix ütemezésű) következő dátummal zárja az alkalmat.
        g.MapPost("/{id:int}/complete", async (int id, AppDbContext db, AppClock clock, CancellationToken ct) =>
        {
            var task = await db.Tasks.Include(t => t.RecurringTemplate).SingleOrDefaultAsync(t => t.Id == id, ct);
            if (task is null) return Results.NotFound();
            if (!task.IsOpen) return Results.Ok(task.ToDto());

            if (task.RecurringTemplate is { } template && template.NextDueDate == task.OccurrenceDate)
                db.CompletionLog.Add(template.Complete(clock.Today, template.SuggestedFollowingDueDate(), null));

            task.CompletedAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(task.ToDto());
        });

        g.MapPost("/{id:int}/reopen", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var task = await db.Tasks.FindAsync([id], ct);
            if (task is null) return Results.NotFound();
            task.CompletedAt = null;
            await db.SaveChangesAsync(ct);
            return Results.Ok(task.ToDto());
        });

        // Törlés. Nyitott generált feladatnál = az alkalom kihagyása (a sablon továbblép, napló nélkül).
        g.MapDelete("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var task = await db.Tasks.Include(t => t.RecurringTemplate).SingleOrDefaultAsync(t => t.Id == id, ct);
            if (task is null) return Results.NotFound();
            if (task.IsOpen && task.RecurringTemplate is { } template && template.NextDueDate == task.OccurrenceDate)
                template.Advance(); // a sablon továbblép, így ez az alkalom nem generálódik újra
            db.Tasks.Remove(task);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    // ---------- Ismétlődő sablonok ----------

    private static void MapTemplates(RouteGroupBuilder g)
    {
        g.MapGet("/", async (AppDbContext db, CancellationToken ct) =>
            (await db.RecurringTemplates.OrderBy(t => !t.IsActive).ThenBy(t => t.NextDueDate).ToListAsync(ct)).Select(t => t.ToDto()));

        g.MapGet("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
            await db.RecurringTemplates.FindAsync([id], ct) is { } t ? Results.Ok(t.ToDto()) : Results.NotFound());

        g.MapPost("/", async (TemplateEditDto dto, AppDbContext db, CancellationToken ct) =>
        {
            if (Validate.Template(dto) is { Count: > 0 } errors) return Results.ValidationProblem(errors);
            var t = new RecurringTemplate
            {
                Title = dto.Title.Trim(), Notes = dto.Notes.Clean(), IntervalMonths = dto.IntervalMonths,
                LeadDays = dto.LeadDays, IsActive = dto.IsActive,
            };
            t.Reschedule(dto.NextDueDate);
            db.RecurringTemplates.Add(t);
            await db.SaveChangesAsync(ct);
            return Results.Ok(t.ToDto());
        });

        g.MapPut("/{id:int}", async (int id, TemplateEditDto dto, AppDbContext db, CancellationToken ct) =>
        {
            if (Validate.Template(dto) is { Count: > 0 } errors) return Results.ValidationProblem(errors);
            var t = await db.RecurringTemplates.FindAsync([id], ct);
            if (t is null) return Results.NotFound();

            var scheduleChanged = t.NextDueDate != dto.NextDueDate || t.IntervalMonths != dto.IntervalMonths;
            var oldDue = t.NextDueDate;
            t.Title = dto.Title.Trim();
            t.Notes = dto.Notes.Clean();
            t.IntervalMonths = dto.IntervalMonths;
            t.LeadDays = dto.LeadDays;
            t.IsActive = dto.IsActive;
            if (scheduleChanged) t.Reschedule(dto.NextDueDate);

            // A már legenerált, nyitott feladat kövesse az új esedékességet.
            var openTask = await db.Tasks.SingleOrDefaultAsync(x => x.RecurringTemplateId == id && x.OccurrenceDate == oldDue && x.CompletedAt == null, ct);
            if (openTask is not null)
            {
                openTask.Title = t.Title;
                if (scheduleChanged) { openTask.OccurrenceDate = t.NextDueDate; openTask.DueDate = t.NextDueDate; }
            }

            await db.SaveChangesAsync(ct);
            return Results.Ok(t.ToDto());
        });

        g.MapDelete("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var t = await db.RecurringTemplates.FindAsync([id], ct);
            if (t is null) return Results.NotFound();
            db.RecurringTemplates.Remove(t); // a napló vele törlődik, a generált feladatok megmaradnak sablon nélkül
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapGet("/{id:int}/log", async (int id, AppDbContext db, CancellationToken ct) =>
            (await db.CompletionLog.Where(e => e.RecurringTemplateId == id).OrderByDescending(e => e.CompletedOn).ToListAsync(ct))
                .Select(e => e.ToDto()));

        // Az aktuális alkalom teljesítése párbeszédablakból: dátum, megjegyzés, felajánlott/átírt következő esedékesség.
        g.MapPost("/{id:int}/complete", async (int id, CompleteOccurrenceDto dto, AppDbContext db, AppClock clock, CancellationToken ct) =>
        {
            var t = await db.RecurringTemplates.FindAsync([id], ct);
            if (t is null) return Results.NotFound();
            if (dto.NextDueDate <= t.NextDueDate)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["NextDueDate"] = ["A következő esedékesség legyen későbbi a mostaninál."] });

            var due = t.NextDueDate;
            var openTask = await db.Tasks.SingleOrDefaultAsync(x => x.RecurringTemplateId == id && x.OccurrenceDate == due && x.CompletedAt == null, ct);
            if (openTask is not null) openTask.CompletedAt = clock.UtcNow;

            db.CompletionLog.Add(t.Complete(dto.CompletedOn, dto.NextDueDate, dto.Note.Clean()));
            await db.SaveChangesAsync(ct);
            return Results.Ok(t.ToDto());
        });

        // Az utolsó teljesítés visszavonása: ütemezés vissza, naplóbejegyzés törlése, a generált feladat újra nyitott.
        g.MapPost("/{id:int}/undo-last", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var t = await db.RecurringTemplates.FindAsync([id], ct);
            if (t is null) return Results.NotFound();
            var entry = await db.CompletionLog.Where(e => e.RecurringTemplateId == id).OrderByDescending(e => e.Id).FirstOrDefaultAsync(ct);
            if (entry is null) return Results.Conflict();

            // Ha közben már a következő alkalom is legenerálódott (nyitott feladat), az felesleges lesz.
            var newer = await db.Tasks.Where(x => x.RecurringTemplateId == id && x.OccurrenceDate == t.NextDueDate && x.CompletedAt == null).ToListAsync(ct);
            db.Tasks.RemoveRange(newer);

            t.Restore(entry);
            var task = await db.Tasks.FirstOrDefaultAsync(x => x.RecurringTemplateId == id && x.OccurrenceDate == entry.DueDate, ct);
            if (task is not null) task.CompletedAt = null;

            db.CompletionLog.Remove(entry);
            await db.SaveChangesAsync(ct);
            return Results.Ok(t.ToDto());
        });
    }

    // ---------- Születésnapok ----------

    private static void MapBirthdays(RouteGroupBuilder g)
    {
        g.MapGet("/", async (AppDbContext db, CancellationToken ct) =>
            (await db.Birthdays.OrderBy(b => b.Month).ThenBy(b => b.Day).ThenBy(b => b.Name).ToListAsync(ct)).Select(b => b.ToDto()));

        g.MapPost("/", async (BirthdayEditDto dto, AppDbContext db, CancellationToken ct) =>
        {
            if (Validate.Birthday(dto) is { Count: > 0 } errors) return Results.ValidationProblem(errors);
            var b = new Birthday { Name = dto.Name.Trim(), Month = dto.Month, Day = dto.Day, Year = dto.Year, Notes = dto.Notes.Clean() };
            db.Birthdays.Add(b);
            await db.SaveChangesAsync(ct);
            return Results.Ok(b.ToDto());
        });

        g.MapPut("/{id:int}", async (int id, BirthdayEditDto dto, AppDbContext db, CancellationToken ct) =>
        {
            if (Validate.Birthday(dto) is { Count: > 0 } errors) return Results.ValidationProblem(errors);
            var b = await db.Birthdays.FindAsync([id], ct);
            if (b is null) return Results.NotFound();
            b.Name = dto.Name.Trim();
            b.Month = dto.Month;
            b.Day = dto.Day;
            b.Year = dto.Year;
            b.Notes = dto.Notes.Clean();
            await db.SaveChangesAsync(ct);
            return Results.Ok(b.ToDto());
        });

        g.MapDelete("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var b = await db.Birthdays.FindAsync([id], ct);
            if (b is null) return Results.NotFound();
            db.Birthdays.Remove(b);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // Egyszeri import: CSV vagy vCard szöveg. A már meglévő (név + hó + nap) párosokat kihagyja.
        g.MapPost("/import", async (ImportRequestDto dto, AppDbContext db, CancellationToken ct) =>
        {
            if (dto.Content.Length > 1_000_000) return Results.BadRequest("Túl nagy fájl.");
            var (items, skipped) = BirthdayImportParser.Parse(dto.Content);
            var existing = (await db.Birthdays.Select(b => new { b.Name, b.Month, b.Day }).ToListAsync(ct))
                .Select(b => (b.Name.ToLowerInvariant(), b.Month, b.Day)).ToHashSet();

            var imported = 0;
            foreach (var p in items)
            {
                var name = p.Name.Length > 200 ? p.Name[..200] : p.Name;
                if (!existing.Add((name.ToLowerInvariant(), p.Month, p.Day))) { skipped++; continue; }
                db.Birthdays.Add(new Birthday { Name = name, Month = p.Month, Day = p.Day, Year = p.Year });
                imported++;
            }
            await db.SaveChangesAsync(ct);
            return Results.Ok(new ImportResultDto(imported, skipped));
        });
    }

    // ---------- Beállítások ----------

    private static void MapSettings(RouteGroupBuilder g)
    {
        g.MapGet("/", async (AppDbContext db, CancellationToken ct) => (await db.Settings.SingleAsync(ct)).ToDto());

        g.MapPut("/", async (SettingsDto dto, AppDbContext db, CancellationToken ct) =>
        {
            if (Validate.Settings(dto) is { Count: > 0 } errors) return Results.ValidationProblem(errors);
            var s = await db.Settings.SingleAsync(ct);
            s.WeatherLabel = string.IsNullOrWhiteSpace(dto.WeatherLabel) ? "Helyszín" : dto.WeatherLabel.Trim();
            s.Latitude = dto.Latitude;
            s.Longitude = dto.Longitude;
            s.BirthdayLookaheadDays = dto.BirthdayLookaheadDays;
            await db.SaveChangesAsync(ct);
            return Results.Ok(s.ToDto());
        });
    }
}
