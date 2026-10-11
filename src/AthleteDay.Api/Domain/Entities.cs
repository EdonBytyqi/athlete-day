using AthleteDay.Contracts;

namespace AthleteDay.Api.Domain;

// Synthetic data only: athletes are pseudonymous codes (ATH-001), never real people.

public sealed class Athlete
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string DisplayName { get; set; }

    /// <summary>From the managed sports list (ADR-10).</summary>
    public int SportId { get; set; }
    public Sport Sport { get; set; } = null!;

    public decimal BodyMassKg { get; set; }

    /// <summary>Only the year (data minimisation); null for athletes created before it was recorded.</summary>
    public int? BirthYear { get; set; }
    public Sex Sex { get; set; } = Sex.NotStated;

    /// <summary>Set when a coach archives the athlete. Archived athletes are hidden by a global query filter.</summary>
    public DateTimeOffset? ArchivedAt { get; set; }
}

/// <summary>An entry of the sports list, managed by the admin. Retired sports stay on existing athletes.</summary>
public sealed class Sport
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset? RetiredAt { get; set; }
}

public sealed class TrainingSession
{
    public int Id { get; set; }
    public int AthleteId { get; set; }
    public Athlete Athlete { get; set; } = null!;
    public DateOnly Date { get; set; }
    public SessionType Type { get; set; }
    public int DurationMinutes { get; set; }
    public int Rpe { get; set; }
    public string? Notes { get; set; }
    public required string LoggedBy { get; set; }
    public DateTimeOffset LoggedAt { get; set; }
}

public sealed class WellnessCheck
{
    public int Id { get; set; }
    public int AthleteId { get; set; }
    public Athlete Athlete { get; set; } = null!;
    public DateOnly Date { get; set; }
    public decimal SleepHours { get; set; }
    public int Soreness { get; set; }
    public int Mood { get; set; }
}

public sealed class NutritionTarget
{
    public int Id { get; set; }
    public int AthleteId { get; set; }
    public Athlete Athlete { get; set; } = null!;
    public DateOnly Date { get; set; }
    public int EnergyKcal { get; set; }
    public decimal CarbsGPerKg { get; set; }
    public string? KitchenNote { get; set; }
    public required string SetBy { get; set; }
}

public sealed class PhysioNote
{
    public int Id { get; set; }
    public int AthleteId { get; set; }
    public Athlete Athlete { get; set; } = null!;
    public DateOnly Date { get; set; }
    public TrainingRestriction Restriction { get; set; }
    public required string Note { get; set; }
    public required string Author { get; set; }
}

public sealed class DeviceMeasurement
{
    public long Id { get; set; }
    public int AthleteId { get; set; }
    public Athlete Athlete { get; set; } = null!;
    public DateOnly Date { get; set; }
    public required string TestType { get; set; }
    public decimal Value { get; set; }
    public required string Unit { get; set; }
    public Guid ImportJobId { get; set; }
}

public sealed class ImportJob
{
    public Guid Id { get; set; }
    public required string FileName { get; set; }
    public required string BlobName { get; set; }
    public ImportStatus Status { get; set; }
    public required string UploadedBy { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int TotalRows { get; set; }
    public int ImportedRows { get; set; }
    public int RejectedRows { get; set; }
    public string? FailureReason { get; set; }
    public List<ImportRowError> Errors { get; set; } = [];
}

public sealed class ImportRowError
{
    public long Id { get; set; }
    public Guid ImportJobId { get; set; }
    public int RowNumber { get; set; }
    public required string Column { get; set; }
    public required string Reason { get; set; }
    public required string RawLine { get; set; }
}

/// <summary>Who looked at which health-related data, and when.</summary>
public sealed class AuditEntry
{
    public long Id { get; set; }
    public DateTimeOffset At { get; set; }
    public required string User { get; set; }
    public required string Role { get; set; }
    public required string Action { get; set; }
    public required string Resource { get; set; }
    public int? AthleteId { get; set; }
}
