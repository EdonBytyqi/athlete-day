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

    [Required(ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.SportRequired))]
    [StringLength(60, MinimumLength = 2, ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.SportLength))]
    public string Sport { get; set; } = "";

    /// <summary>Only the year is stored (data minimisation); the age is the age reached this year.</summary>
    [Required(ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.BirthYearRequired))]
    [AgeBetween(10, 70)]
    public int? BirthYear { get; set; }

    [EnumDataType(typeof(Sex))]
    public Sex Sex { get; set; } = Sex.NotStated;

    [Range(typeof(decimal), "30", "200", ErrorMessageResourceType = typeof(Text), ErrorMessageResourceName = nameof(Text.BodyMassRange))]
    public decimal BodyMassKg { get; set; } = 70;
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
