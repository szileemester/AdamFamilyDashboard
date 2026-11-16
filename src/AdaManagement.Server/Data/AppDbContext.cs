using AdaManagement.Server.Domain;
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

    /// <summary>A cookie-titkosító kulcsok az adatbázisban: így az F1 újraindulásai után is bent maradsz.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder b)
    {
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
}
