using System.Text.Json.Serialization;

namespace AthleteDay.Contracts;

// Enums travel as strings ("OnIce", not 3) so the Angular client and the API stay readable and stable.

[JsonConverter(typeof(JsonStringEnumConverter<SessionType>))]
public enum SessionType
{
    Strength,
    Speed,
    Endurance,
    OnIce,
    Technique,
    Recovery
}

[JsonConverter(typeof(JsonStringEnumConverter<TrainingRestriction>))]
public enum TrainingRestriction
{
    None,
    Modified,
    NoTraining
}

[JsonConverter(typeof(JsonStringEnumConverter<ReadinessStatus>))]
public enum ReadinessStatus
{
    Ok,
    CheckIn,
    Missing
}

[JsonConverter(typeof(JsonStringEnumConverter<ImportStatus>))]
public enum ImportStatus
{
    Queued,
    Processing,
    Completed,
    Failed
}

[JsonConverter(typeof(JsonStringEnumConverter<Sex>))]
public enum Sex
{
    Female,
    Male,
    NotStated
}

/// <summary>Last 4 weeks against the 4 before (spec section 4).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Trend>))]
public enum Trend
{
    Up,
    Down,
    Stable
}

[JsonConverter(typeof(JsonStringEnumConverter<GoalStatus>))]
public enum GoalStatus
{
    Open,
    Reached,
    Behind
}

/// <summary>Whether a plan day follows the standard week or was changed for that date (ADR-14).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlanDaySource>))]
public enum PlanDaySource
{
    Standard,
    Changed
}

/// <summary>The kind of an athlete document; it decides who may see it (spec section 4, ADR-15).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DocumentType>))]
public enum DocumentType
{
    Medical,
    GovernmentId,
    Certificate,
    General
}
