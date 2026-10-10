using System.ComponentModel.DataAnnotations;
using AthleteDay.Contracts;
using AthleteDay.Core;

namespace AthleteDay.UnitTests;

public class ValidationAndNutritionTests
{
    private static List<ValidationResult> Validate(object request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    [Trait("AC", "AC-07")]
    public void A_complete_session_request_is_valid()
    {
        Assert.Empty(Validate(new LogSessionRequest { AthleteId = 1, DurationMinutes = 60, Rpe = 7 }));
    }

    [Fact]
    [Trait("AC", "AC-07")]
    public void Session_request_reports_every_invalid_field()
    {
        var errors = Validate(new LogSessionRequest { AthleteId = 0, DurationMinutes = 2, Rpe = 11 });

        Assert.Equivalent(
            new[] { "AthleteId", "DurationMinutes", "Rpe" },
            errors.SelectMany(e => e.MemberNames).ToArray());
    }

    [Fact]
    [Trait("AC", "AC-08")]
    public void Session_in_the_future_is_rejected()
    {
        var errors = Validate(new LogSessionRequest
        {
            AthleteId = 1, Date = DateOnly.FromDateTime(DateTime.Today).AddDays(3), DurationMinutes = 60, Rpe = 5
        });

        Assert.Contains(errors, e => e.MemberNames.Contains("Date"));
    }

    [Fact]
    [Trait("AC", "AC-21")]
    public void Physio_note_requires_text()
    {
        var errors = Validate(new PhysioNoteRequest { AthleteId = 1, Note = "" });

        Assert.Contains(errors, e => e.MemberNames.Contains("Note"));
    }

    [Theory]
    [InlineData(6.0, 66.0, 396)]
    [InlineData(5.5, 63.0, 347)] // 346.5 rounds up
    [Trait("AC", "AC-19")]
    public void Carbohydrate_grams_are_rounded_to_whole_grams(decimal perKg, decimal mass, int expected) =>
        Assert.Equal(expected, NutritionCalculator.CarbsGrams(perKg, mass));

    [Theory]
    [InlineData(2199, "S")]
    [InlineData(2200, "M")]
    [InlineData(3000, "L")]
    [InlineData(3800, "XL")]
    [Trait("AC", "AC-19")]
    public void Portion_size_follows_the_energy_target(int kcal, string portion) =>
        Assert.Equal(portion, NutritionCalculator.Portion(kcal));
}
