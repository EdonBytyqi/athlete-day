using System.Net;
using System.Net.Http.Json;
using AthleteDay.Contracts;

namespace AthleteDay.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class AthleteSignInTests(ApiFactory api)
{
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich")).DateTime);

    [Fact]
    [Trait("AC", "AC-52")]
    public async Task An_athlete_added_after_start_up_can_send_their_own_morning_check()
    {
        var added = await (await api.ClientAs(Roles.Coach).PostAsJsonAsync("/api/athletes", new AthleteRequest
        {
            DisplayName = "Ada", SportId = await api.SportIdAsync("Rowing"), BirthYear = Today.Year - 24, Sex = Sex.Female, BodyMassKg = 68.5m
        })).ReadAsync<AthleteDetailsDto>();

        var check = await api.ClientAs(Roles.Athlete, user: added.Code).PutAsJsonAsync("/api/wellness",
            new WellnessCheckRequest { AthleteId = added.Id, Date = Today, SleepHours = 7.5m, Soreness = 2, Mood = 4 });

        Assert.Equal(HttpStatusCode.OK, check.StatusCode);
        var row = (await api.ClientAs(Roles.Coach).GetFromJsonAsync<List<DashboardRowDto>>("/api/dashboard"))!
            .Single(r => r.AthleteId == added.Id);
        Assert.Equal(7.5m, row.Wellness?.SleepHours);
    }
}
