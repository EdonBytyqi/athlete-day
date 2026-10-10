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
