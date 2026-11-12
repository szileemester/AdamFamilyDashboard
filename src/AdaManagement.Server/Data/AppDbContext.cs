using AdaManagement.Server.Domain;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AdaManagement.Server.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<RecurringTemplate> RecurringTemplates => Set<RecurringTemplate>();
    public DbSet<CompletionLogEntry> CompletionLog => Set<CompletionLogEntry>();
    public DbSet<Birthday> Birthdays => Set<Birthday>();
    public DbSet<AppSettings> Settings => Set<AppSettings>();

    /// <summary>A cookie-titkosító kulcsok az adatbázisban: így az F1 újraindulásai után is bent maradsz.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<TaskItem>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.HasOne(x => x.RecurringTemplate).WithMany().HasForeignKey(x => x.RecurringTemplateId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.RecurringTemplateId, x.OccurrenceDate })
                .IsUnique()
                .HasFilter("[RecurringTemplateId] IS NOT NULL AND [OccurrenceDate] IS NOT NULL");
            e.HasIndex(x => x.CompletedAt);
        });

        b.Entity<RecurringTemplate>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.HasMany(x => x.Log).WithOne().HasForeignKey(x => x.RecurringTemplateId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<CompletionLogEntry>().Property(x => x.Note).HasMaxLength(1000);

        b.Entity<Birthday>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Notes).HasMaxLength(1000);
        });

        b.Entity<AppSettings>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.WeatherLabel).HasMaxLength(200);
            e.HasData(new AppSettings());
        });
    }
}
