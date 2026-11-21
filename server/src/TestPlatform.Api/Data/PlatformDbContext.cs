using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TestPlatform.Api.Domain;

namespace TestPlatform.Api.Data;

public class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options)
{
    public DbSet<TargetEnvironment> Environments => Set<TargetEnvironment>();
    public DbSet<Run> Runs => Set<Run>();
    public DbSet<TestCase> TestCases => Set<TestCase>();
    public DbSet<ScenarioResult> ScenarioResults => Set<ScenarioResult>();
    public DbSet<Evidence> Evidence => Set<Evidence>();
    public DbSet<KnownBug> KnownBugs => Set<KnownBug>();
    public DbSet<TestDataRecord> TestData => Set<TestDataRecord>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Setting> Settings => Set<Setting>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Enums as readable text in the database ("ExpectedFailure", not 4).
        builder.Properties<RunTrigger>().HaveConversion<string>();
        builder.Properties<RunStatus>().HaveConversion<string>();
        builder.Properties<ScenarioStatus>().HaveConversion<string>();
        builder.Properties<EvidenceKind>().HaveConversion<string>();
        builder.Properties<TestDataKind>().HaveConversion<string>();
        builder.Properties<TestDataState>().HaveConversion<string>();
        // SQLite has no DateTimeOffset type: store as ISO-8601 text that sorts chronologically (UTC).
        builder.Properties<DateTimeOffset>().HaveConversion<UtcIsoConverter>();
        builder.Properties<DateTimeOffset?>().HaveConversion<UtcIsoConverter>();
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<TargetEnvironment>(e =>
        {
            e.ToTable("Environments");
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.BaseUrl).HasMaxLength(500);
        });

        model.Entity<Run>(e =>
        {
            e.HasIndex(x => new { x.EnvironmentId, x.CreatedAt });
            e.HasIndex(x => x.Status);
            e.Ignore(x => x.IsFullSuite);
        });

        model.Entity<TestCase>(e =>
        {
            e.HasIndex(x => x.Key).IsUnique();
        });

        model.Entity<ScenarioResult>(e =>
        {
            e.HasOne(x => x.Run).WithMany(r => r.Results).HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.TestCase).WithMany(t => t.Results).HasForeignKey(x => x.TestCaseId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.RunId, x.TestCaseId }).IsUnique();
            e.HasIndex(x => new { x.TestCaseId, x.FinishedAt });
        });

        model.Entity<Evidence>(e =>
        {
            e.HasOne(x => x.ScenarioResult).WithMany(r => r.Evidence).HasForeignKey(x => x.ScenarioResultId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<KnownBug>(e => e.HasKey(x => x.Id));

        model.Entity<TestDataRecord>(e =>
        {
            e.ToTable("TestData");
            e.HasOne(x => x.Run).WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.EnvironmentBaseUrl, x.State });
            e.HasIndex(x => new { x.Kind, x.ExternalId, x.EnvironmentBaseUrl });
        });

        model.Entity<Schedule>(e =>
        {
            e.HasOne(x => x.Environment).WithMany().HasForeignKey(x => x.EnvironmentId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<Setting>(e => e.HasKey(x => x.Key));
    }
}

/// <summary>DateTimeOffset ⇄ "2026-11-21T10:15:30.1234567+00:00" (always UTC, so text order = time order).</summary>
public class UtcIsoConverter() : ValueConverter<DateTimeOffset, string>(
    v => v.ToUniversalTime().ToString("O"),
    v => DateTimeOffset.Parse(v, null, System.Globalization.DateTimeStyles.RoundtripKind));
