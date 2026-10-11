namespace AthleteDay.Contracts;

public sealed record AthleteDto(int Id, string Code, string DisplayName, string Sport);

/// <summary>Master data for squad management (coach) and the athlete page. Never part of the kitchen view.</summary>
public sealed record AthleteDetailsDto(
    int Id,
    string Code,
    string DisplayName,
    string Sport,
    int? BirthYear,
    Sex Sex,
    decimal BodyMassKg,
    DateTimeOffset? ArchivedAt);

public sealed record TrainingSessionDto(
    int Id,
    int AthleteId,
    string AthleteCode,
    DateOnly Date,
    SessionType Type,
    int DurationMinutes,
    int Rpe,
    int Load,
    string LoggedBy);

public sealed record WellnessSummaryDto(DateOnly Date, decimal SleepHours, int Soreness, int Mood);

public sealed record ReadinessDto(ReadinessStatus Status, IReadOnlyList<string> Reasons);

public sealed record NutritionTodayDto(int EnergyKcal, int CarbsG);

/// <summary>One row of the coach dashboard. Contains no free-text health notes.</summary>
public sealed record DashboardRowDto(
    int AthleteId,
    string Code,
    string DisplayName,
    string Sport,
    int? Age,
    Sex Sex,
    int TodayLoad,
    double AcuteLoad,
    double ChronicLoad,
    double? AcuteChronicRatio,
    WellnessSummaryDto? Wellness,
    ReadinessDto Readiness,
    TrainingRestriction Restriction,
    NutritionTodayDto? Nutrition);

public sealed record LoadTrendPointDto(DateOnly Date, int DailyLoad, double Acute7, double Chronic28);

public sealed record LoadTrendDto(int AthleteId, string Code, IReadOnlyList<LoadTrendPointDto> Points);

/// <summary>What the kitchen sees: portions and carbohydrates, nothing about health.</summary>
public sealed record KitchenRowDto(
    string AthleteCode,
    string DisplayName,
    int EnergyKcal,
    int CarbsG,
    string Portion,
    string? KitchenNote);

public sealed record PhysioNoteDto(
    int Id,
    int AthleteId,
    DateOnly Date,
    TrainingRestriction Restriction,
    string Note,
    string Author);

public sealed record MeasurementDto(DateOnly Date, string TestType, decimal Value, string Unit, Guid ImportJobId);

public sealed record ImportRowErrorDto(int RowNumber, string Column, string Reason, string RawLine);

public sealed record ImportJobDto(
    Guid Id,
    string FileName,
    ImportStatus Status,
    string UploadedBy,
    DateTimeOffset UploadedAt,
    DateTimeOffset? CompletedAt,
    int TotalRows,
    int ImportedRows,
    int RejectedRows,
    string? FailureReason,
    IReadOnlyList<ImportRowErrorDto> Errors);

public sealed record AuditEntryDto(
    DateTimeOffset At,
    string User,
    string Role,
    string Action,
    string Resource,
    int? AthleteId);
