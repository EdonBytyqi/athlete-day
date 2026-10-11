using AthleteDay.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AthleteDay.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Sport> Sports => Set<Sport>();
    public DbSet<Athlete> Athletes => Set<Athlete>();
    public DbSet<TrainingSession> TrainingSessions => Set<TrainingSession>();
    public DbSet<SessionNote> SessionNotes => Set<SessionNote>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<ProgressReview> ProgressReviews => Set<ProgressReview>();
    public DbSet<PlanTemplateSession> PlanTemplateSessions => Set<PlanTemplateSession>();
    public DbSet<PlanDayChange> PlanDayChanges => Set<PlanDayChange>();
    public DbSet<WellnessCheck> WellnessChecks => Set<WellnessCheck>();
    public DbSet<NutritionTarget> NutritionTargets => Set<NutritionTarget>();
    public DbSet<PhysioNote> PhysioNotes => Set<PhysioNote>();
    public DbSet<DeviceMeasurement> DeviceMeasurements => Set<DeviceMeasurement>();
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();
    public DbSet<ImportRowError> ImportRowErrors => Set<ImportRowError>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        // Sessions, targets and notes of an archived athlete drop out of navigation queries (e.g. the kitchen view).
        // That is exactly what ADR-07 wants, so EF Core's warning about it is noise here.
        optionsBuilder.ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Enums are stored as readable strings, which also keeps the hand-written T-SQL reports clear.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<string>().HaveMaxLength(200);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Athlete>(e =>
        {
            e.HasIndex(a => a.Code).IsUnique();
            e.Property(a => a.Code).HasMaxLength(16);
            e.Property(a => a.BodyMassKg).HasPrecision(5, 1);
            e.HasOne(a => a.Sport).WithMany().HasForeignKey(a => a.SportId).OnDelete(DeleteBehavior.Restrict);
            // Archive instead of delete (ADR-07): every query, form and import treats an archived athlete as unknown.
            // Restore and the archive list opt out with IgnoreQueryFilters().
            e.HasQueryFilter(a => a.ArchivedAt == null);
        });

        b.Entity<Sport>(e =>
        {
            // Unique ignoring case: SQL Server's default collation is case-insensitive.
            e.Property(s => s.Name).HasMaxLength(60);
            e.HasIndex(s => s.Name).IsUnique();
        });

        b.Entity<TrainingSession>(e =>
        {
            // Covers the dashboard report: sessions per athlete within a date window.
            e.HasIndex(s => new { s.AthleteId, s.Date }).IncludeProperties(s => new { s.Rpe, s.DurationMinutes });
        });

        b.Entity<SessionNote>(e =>
        {
            e.Property(n => n.Text).HasMaxLength(1000);
            e.HasOne(n => n.Session).WithMany().HasForeignKey(n => n.SessionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Athlete>().WithMany().HasForeignKey(n => n.AthleteId).OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(n => new { n.AthleteId, n.CreatedAt });
        });

        b.Entity<Goal>(e =>
        {
            e.Property(g => g.TestType).HasMaxLength(32);
            e.Property(g => g.Target).HasPrecision(9, 2);
            e.HasOne<Athlete>().WithMany().HasForeignKey(g => g.AthleteId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(g => new { g.AthleteId, g.TestType }).IsUnique();
        });

        b.Entity<ProgressReview>(e =>
        {
            foreach (var text in new[] { nameof(ProgressReview.Observations), nameof(ProgressReview.Achievements), nameof(ProgressReview.Challenges), nameof(ProgressReview.Recommendations) })
                e.Property(text).HasMaxLength(2000);
            e.Property(r => r.SnapshotJson).HasMaxLength(-1); // nvarchar(max)
            e.HasOne<Athlete>().WithMany().HasForeignKey(r => r.AthleteId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => new { r.AthleteId, r.CreatedAt });
        });

        b.Entity<PlanTemplateSession>(e =>
        {
            e.HasOne<Athlete>().WithMany().HasForeignKey(t => t.AthleteId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(t => new { t.AthleteId, t.DayOfWeek, t.Position });
        });

        b.Entity<PlanDayChange>(e =>
        {
            e.HasOne<Athlete>().WithMany().HasForeignKey(c => c.AthleteId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(c => new { c.AthleteId, c.Date }).IsUnique();
            // The sessions belong to the change and live and die with it.
            e.OwnsMany(c => c.Sessions, s => s.ToTable("PlanDayChangeSessions"));
        });

        b.Entity<WellnessCheck>(e =>
        {
            e.Property(w => w.SleepHours).HasPrecision(4, 1);
            e.HasIndex(w => new { w.AthleteId, w.Date }).IsUnique();
        });

        b.Entity<NutritionTarget>(e =>
        {
            e.Property(n => n.CarbsGPerKg).HasPrecision(4, 1);
            e.HasIndex(n => new { n.AthleteId, n.Date }).IsUnique();
        });

        b.Entity<PhysioNote>(e =>
        {
            e.Property(p => p.Note).HasMaxLength(1000);
            e.HasIndex(p => new { p.AthleteId, p.Date });
        });

        b.Entity<DeviceMeasurement>(e =>
        {
            e.Property(m => m.Value).HasPrecision(9, 2);
            e.Property(m => m.TestType).HasMaxLength(32);
            e.Property(m => m.Unit).HasMaxLength(8);
            e.HasIndex(m => new { m.AthleteId, m.Date, m.TestType });
        });

        b.Entity<ImportJob>(e =>
        {
            e.Property(j => j.FailureReason).HasMaxLength(1000);
            e.HasMany(j => j.Errors).WithOne().HasForeignKey(x => x.ImportJobId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(j => j.UploadedAt);
        });

        b.Entity<ImportRowError>(e =>
        {
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.RawLine).HasMaxLength(1000);
        });

        b.Entity<AuditEntry>(e => e.HasIndex(a => a.At));
    }
}
