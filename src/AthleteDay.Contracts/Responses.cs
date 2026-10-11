namespace AthleteDay.Contracts;

/// <summary>A response about one athlete. The audit log reads the id from it when the route has none (e.g. a create).</summary>
public interface IAthleteScoped
{
    int AthleteId { get; }
}

public sealed record AthleteDto(int Id, string Code, string DisplayName, string Sport);

/// <summary>Master data for squad management (coach) and the athlete page. Never part of the kitchen view.</summary>
public sealed record AthleteDetailsDto(
    int Id,
    string Code,
    string DisplayName,
    int SportId,
    string Sport,
    int? BirthYear,
    Sex Sex,
    decimal BodyMassKg,
    DateTimeOffset? ArchivedAt) : IAthleteScoped
{
    int IAthleteScoped.AthleteId => Id;
}

public sealed record TrainingSessionDto(
    int Id,
    int AthleteId,
    string AthleteCode,
    DateOnly Date,
    SessionType Type,
    int DurationMinutes,
    int Rpe,
    int Load,
    string LoggedBy) : IAthleteScoped;

public sealed record WellnessSummaryDto(DateOnly Date, decimal SleepHours, int Soreness, int Mood);

/// <summary>One morning check in the wellness history (spec F11).</summary>
public sealed record WellnessDayDto(DateOnly Date, decimal SleepHours, int Soreness, int Mood, IReadOnlyList<string> Flags);

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
    NutritionTodayDto? Nutrition,
    DashboardRangeDto Range);

/// <summary>
/// Figures over the chosen period (both dates included). Average daily load counts rest days as 0;
/// wellness averages cover the days with a morning check.
/// </summary>
public sealed record DashboardRangeDto(
    DateOnly From,
    DateOnly To,
    int Days,
    int TotalLoad,
    double AverageDailyLoad,
    int Sessions,
    int WellnessDays,
    decimal? AverageSleep,
    double? AverageSoreness,
    double? AverageMood);

/// <summary>An entry of the managed sports list (ADR-10). Retired sports stay on existing athletes.</summary>
public sealed record SportDto(int Id, string Name, bool Retired);

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
    string Author) : IAthleteScoped;

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

// ---- Athlete space (spec v1.7) --------------------------------------------------------------------------

/// <summary>One week, Monday to Sunday. Wellness averages are null in a week without morning checks.</summary>
public sealed record WeeklyProgressDto(
    DateOnly WeekStart,
    int TotalLoad,
    int Sessions,
    decimal? AverageSleep,
    double? AverageSoreness,
    double? AverageMood);

/// <summary>A metric ("load", "sessions", "sleep", "soreness", "mood"): last 4 weeks against the 4 before.</summary>
public sealed record TrendDto(string Metric, double? Recent, double? Previous, Trend Trend);

public sealed record ProgressDto(int AthleteId, IReadOnlyList<WeeklyProgressDto> Weeks, IReadOnlyList<TrendDto> Trends);

public sealed record TestResultDto(DateOnly Date, decimal Value);

public sealed record GoalDto(string TestType, decimal Target, DateOnly? TargetDate, string? Note, string SetBy);

/// <summary>
/// One device test of an athlete. Gap to goal: positive = still to go, zero or negative = reached.
/// History holds the results within the requested period.
/// </summary>
public sealed record BenchmarkDto(
    string TestType,
    string Unit,
    bool HigherIsBetter,
    TestResultDto Latest,
    TestResultDto Best,
    TestResultDto? FirstInPeriod,
    decimal? ChangeInPeriod,
    decimal? SquadAverage,
    GoalDto? Goal,
    decimal? GapToGoal,
    GoalStatus? Status,
    IReadOnlyList<TestResultDto> History);

public sealed record SessionNoteDto(
    int Id,
    int SessionId,
    int AthleteId,
    DateOnly SessionDate,
    SessionType SessionType,
    int SessionLoad,
    string Text,
    string Author,
    DateTimeOffset CreatedAt) : IAthleteScoped;

/// <summary>The figures of a review's period, stored when the review is written (ADR-13).</summary>
public sealed record ReviewSnapshotDto(
    IReadOnlyList<WeeklyProgressDto> Weeks,
    int TotalLoad,
    int Sessions,
    decimal? AverageSleep,
    double? AverageSoreness,
    double? AverageMood,
    IReadOnlyList<BenchmarkDto> Benchmarks);

public sealed record ProgressReviewDto(
    int Id,
    int AthleteId,
    DateOnly From,
    DateOnly To,
    string? Observations,
    string? Achievements,
    string? Challenges,
    string? Recommendations,
    string Author,
    DateTimeOffset CreatedAt,
    ReviewSnapshotDto Snapshot) : IAthleteScoped;

// ---- Training plans (spec v1.8, ADR-14) ------------------------------------------------------------------

public sealed record PlannedSessionDto(SessionType Type, int DurationMinutes, int Rpe, string? Note, int PlannedLoad);

/// <summary>Weekdays are numbered 1 (Monday) to 7 (Sunday).</summary>
public sealed record PlanTemplateDayDto(int DayOfWeek, IReadOnlyList<PlannedSessionDto> Sessions);

/// <summary>The standard week: always seven days, some of them without sessions.</summary>
public sealed record PlanTemplateDto(int AthleteId, IReadOnlyList<PlanTemplateDayDto> Days);

public sealed record PlanDayDto(
    DateOnly Date,
    int DayOfWeek,
    PlanDaySource Source,
    string? Reason,
    IReadOnlyList<PlannedSessionDto> Sessions,
    int PlannedLoad,
    int LoggedLoad,
    int LoggedSessions);

public sealed record WeekPlanDto(int AthleteId, DateOnly WeekStart, IReadOnlyList<PlanDayDto> Days, int PlannedLoad, int LoggedLoad);
