using System.ComponentModel.DataAnnotations;
using AthleteDay.Contracts;

namespace AthleteDay.UnitTests;

public class AthleteRequestTests
{
    private static readonly int ThisYear = DateTime.Today.Year;

    private static List<ValidationResult> Validate(object request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }

    private static AthleteRequest Valid() => new()
    {
        DisplayName = "Ada", Sport = "Rowing", BirthYear = ThisYear - 24, Sex = Sex.Female, BodyMassKg = 68.5m
    };

    [Fact]
    [Trait("AC", "AC-33")]
    public void A_complete_athlete_request_is_valid()
    {
        Assert.Empty(Validate(Valid()));
    }

    [Fact]
    [Trait("AC", "AC-33")]
    public void Athlete_request_reports_every_invalid_field()
    {
        var errors = Validate(new AthleteRequest { DisplayName = "", Sport = "x", BirthYear = null, BodyMassKg = 10 });

        Assert.Equivalent(
            new[] { "DisplayName", "Sport", "BirthYear", "BodyMassKg" },
            errors.SelectMany(e => e.MemberNames).ToArray());
    }

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(70, true)]
    [InlineData(71, false)]
    [Trait("AC", "AC-33")]
    public void Birth_year_must_give_an_age_between_10_and_70(int age, bool valid)
    {
        var request = Valid();
        request.BirthYear = ThisYear - age;

        Assert.Equal(valid, !Validate(request).Any(e => e.MemberNames.Contains("BirthYear")));
    }

    [Theory]
    [InlineData("A")]
    [InlineData("This name is far too long for the squad list and keeps going on and on")]
    [Trait("AC", "AC-33")]
    public void Name_must_be_2_to_60_characters(string name)
    {
        var request = Valid();
        request.DisplayName = name;

        Assert.Contains(Validate(request), e => e.MemberNames.Contains("DisplayName"));
    }
}
