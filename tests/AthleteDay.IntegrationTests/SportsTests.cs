using System.Net;
using System.Net.Http.Json;
using AthleteDay.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace AthleteDay.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class SportsTests(ApiFactory api)
{
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich")).DateTime);

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid().ToString("N")[..6]}";

    private async Task<SportDto> AddSportAsync(string name)
    {
        var response = await api.ClientAs(Roles.Admin).PostAsJsonAsync("/api/sports", new SportRequest { Name = name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<SportDto>();
    }

    private AthleteRequest Athlete(int sportId) => new()
    {
        DisplayName = "Ada", SportId = sportId, BirthYear = Today.Year - 24, Sex = Sex.Female, BodyMassKg = 68.5m
    };

    [Fact]
    [Trait("AC", "AC-63")]
    public async Task Admin_adds_renames_retires_and_reactivates_sports()
    {
        var admin = api.ClientAs(Roles.Admin);
        var name = Unique("Swimming");
        var sport = await AddSportAsync(name);

        var duplicate = await admin.PostAsJsonAsync("/api/sports", new SportRequest { Name = name.ToUpperInvariant() });
        var tooShort = await admin.PostAsJsonAsync("/api/sports", new SportRequest { Name = "S" });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains("Name", (await duplicate.ReadAsync<ValidationProblemDetails>()).Errors.Keys);
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);

        var renamed = Unique("Open-water swimming");
        (await admin.PutAsJsonAsync($"/api/sports/{sport.Id}", new SportRequest { Name = renamed, Retired = true })).EnsureSuccessStatusCode();
        var list = await api.ClientAs(Roles.Coach).GetFromJsonAsync<List<SportDto>>("/api/sports");
        Assert.Contains(list!, s => s.Id == sport.Id && s.Name == renamed && s.Retired);

        (await admin.PutAsJsonAsync($"/api/sports/{sport.Id}", new SportRequest { Name = renamed, Retired = false })).EnsureSuccessStatusCode();
        list = await api.ClientAs(Roles.Coach).GetFromJsonAsync<List<SportDto>>("/api/sports");
        Assert.Contains(list!, s => s.Id == sport.Id && !s.Retired);
    }

    [Theory]
    [InlineData(Roles.Coach)]
    [InlineData(Roles.Physio)]
    [InlineData(Roles.Nutrition)]
    [InlineData(Roles.Kitchen)]
    [InlineData(Roles.Athlete)]
    [Trait("AC", "AC-63")]
    public async Task Only_the_admin_changes_the_list(string role)
    {
        var sport = await AddSportAsync(Unique("Fencing"));
        var client = api.ClientAs(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/sports", new SportRequest { Name = Unique("Judo") })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/sports/{sport.Id}", new SportRequest { Name = "Judo" })).StatusCode);
    }

    [Theory]
    [InlineData(Roles.Coach, HttpStatusCode.OK)]
    [InlineData(Roles.Athlete, HttpStatusCode.OK)]
    [InlineData(Roles.Admin, HttpStatusCode.OK)]
    [InlineData(Roles.Kitchen, HttpStatusCode.Forbidden)]
    [Trait("AC", "AC-63")]
    public async Task Roles_that_see_athletes_can_read_the_list(string role, HttpStatusCode expected)
    {
        Assert.Equal(expected, (await api.ClientAs(role).GetAsync("/api/sports")).StatusCode);
    }

    [Fact]
    [Trait("AC", "AC-64")]
    public async Task Athletes_take_a_sport_from_the_list_and_follow_its_renames()
    {
        var sport = await AddSportAsync(Unique("Climbing"));
        var coach = api.ClientAs(Roles.Coach);

        var created = await coach.PostAsJsonAsync("/api/athletes", Athlete(sport.Id));
        var athlete = await created.ReadAsync<AthleteDetailsDto>();
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal((sport.Id, sport.Name), (athlete.SportId, athlete.Sport));

        var renamed = Unique("Bouldering");
        (await api.ClientAs(Roles.Admin).PutAsJsonAsync($"/api/sports/{sport.Id}", new SportRequest { Name = renamed })).EnsureSuccessStatusCode();

        Assert.Contains((await coach.GetFromJsonAsync<List<AthleteDto>>("/api/athletes"))!, a => a.Id == athlete.Id && a.Sport == renamed);
        Assert.Contains((await coach.GetFromJsonAsync<List<DashboardRowDto>>("/api/dashboard"))!, r => r.AthleteId == athlete.Id && r.Sport == renamed);
    }

    [Fact]
    [Trait("AC", "AC-64")]
    public async Task Unknown_and_retired_sports_are_rejected_but_an_athlete_may_keep_a_retired_one()
    {
        var sport = await AddSportAsync(Unique("Curling"));
        var coach = api.ClientAs(Roles.Coach);
        var athlete = await (await coach.PostAsJsonAsync("/api/athletes", Athlete(sport.Id))).ReadAsync<AthleteDetailsDto>();
        (await api.ClientAs(Roles.Admin).PutAsJsonAsync($"/api/sports/{sport.Id}", new SportRequest { Name = sport.Name, Retired = true })).EnsureSuccessStatusCode();

        var unknown = await coach.PostAsJsonAsync("/api/athletes", Athlete(999_999));
        var retired = await coach.PostAsJsonAsync("/api/athletes", Athlete(sport.Id));
        var keep = await coach.PutAsJsonAsync($"/api/athletes/{athlete.Id}", Athlete(sport.Id));

        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Contains("SportId", (await unknown.ReadAsync<ValidationProblemDetails>()).Errors.Keys);
        Assert.Equal(HttpStatusCode.BadRequest, retired.StatusCode);
        Assert.Equal(HttpStatusCode.OK, keep.StatusCode);
    }
}
