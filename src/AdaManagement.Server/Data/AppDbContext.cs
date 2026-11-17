using AdaManagement.Server.Domain;
using AdaManagement.Server.Tournaments.Domain.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AdaManagement.Server.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TaskChecklistItem> ChecklistItems => Set<TaskChecklistItem>();
    public DbSet<TaskStoryEntry> StoryEntries => Set<TaskStoryEntry>();
    public DbSet<RecurringTemplate> RecurringTemplates => Set<RecurringTemplate>();
    public DbSet<CompletionLogEntry> CompletionLog => Set<CompletionLogEntry>();
    public DbSet<Birthday> Birthdays => Set<Birthday>();
    public DbSet<Tradesperson> Tradespeople => Set<Tradesperson>();
    public DbSet<AppSettings> Settings => Set<AppSettings>();

    // Versenyek (az Adaminatorból átvett modul), saját sémában.
    public const string TournamentSchema = "tournament";
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<UnmatchedScoreboard> UnmatchedScoreboard => Set<UnmatchedScoreboard>();

    /// <summary>A cookie-titkosító kulcsok az adatbázisban: így az F1 újraindulásai után is bent maradsz.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // A versenymodul konfigurációi (Tournaments/Data/*Configuration.cs).
        b.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly,
            t => t.Namespace == typeof(Tournaments.Data.TournamentConfiguration).Namespace);

        b.Entity<TaskItem>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.Property(x => x.Icon).HasMaxLength(40);
            e.HasOne(x => x.RecurringTemplate).WithMany().HasForeignKey(x => x.RecurringTemplateId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.RecurringTemplateId, x.OccurrenceDate })
                .IsUnique()
                .HasFilter("[RecurringTemplateId] IS NOT NULL AND [OccurrenceDate] IS NOT NULL");
            e.HasIndex(x => x.CompletedAt);
            // A feladattal együtt törlődnek (adatbázis-szinten is, így a tömeges törlésnél is).
            e.HasMany(x => x.Checklist).WithOne().HasForeignKey(x => x.TaskItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Story).WithOne().HasForeignKey(x => x.TaskItemId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<TaskChecklistItem>().Property(x => x.Text).HasMaxLength(Api.Validate.ChecklistTextMax);
        b.Entity<TaskStoryEntry>().Property(x => x.Text).HasMaxLength(Api.Validate.StoryTextMax);

        b.Entity<RecurringTemplate>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.HasMany(x => x.Log).WithOne().HasForeignKey(x => x.RecurringTemplateId).OnDelete(DeleteBehavior.Cascade);
            // A törölt (visszaállítható) sablonok minden lekérdezésből kimaradnak.
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<CompletionLogEntry>().Property(x => x.Note).HasMaxLength(1000);

        b.Entity<Birthday>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Notes).HasMaxLength(1000);
        });

        b.Entity<Tradesperson>(e =>
        {
            e.Property(x => x.Occupation).HasMaxLength(100);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Phone).HasMaxLength(50);
            e.Property(x => x.Email).HasMaxLength(254);
            e.Property(x => x.Website).HasMaxLength(500);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.HasIndex(x => x.Occupation);
        });

        b.Entity<AppSettings>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.WeatherLabel).HasMaxLength(200);
            e.HasData(new AppSettings());
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        TouchChangedTournaments();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Brings a tournament row into any save that changed only its children, so the row-version check
    /// actually runs.
    ///
    /// The concurrency token lives on the tournament, and EF only puts it in the WHERE clause when a
    /// tournament column is dirty. Almost nothing dirties one: completing a match, undoing it, starting
    /// the playoffs, pairing the next Swiss round and replacing the roster all write children only, so
    /// those saves were leaving the token untested - exactly the writes it was added for. Two requests
    /// could then both draw a playoff from the same group stage and leave the tournament with two Grand
    /// Finals, which every later bracket read fails on. Marking the root modified restores the intended
    /// behaviour: the second writer loses on the version check and gets a 409.
    ///
    /// It hangs off the context rather than a repository so that every save is covered, including one
    /// made through a repository that knows nothing about tournaments.
    /// </summary>
    private void TouchChangedTournaments()
    {
        // Materialised once: each ChangeTracker.Entries() call re-runs change detection over the whole
        // tracked graph, which for a full aggregate is thousands of entities.
        var entries = ChangeTracker.Entries().ToList();
        var changed = new HashSet<Guid>();
        Dictionary<Guid, Guid>? tournamentByMatch = null;

        foreach (var entry in entries)
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            switch (entry.Entity)
            {
                case Participant participant:
                    changed.Add(participant.TournamentId);
                    break;
                case Match match:
                    changed.Add(match.TournamentId);
                    break;
                case ScoreEntry scoreEntry:
                    // A score entry has no navigation to its match, so the owner is looked up by id.
                    // Built on first use: a Best-of-5 result produces ten changed entries (five deleted,
                    // five added) and every one of them would otherwise rescan the tracked matches.
                    tournamentByMatch ??= entries
                        .Select(e => e.Entity)
                        .OfType<Match>()
                        .ToDictionary(m => m.Id, m => m.TournamentId);

                    if (tournamentByMatch.TryGetValue(scoreEntry.MatchId, out var tournamentId))
                    {
                        changed.Add(tournamentId);
                    }

                    break;
            }
        }

        if (changed.Count == 0)
        {
            return;
        }

        foreach (var entry in entries)
        {
            // An added root carries its children in the same INSERT; only an untouched existing one needs it.
            if (entry.Entity is Tournament tournament
                && entry.State == EntityState.Unchanged
                && changed.Contains(tournament.Id))
            {
                entry.State = EntityState.Modified;
            }
        }
    }
}
