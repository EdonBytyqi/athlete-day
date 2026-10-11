using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Text = AthleteDay.Contracts.Resources.ValidationText;

namespace AthleteDay.Contracts;

// These request types are shared by the API and the Blazor staff app,
// so the same validation rules run in the browser and on the server.
// Messages come from Resources/ValidationText(.de).resx in the current UI culture (ADR-09).

public sealed class LogSessionRequest : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.SelectAthlete))]
    public int AthleteId { get; set; }

    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [EnumDataType(typeof(SessionType))]
    public SessionType Type { get; set; } = SessionType.Strength;

    [Range(5, 300, ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.DurationRange))]
    public int DurationMinutes { get; set; } = 60;

    [Range(1, 10, ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.RpeRange))]
    public int Rpe { get; set; } = 5;

    [StringLength(500, ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.NotesLength))]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        DateRules.NotInFuture(Date, nameof(Date));
}

public sealed class WellnessCheckRequest : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.SelectAthlete))]
    public int AthleteId { get; set; }

    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Range(0, 14, ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.SleepRange))]
    public decimal SleepHours { get; set; } = 8;

    /// <summary>1 = no soreness, 5 = very sore.</summary>
    [Range(1, 5, ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.SorenessRange))]
    public int Soreness { get; set; } = 2;

    /// <summary>1 = very low, 5 = very good.</summary>
    [Range(1, 5, ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.MoodRange))]
    public int Mood { get; set; } = 4;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        DateRules.NotInFuture(Date, nameof(Date));
}

public sealed class NutritionTargetRequest
{
    [Range(1, int.MaxValue, ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.SelectAthlete))]
    public int AthleteId { get; set; }

    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Range(1200, 7000, ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.EnergyRange))]
    public int EnergyKcal { get; set; } = 3000;

    [Range(typeof(decimal), "2", "12", ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.CarbsRange))]
    public decimal CarbsGPerKg { get; set; } = 5;

    /// <summary>Shown to the kitchen. Preferences only, never medical details.</summary>
    [StringLength(200, ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.KitchenNoteLength))]
    public string? KitchenNote { get; set; }
}

public sealed class PhysioNoteRequest : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.SelectAthlete))]
    public int AthleteId { get; set; }

    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [EnumDataType(typeof(TrainingRestriction))]
    public TrainingRestriction Restriction { get; set; } = TrainingRestriction.None;

    [Required(ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.NoteRequired))]
    [StringLength(1000, MinimumLength = 3, ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.NoteLength))]
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
            yield return new ValidationResult(Text.DateNotInFuture, [member]);
        }
    }
}

/// <summary>Add or edit an athlete. The code is assigned by the system and never part of the request.</summary>
public sealed class AthleteRequest
{
    [Required(ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.NameRequired))]
    [StringLength(60, MinimumLength = 2, ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.NameLength))]
    public string DisplayName { get; set; } = "";

    /// <summary>An active sport from the managed list (ADR-10).</summary>
    [Range(1, int.MaxValue, ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.SelectSport))]
    public int SportId { get; set; }

    /// <summary>Only the year is stored (data minimisation); the age is the age reached this year.</summary>
    [Required(ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.BirthYearRequired))]
    [AgeBetween(10, 70)]
    public int? BirthYear { get; set; }

    [EnumDataType(typeof(Sex))]
    public Sex Sex { get; set; } = Sex.NotStated;

    [Range(typeof(decimal), "30", "200", ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.BodyMassRange))]
    public decimal BodyMassKg { get; set; } = 70;
}

/// <summary>Add or change an entry of the sports list (admin). Retired sports cannot be chosen for athletes.</summary>
public sealed class SportRequest
{
    [Required(ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.SportRequired))]
    [StringLength(60, MinimumLength = 2, ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.SportLength))]
    public string Name { get; set; } = "";

    public bool Retired { get; set; }
}

/// <summary>A goal for one device test; the plausible range per test is checked by the API.</summary>
public sealed class GoalRequest
{
    [Range(typeof(decimal), "0.01", "100000", ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.GoalTargetPositive))]
    public decimal Target { get; set; }

    public DateOnly? TargetDate { get; set; }

    [StringLength(200, ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.TextTooLong))]
    public string? Note { get; set; }
}

