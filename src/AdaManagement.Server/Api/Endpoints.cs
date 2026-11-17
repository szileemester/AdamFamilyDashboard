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
        MapTradespeople(api.MapGroup("/tradespeople"));
        MapSettings(api.MapGroup("/settings"));
        api.MapTournaments();

        // Bejelentkezés nélkül, csak olvasásra: a verseny nézői linkje és az Unmatched tábla.
        app.MapGroup("/api/public").AllowAnonymous().MapPublicTournaments();

        // Ismeretlen /api útvonal ne az index.html-t kapja.
        app.MapFallback("/api/{**rest}", () => Results.NotFound());
    }

    // ---------- Dashboard ----------

    private static async Task<DashboardDto> GetDashboard(AppDbContext db, AppClock clock, CancellationToken ct)
    {
        await PurgeDeletedTemplatesAsync(db, clock, ct);
        await GenerateOccurrencesAsync(db, clock, ct);
        var today = clock.Today;
        var settings = await db.Settings.SingleAsync(ct);

        // A törölt (még visszaállítható) sablonok nyitott alkalmai nem látszanak; visszaállításkor újra előjönnek.
        var deletedTemplateIds = db.RecurringTemplates.IgnoreQueryFilters().Where(t => t.DeletedAt != null).Select(t => t.Id);
        var open = await db.Tasks
            .Where(t => t.CompletedAt == null && (t.RecurringTemplateId == null || !deletedTemplateIds.Contains(t.RecurringTemplateId.Value)))
            .OrderBy(t => t.DueDate == null).ThenBy(t => t.DueDate).ThenBy(t => t.CreatedAt)
            .Select(Mapping.TaskToDto)
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

        return new DashboardDto(today, open, birthdays, maintenance, settings.ToDto());
    }

    public static async Task GenerateOccurrencesAsync(AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var today = clock.Today;
        var templates = (await db.RecurringTemplates.Where(t => t.IsActive).ToListAsync(ct))
            .Where(t => t.NextDueDate.AddDays(-t.LeadDays) <= today)
            .ToList();
        if (templates.Count == 0) return;

        // Csak az érintett sablonok mostani (vagy későbbi) alkalmai számítanak – nem kell az összes régi feladat.
        var ids = templates.Select(t => t.Id).ToList();
        var minDue = templates.Min(t => t.NextDueDate);
        var existing = (await db.Tasks
                .Where(t => t.RecurringTemplateId != null && ids.Contains(t.RecurringTemplateId.Value) && t.OccurrenceDate >= minDue)
                .Select(t => new { t.RecurringTemplateId, t.OccurrenceDate })
                .ToListAsync(ct))
            .Select(x => (x.RecurringTemplateId!.Value, x.OccurrenceDate!.Value));

        var toCreate = OccurrenceGenerator.TasksToCreate(today, clock.UtcNow, templates, existing);
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

    /// <summary>
    /// Az egy napnál régebben törölt sablonok végleges eltávolítása (a napló velük törlődik). A nyitott, még el nem
    /// végzett alkalmaik is mennek; a kész feladatok sablon nélkül megmaradnak a Kész listában.
    /// </summary>
    private static async Task PurgeDeletedTemplatesAsync(AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var cutoff = clock.UtcNow.AddDays(-1);
        var ids = await db.RecurringTemplates.IgnoreQueryFilters().Where(x => x.DeletedAt < cutoff).Select(x => x.Id).ToListAsync(ct);
        if (ids.Count == 0) return;
        await db.Tasks.Where(t => t.CompletedAt == null && t.RecurringTemplateId != null && ids.Contains(t.RecurringTemplateId.Value))
            .ExecuteDeleteAsync(ct);
        await db.RecurringTemplates.IgnoreQueryFilters().Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(ct);
    }

    /// <summary>
    /// Egy ismétlődő teljesítés visszavonása: a sablon ütemezése visszaáll, a naplóbejegyzés törlődik, az alkalom
    /// feladata újra nyitott. Ha közben már a következő alkalom is legenerálódott (nyitott feladat), az felesleges lesz.
    /// </summary>
    private static async Task UndoCompletionAsync(AppDbContext db, RecurringTemplate t, CompletionLogEntry entry, CancellationToken ct)
    {
        var newer = await db.Tasks.Where(x => x.RecurringTemplateId == t.Id && x.OccurrenceDate == t.NextDueDate && x.CompletedAt == null).ToListAsync(ct);
        db.Tasks.RemoveRange(newer);

        t.Restore(entry);
        var task = await db.Tasks.FirstOrDefaultAsync(x => x.RecurringTemplateId == t.Id && x.OccurrenceDate == entry.DueDate, ct);
        if (task is not null) task.CompletedAt = null;

        db.CompletionLog.Remove(entry);
    }

    private static Task<CompletionLogEntry?> LastLogEntryAsync(AppDbContext db, int templateId, CancellationToken ct) =>
        db.CompletionLog.Where(e => e.RecurringTemplateId == templateId).OrderByDescending(e => e.Id).FirstOrDefaultAsync(ct);

    // ---------- Feladatok ----------

    private static void MapTasks(RouteGroupBuilder g)
    {
        g.MapGet("/completed", async (AppDbContext db, string? q, CancellationToken ct) =>
        {
            var query = db.Tasks.Where(t => t.CompletedAt != null);
            if (!string.IsNullOrWhiteSpace(q)) query = query.Where(t => t.Title.Contains(q) || (t.Notes != null && t.Notes.Contains(q)));
            return await query.OrderByDescending(t => t.CompletedAt).Take(200).Select(Mapping.TaskToDto).ToListAsync(ct);
        });

        g.MapPost("/", async (TaskEditDto dto, AppDbContext db, AppClock clock, CancellationToken ct) =>
        {
            if (Validate.Task(dto) is { Count: > 0 } errors) return Results.ValidationProblem(errors);
            var task = new TaskItem
            {
                Title = dto.Title.Trim(), Notes = dto.Notes.Clean(), IsImportant = dto.IsImportant, IsUrgent = dto.IsUrgent,
                DueDate = dto.DueDate, Icon = dto.Icon.Clean(), CreatedAt = clock.UtcNow,
            };
            db.Tasks.Add(task);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await TaskDtoAsync(db, task.Id, ct));
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
            task.Icon = dto.Icon.Clean();
            await db.SaveChangesAsync(ct);
            return Results.Ok(await TaskDtoAsync(db, task.Id, ct));
        });

        // Sima feladat kipipálása. Generált feladatnál az alapértelmezett (fix ütemezésű) következő dátummal zárja az alkalmat.
        g.MapPost("/{id:int}/complete", async (int id, AppDbContext db, AppClock clock, CancellationToken ct) =>
        {
            var task = await db.Tasks.Include(t => t.RecurringTemplate).SingleOrDefaultAsync(t => t.Id == id, ct);
            if (task is null) return Results.NotFound();
            if (!task.IsOpen) return Results.Ok(await TaskDtoAsync(db, task.Id, ct));

            if (task.RecurringTemplate is { } template && template.NextDueDate == task.OccurrenceDate)
                db.CompletionLog.Add(template.Complete(clock.Today, template.SuggestedFollowingDueDate(), null));

            task.CompletedAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(await TaskDtoAsync(db, task.Id, ct));
        });

        // Visszanyitás (a „Kész” visszavonása is ez). Generált feladatnál, ha ez volt a sablon utolsó teljesítése,
        // a sablon ütemezése is visszaáll – különben a sablon már a következő alkalomnál tartana.
        g.MapPost("/{id:int}/reopen", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var task = await db.Tasks.Include(t => t.RecurringTemplate).SingleOrDefaultAsync(t => t.Id == id, ct);
            if (task is null) return Results.NotFound();
            if (task.IsOpen) return Results.Ok(await TaskDtoAsync(db, task.Id, ct));
            if (task.RecurringTemplate is { } template &&
                await LastLogEntryAsync(db, template.Id, ct) is { } last && last.DueDate == task.OccurrenceDate)
                await UndoCompletionAsync(db, template, last, ct);
            task.CompletedAt = null;
            await db.SaveChangesAsync(ct);
            return Results.Ok(await TaskDtoAsync(db, task.Id, ct));
        });

        MapTaskStory(g);

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

    private static Task<TaskDto> TaskDtoAsync(AppDbContext db, int id, CancellationToken ct) =>
        db.Tasks.Where(t => t.Id == id).Select(Mapping.TaskToDto).SingleAsync(ct);

    // ---------- Feladat-sztori: mini teendők és időbélyeges bejegyzések ----------

    /// <summary>
    /// A teendők és bejegyzések azonnal mentődnek (nem a feladat Mentés gombjával). Kész feladatnál csak olvashatók:
    /// előbb vissza kell nyitni. Az utolsó teendő kipipálása sem zárja le a feladatot.
    /// </summary>
    private static void MapTaskStory(RouteGroupBuilder g)
    {
        g.MapGet("/{id:int}/story", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var items = await db.ChecklistItems.Where(i => i.TaskItemId == id).OrderBy(i => i.Position).ThenBy(i => i.Id).ToListAsync(ct);
            var entries = await db.StoryEntries.Where(e => e.TaskItemId == id).OrderByDescending(e => e.At).ThenByDescending(e => e.Id).ToListAsync(ct);
            // Csak üres listáknál kell külön megnézni, hogy a feladat egyáltalán létezik-e.
            if (items.Count == 0 && entries.Count == 0 && !await db.Tasks.AnyAsync(t => t.Id == id, ct)) return Results.NotFound();
            return Results.Ok(new TaskStoryDto(items.Select(i => i.ToDto()).ToList(), entries.Select(e => e.ToDto()).ToList()));
        });

        g.MapPost("/{id:int}/checklist", async (int id, ChecklistItemEditDto dto, AppDbContext db, CancellationToken ct) =>
        {
            if (Validate.ChecklistItem(dto) is { Count: > 0 } errors) return Results.ValidationProblem(errors);
            // Egy lekérdezésben: nyitott-e a feladat, hány teendője van, és mi az utolsó pozíció.
            var state = await db.Tasks.Where(t => t.Id == id)
                .Select(t => new { Open = t.CompletedAt == null, Count = t.Checklist.Count, Last = t.Checklist.Max(i => (int?)i.Position) })
                .SingleOrDefaultAsync(ct);
            if (EditableTaskError(state?.Open) is { } error) return error;
            if (state!.Count >= Validate.ChecklistMaxItems)
                return Problem("Text", $"Egy feladaton legfeljebb {Validate.ChecklistMaxItems} teendő lehet.");
            var item = new TaskChecklistItem { TaskItemId = id, Text = dto.Text.Trim(), IsDone = dto.IsDone, Position = (state.Last ?? 0) + 1 };
            db.ChecklistItems.Add(item);
            await db.SaveChangesAsync(ct);
            return Results.Ok(item.ToDto());
        });

        g.MapPut("/{id:int}/checklist/{itemId:int}", async (int id, int itemId, ChecklistItemEditDto dto, AppDbContext db, CancellationToken ct) =>
        {
            if (Validate.ChecklistItem(dto) is { Count: > 0 } errors) return Results.ValidationProblem(errors);
            if (await EditableTaskErrorAsync(db, id, ct) is { } error) return error;
            var item = await db.ChecklistItems.SingleOrDefaultAsync(i => i.Id == itemId && i.TaskItemId == id, ct);
            if (item is null) return Results.NotFound();
            item.Text = dto.Text.Trim();
            item.IsDone = dto.IsDone;
            await db.SaveChangesAsync(ct);
            return Results.Ok(item.ToDto());
        });

        g.MapDelete("/{id:int}/checklist/{itemId:int}", async (int id, int itemId, AppDbContext db, CancellationToken ct) =>
        {
            if (await EditableTaskErrorAsync(db, id, ct) is { } error) return error;
            var deleted = await db.ChecklistItems.Where(i => i.Id == itemId && i.TaskItemId == id).ExecuteDeleteAsync(ct);
            return deleted == 0 ? Results.NotFound() : Results.NoContent();
        });

        g.MapPost("/{id:int}/entries", async (int id, StoryEntryEditDto dto, AppDbContext db, CancellationToken ct) =>
        {
            if (Validate.StoryEntry(dto) is { Count: > 0 } errors) return Results.ValidationProblem(errors);
            if (await EditableTaskErrorAsync(db, id, ct) is { } error) return error;
            var entry = new TaskStoryEntry { TaskItemId = id, At = dto.At, Text = dto.Text.Trim() };
            db.StoryEntries.Add(entry);
            await db.SaveChangesAsync(ct);
            return Results.Ok(entry.ToDto());
        });

        g.MapPut("/{id:int}/entries/{entryId:int}", async (int id, int entryId, StoryEntryEditDto dto, AppDbContext db, CancellationToken ct) =>
        {
            if (Validate.StoryEntry(dto) is { Count: > 0 } errors) return Results.ValidationProblem(errors);
            if (await EditableTaskErrorAsync(db, id, ct) is { } error) return error;
            var entry = await db.StoryEntries.SingleOrDefaultAsync(e => e.Id == entryId && e.TaskItemId == id, ct);
            if (entry is null) return Results.NotFound();
            entry.At = dto.At;
            entry.Text = dto.Text.Trim();
            await db.SaveChangesAsync(ct);
            return Results.Ok(entry.ToDto());
        });

        g.MapDelete("/{id:int}/entries/{entryId:int}", async (int id, int entryId, AppDbContext db, CancellationToken ct) =>
        {
            if (await EditableTaskErrorAsync(db, id, ct) is { } error) return error;
            var deleted = await db.StoryEntries.Where(e => e.Id == entryId && e.TaskItemId == id).ExecuteDeleteAsync(ct);
            return deleted == 0 ? Results.NotFound() : Results.NoContent();
        });
    }

    /// <summary>Null, ha a feladat létezik és nyitott; különben a megfelelő hibaválasz.</summary>
    private static async Task<IResult?> EditableTaskErrorAsync(AppDbContext db, int id, CancellationToken ct) =>
        EditableTaskError(await db.Tasks.Where(t => t.Id == id).Select(t => (bool?)(t.CompletedAt == null)).SingleOrDefaultAsync(ct));

    /// <summary>A feladat nyitottsága alapján (null = nincs ilyen feladat).</summary>
    private static IResult? EditableTaskError(bool? open) => open switch
    {
        null => Results.NotFound(),
        false => Problem("Task", "Kész feladat nem módosítható; előbb nyisd vissza."),
        true => null,
    };

    private const string OccurrenceTaken = "Erre a napra már van egy alkalom ennél a feladatnál. Válassz másik dátumot.";

    private static IResult Problem(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

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

        g.MapPut("/{id:int}", async (int id, TemplateEditDto dto, AppDbContext db, AppClock clock, CancellationToken ct) =>
        {
            if (Validate.Template(dto) is { Count: > 0 } errors) return Results.ValidationProblem(errors);
            var t = await db.RecurringTemplates.FindAsync([id], ct);
            if (t is null) return Results.NotFound();

            var oldDue = t.NextDueDate;
            var dateChanged = oldDue != dto.NextDueDate;
            // Erre a napra már volt (pl. teljesített) alkalom: az egyedi index miatt nem lehet még egy.
            if (dateChanged && await db.Tasks.AnyAsync(x => x.RecurringTemplateId == id && x.OccurrenceDate == dto.NextDueDate, ct))
                return Problem("NextDueDate", OccurrenceTaken);

            t.Title = dto.Title.Trim();
            t.Notes = dto.Notes.Clean();
            t.LeadDays = dto.LeadDays;
            t.IsActive = dto.IsActive;
            if (dateChanged)
            {
                t.IntervalMonths = dto.IntervalMonths;
                t.Reschedule(dto.NextDueDate);
            }
            else if (t.IntervalMonths != dto.IntervalMonths)
                t.ChangeInterval(dto.IntervalMonths); // a hónap napja (pl. 31.) megmarad

            // A már legenerált, nyitott feladat kövesse az új esedékességet; ha az új dátum még messze van, kikerül
            // a mátrixból (időben újra bekerül).
            var openTask = await db.Tasks.SingleOrDefaultAsync(x => x.RecurringTemplateId == id && x.OccurrenceDate == oldDue && x.CompletedAt == null, ct);
            if (openTask is not null)
            {
                if (dateChanged && t.NextDueDate.AddDays(-t.LeadDays) > clock.Today)
                    db.Tasks.Remove(openTask);
                else
                {
                    openTask.Title = t.Title;
                    if (dateChanged) { openTask.OccurrenceDate = t.NextDueDate; openTask.DueDate = t.NextDueDate; }
                }
            }

            await db.SaveChangesAsync(ct);
            return Results.Ok(t.ToDto());
        });

        // Törlés: először csak megjelöli (a Visszavonás gomb visszaállíthatja), a nyitott alkalma addig rejtve marad.
        // Az egy napnál régebben törölteket véglegesen eltávolítja (ugyanígy a dashboard lekérésekor is).
        g.MapDelete("/{id:int}", async (int id, AppDbContext db, AppClock clock, CancellationToken ct) =>
        {
            var t = await db.RecurringTemplates.FindAsync([id], ct);
            if (t is null) return Results.NotFound();
            t.DeletedAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
            await PurgeDeletedTemplatesAsync(db, clock, ct);
            return Results.NoContent();
        });

        g.MapPost("/{id:int}/restore", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var t = await db.RecurringTemplates.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id, ct);
            if (t is null) return Results.NotFound();
            t.DeletedAt = null;
            await db.SaveChangesAsync(ct);
            return Results.Ok(t.ToDto());
        });

        g.MapGet("/{id:int}/log", async (int id, AppDbContext db, CancellationToken ct) =>
            (await db.CompletionLog.Where(e => e.RecurringTemplateId == id).OrderByDescending(e => e.CompletedOn).ToListAsync(ct))
                .Select(e => e.ToDto()));

        // Az aktuális alkalom teljesítése párbeszédablakból: dátum, megjegyzés, felajánlott/átírt következő esedékesség.
        g.MapPost("/{id:int}/complete", async (int id, CompleteOccurrenceDto dto, AppDbContext db, AppClock clock, CancellationToken ct) =>
        {
            if (Validate.Completion(dto) is { Count: > 0 } errors) return Results.ValidationProblem(errors);
            var t = await db.RecurringTemplates.FindAsync([id], ct);
            if (t is null) return Results.NotFound();
            if (dto.NextDueDate <= t.NextDueDate)
                return Problem("NextDueDate", "A következő esedékesség legyen későbbi a mostaninál.");
            if (await db.Tasks.AnyAsync(x => x.RecurringTemplateId == id && x.OccurrenceDate == dto.NextDueDate, ct))
                return Problem("NextDueDate", OccurrenceTaken);

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
            if (await LastLogEntryAsync(db, id, ct) is not { } entry) return Results.Conflict();
            await UndoCompletionAsync(db, t, entry, ct);
            await db.SaveChangesAsync(ct);
            return Results.Ok(t.ToDto());
        });
    }

    // ---------- Születésnapok ----------

    private static void MapBirthdays(RouteGroupBuilder g)
    {
        g.MapGet("/", async (AppDbContext db, CancellationToken ct) =>
            (await db.Birthdays.OrderBy(b => b.Month).ThenBy(b => b.Day).ThenBy(b => b.Name).ToListAsync(ct)).Select(b => b.ToDto()));

        g.MapPost("/", async (BirthdayEditDto dto, AppDbContext db, AppClock clock, CancellationToken ct) =>
        {
            if (Validate.Birthday(dto, clock.Today) is { Count: > 0 } errors) return Results.ValidationProblem(errors);
            var b = new Birthday { Name = dto.Name.Trim(), Month = dto.Month, Day = dto.Day, Year = dto.Year, Notes = dto.Notes.Clean() };
            db.Birthdays.Add(b);
            await db.SaveChangesAsync(ct);
            return Results.Ok(b.ToDto());
        });

        g.MapPut("/{id:int}", async (int id, BirthdayEditDto dto, AppDbContext db, AppClock clock, CancellationToken ct) =>
        {
            if (Validate.Birthday(dto, clock.Today) is { Count: > 0 } errors) return Results.ValidationProblem(errors);
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
    }

    // ---------- Szakik ----------

    private static void MapTradespeople(RouteGroupBuilder g)
    {
        g.MapGet("/", async (AppDbContext db, CancellationToken ct) =>
            (await db.Tradespeople.OrderBy(t => t.Occupation).ThenBy(t => t.Name).ToListAsync(ct)).Select(t => t.ToDto()));

        g.MapPost("/", async (TradespersonEditDto dto, AppDbContext db, CancellationToken ct) =>
        {
            if (Validate.Tradesperson(dto) is { Count: > 0 } errors) return Results.ValidationProblem(errors);
            var t = new Tradesperson { Occupation = "", Name = "" };
            Apply(t, dto);
            db.Tradespeople.Add(t);
            await db.SaveChangesAsync(ct);
            return Results.Ok(t.ToDto());
        });

        g.MapPut("/{id:int}", async (int id, TradespersonEditDto dto, AppDbContext db, CancellationToken ct) =>
        {
            if (Validate.Tradesperson(dto) is { Count: > 0 } errors) return Results.ValidationProblem(errors);
            var t = await db.Tradespeople.FindAsync([id], ct);
            if (t is null) return Results.NotFound();
            Apply(t, dto);
            await db.SaveChangesAsync(ct);
            return Results.Ok(t.ToDto());
        });

        g.MapDelete("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var t = await db.Tradespeople.FindAsync([id], ct);
            if (t is null) return Results.NotFound();
            db.Tradespeople.Remove(t);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        static void Apply(Tradesperson t, TradespersonEditDto dto)
        {
            t.Occupation = dto.Occupation.Trim();
            t.Name = dto.Name.Trim();
            t.Phone = dto.Phone.Clean();
            t.Email = dto.Email.Clean();
            t.Website = dto.Website.Clean();
            t.Notes = dto.Notes.Clean();
        }
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
