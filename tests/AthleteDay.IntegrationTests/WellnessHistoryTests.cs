using System.Net;
using System.Net.Http.Json;
using AthleteDay.Api.Domain;
using AthleteDay.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace AthleteDay.IntegrationTests;

/// <summary>Spec v1.1, F11 wellness history.</summary>
[Collection(ApiCollection.Name)]
public sealed class WellnessHistoryTests(ApiFactory api)
{
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich")).DateTime);

    private async Task<Athlete> AthleteWithChecksAsync()
    {
        var athlete = await api.CreateAthleteAsync();
        await api.WithDbAsync(async db =>
        {
            db.WellnessChecks.AddRange(
                new WellnessCheck { AthleteId = athlete.Id, Date = Today, SleepHours = 5.5m, Soreness = 4, Mood = 4 },
                new WellnessCheck { AthleteId = athlete.Id, Date = Today.AddDays(-3), SleepHours = 8, Soreness = 2, Mood = 4 },
                new WellnessCheck { AthleteId = athlete.Id, Date = Today.AddDays(-20), SleepHours = 7, Soreness = 1, Mood = 5 });
            await db.SaveChangesAsync();
        });
        return athlete;
    }

    [Fact]
    [Trait("AC", "AC-43")]
    public async Task History_returns_the_checks_of_the_window_newest_first_with_flags()
    {
        var athlete = await AthleteWithChecksAsync();

        var history = (await api.ClientAs(Roles.Coach)
            .GetFromJsonAsync<List<WellnessDayDto>>($"/api/athletes/{athlete.Id}/wellness?days=14"))!;

        Assert.Equal([Today, Today.AddDays(-3)], history.Select(d => d.Date));
        var today = history[0];
        Assert.Equal((5.5m, 4, 4), (today.SleepHours, today.Soreness, today.Mood));
        Assert.Equal(["Sleep under 6 h (5.5 h)", "High soreness (4/5)"], today.Flags);
        Assert.Empty(history[1].Flags);
    }

    [Fact]
    [Trait("AC", "AC-43")]
    public async Task History_defaults_to_14_days()
    {
        var athlete = await AthleteWithChecksAsync();

        var history = await api.ClientAs(Roles.Physio)
            .GetFromJsonAsync<List<WellnessDayDto>>($"/api/athletes/{athlete.Id}/wellness");

        Assert.Equal(2, history!.Count);
    }

    [Theory]
    [Trait("AC", "AC-45")]
    [InlineData(0)]
    [InlineData(29)]
    public async Task Days_outside_1_to_28_is_rejected(int days)
    {
        var athlete = await api.CreateAthleteAsync();

        var response = await api.ClientAs(Roles.Coach).GetAsync($"/api/athletes/{athlete.Id}/wellness?days={days}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("days", (await response.ReadAsync<ValidationProblemDetails>()).Errors.Keys);
    }

    [Theory]
    [Trait("AC", "AC-46")]
    [InlineData(Roles.Coach, HttpStatusCode.OK)]
    [InlineData(Roles.Physio, HttpStatusCode.OK)]
    [InlineData(Roles.Nutrition, HttpStatusCode.Forbidden)]
    [InlineData(Roles.Kitchen, HttpStatusCode.Forbidden)]
    [InlineData(Roles.Athlete, HttpStatusCode.Forbidden)]
    [InlineData(Roles.Admin, HttpStatusCode.Forbidden)]
    public async Task Only_coach_and_physio_may_read_the_history(string role, HttpStatusCode expected)
    {
        var athlete = await api.CreateAthleteAsync();

        var response = await api.ClientAs(role).GetAsync($"/api/athletes/{athlete.Id}/wellness");

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    [Trait("AC", "AC-47")]
    public async Task Reading_the_history_is_audited()
    {
        var athlete = await api.CreateAthleteAsync();

        (await api.ClientAs(Roles.Coach, "lena-audit").GetAsync($"/api/athletes/{athlete.Id}/wellness")).EnsureSuccessStatusCode();

        var audit = await api.ClientAs(Roles.Admin).GetFromJsonAsync<List<AuditEntryDto>>("/api/audit?take=500");
        Assert.Contains(audit!, a => a is { User: "lena-audit", Action: "Viewed", Resource: "WellnessHistory" }
                                     && a.AthleteId == athlete.Id);
    }

    [Fact]
    [Trait("AC", "AC-48")]
    public async Task Unknown_athlete_returns_404()
    {
        var response = await api.ClientAs(Roles.Coach).GetAsync("/api/athletes/999999/wellness");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