/// <summary>A note on a training session (ADR-12).</summary>
public sealed class SessionNoteRequest
{
    // Qualified on purpose: inside this class, `Text` is the property, not the resource alias.
    [Required(ErrorMessageResourceType = typeof(Resources.ValidationText), ErrorMessageResourceName = nameof(Resources.ValidationText.NoteRequired))]
    [StringLength(1000, MinimumLength = 1, ErrorMessageResourceType = typeof(Resources.ValidationText), ErrorMessageResourceName = nameof(Resources.ValidationText.NoteLength))]
    public string Text { get; set; } = "";
}

/// <summary>A progress review over the last <see cref="Weeks"/> weeks, ending today (ADR-13).</summary>
public sealed class ProgressReviewRequest : IValidatableObject
{
    [Range(1, 52, ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.WeeksRange))]
    public int Weeks { get; set; } = 4;

    [StringLength(2000, ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.TextTooLong))]
    public string? Observations { get; set; }

    [StringLength(2000, ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.TextTooLong))]
    public string? Achievements { get; set; }

    [StringLength(2000, ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.TextTooLong))]
    public string? Challenges { get; set; }

    [StringLength(2000, ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.TextTooLong))]
    public string? Recommendations { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (new[] { Observations, Achievements, Challenges, Recommendations }.All(string.IsNullOrWhiteSpace))
            yield return new ValidationResult(Text.ReviewEmpty, [nameof(Observations)]);
    }
}

/// <summary>One planned session of a training plan.</summary>
public sealed class PlannedSessionRequest
{
    [EnumDataType(typeof(SessionType))]
    public SessionType Type { get; set; } = SessionType.Strength;

    [Range(5, 300, ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.DurationRange))]
    public int DurationMinutes { get; set; } = 60;

    [Range(1, 10, ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.RpeRange))]
    public int Rpe { get; set; } = 5;

    [StringLength(200, ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.TextTooLong))]
    public string? Note { get; set; }
}

/// <summary>The sessions of one weekday in the standard week.</summary>
public sealed class PlanTemplateDayRequest : IValidatableObject
{
    [Range(1, 7, ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.DayOfWeekRange))]
    public int DayOfWeek { get; set; } = 1;

    public List<PlannedSessionRequest> Sessions { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) => PlanRules.Sessions(Sessions);
}

/// <summary>The whole standard week (ADR-14); weekdays not listed have no sessions.</summary>
public sealed class PlanTemplateRequest : IValidatableObject
{
    public List<PlanTemplateDayRequest> Days { get; set; } = [];

    // Validating a day checks its weekday and, through the day's own rule, its sessions.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) => Days.SelectMany(PlanRules.Nested);
}

/// <summary>Replaces one date's sessions; no sessions means the day is cancelled (ADR-14).</summary>
public sealed class PlanDayChangeRequest : IValidatableObject
{
    [StringLength(200, ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.TextTooLong))]
    public string? Reason { get; set; }

    public List<PlannedSessionRequest> Sessions { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) => PlanRules.Sessions(Sessions);
}

internal static class PlanRules
{
    public const int MaxSessionsPerDay = 3;

    /// <summary>At most three sessions a day, each one valid. Checked here so the rules hold however deep the
    /// validator looks into lists.</summary>
    public static IEnumerable<ValidationResult> Sessions(IReadOnlyCollection<PlannedSessionRequest> sessions)
    {
        if (sessions.Count > MaxSessionsPerDay)
            yield return new ValidationResult(string.Format(CultureInfo.CurrentCulture, Text.TooManySessions, MaxSessionsPerDay), ["Sessions"]);
        foreach (var result in sessions.SelectMany(Nested))
            yield return result;
    }

    public static IEnumerable<ValidationResult> Nested(object item)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(item, new ValidationContext(item), results, validateAllProperties: true);
        return results;
    }
}

/// <summary>
/// A property-level rule, so it reports together with the other field errors
/// (IValidatableObject only runs once every property is valid).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class AgeBetweenAttribute(int min, int max) : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is not int year) return ValidationResult.Success;
        var age = DateTime.Today.Year - year;
        return age >= min && age <= max
            ? ValidationResult.Success
            : new ValidationResult(string.Format(CultureInfo.CurrentCulture, Text.AgeBetween, min, max),
                context.MemberName is { } member ? [member] : null);
    }
}
