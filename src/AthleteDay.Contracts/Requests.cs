using System.ComponentModel.DataAnnotations;

namespace AthleteDay.Contracts;

// These request types are shared by the API and the Blazor staff app,
// so the same validation rules run in the browser and on the server.

public sealed class LogSessionRequest : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "Select an athlete.")]
    public int AthleteId { get; set; }

    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [EnumDataType(typeof(SessionType))]
    public SessionType Type { get; set; } = SessionType.Strength;

    [Range(5, 300, ErrorMessage = "Duration must be between 5 and 300 minutes.")]
    public int DurationMinutes { get; set; } = 60;

    [Range(1, 10, ErrorMessage = "RPE must be between 1 and 10.")]
    public int Rpe { get; set; } = 5;

    [StringLength(500)]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        DateRules.NotInFuture(Date, nameof(Date));
}

public sealed class WellnessCheckRequest : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "Select an athlete.")]
    public int AthleteId { get; set; }

    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Range(0, 14, ErrorMessage = "Sleep must be between 0 and 14 hours.")]
    public decimal SleepHours { get; set; } = 8;

    /// <summary>1 = no soreness, 5 = very sore.</summary>
    [Range(1, 5, ErrorMessage = "Soreness must be between 1 and 5.")]
    public int Soreness { get; set; } = 2;

    /// <summary>1 = very low, 5 = very good.</summary>
    [Range(1, 5, ErrorMessage = "Mood must be between 1 and 5.")]
    public int Mood { get; set; } = 4;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        DateRules.NotInFuture(Date, nameof(Date));
}

public sealed class NutritionTargetRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Select an athlete.")]
    public int AthleteId { get; set; }

    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Range(1200, 7000, ErrorMessage = "Energy must be between 1200 and 7000 kcal.")]
    public int EnergyKcal { get; set; } = 3000;

    [Range(typeof(decimal), "2", "12", ErrorMessage = "Carbohydrates must be between 2 and 12 g/kg.")]
    public decimal CarbsGPerKg { get; set; } = 5;

    /// <summary>Shown to the kitchen. Preferences only, never medical details.</summary>
    [StringLength(200)]
    public string? KitchenNote { get; set; }
}

public sealed class PhysioNoteRequest : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "Select an athlete.")]
    public int AthleteId { get; set; }

    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [EnumDataType(typeof(TrainingRestriction))]
    public TrainingRestriction Restriction { get; set; } = TrainingRestriction.None;

    [Required(ErrorMessage = "A note is required.")]
    [StringLength(1000, MinimumLength = 3)]
    public string Note { get; set; } = "";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        DateRules.NotInFuture(Date, nameof(Date));
}

internal static class DateRules
{
    // One day of tolerance so a browser in Zurich and a server in UTC agree around midnight.
    public static IEnumerable<ValidationResult> NotInFuture(DateOnly date, string member)
    {
        if (date > DateOnly.FromDateTime(DateTime.Today).AddDays(1))
        {
            yield return new ValidationResult("Date cannot be in the future.", [member]);
        }
    }
}
