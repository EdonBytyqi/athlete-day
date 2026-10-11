using System.Net;
using System.Net.Http.Json;
using AthleteDay.Api.Domain;
using AthleteDay.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace AthleteDay.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class DashboardRangeTests(ApiFactory api)
{
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich")).DateTime);

    private static string Url(DateOnly from, DateOnly to) => $"/api/dashboard?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";

    [Theory]
    [InlineData(0, 1, "to")]      // to in the future
    [InlineData(-2, -5, "from")]  // from after to
    [InlineData(-400, 0, "from")] // more than 366 days
    [Trait("AC", "AC-58")]
    public async Task Invalid_ranges_are_rejected(int fromDays, int toDays, string field)
    {
        var response = await api.ClientAs(Roles.Coach).GetAsync(Url(Today.AddDays(fromDays), Today.AddDays(toDays)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(field, (await response.ReadAsync<ValidationProblemDetails>()).Errors.Keys);
    }

    [Fact]
    [Trait("AC", "AC-58")]
    public async Task Without_a_range_the_dashboard_shows_today()
    {
        var athlete = await api.CreateAthleteAsync();

        var row = (await api.ClientAs(Roles.Coach).GetFromJsonAsync<List<DashboardRowDto>>("/api/dashboard"))!
            .Single(r => r.AthleteId == athlete.Id);

        Assert.Equal((Today, Today, 1), (row.Range.From, row.Range.To, row.Range.Days));
    }

    [Fact]
    [Trait("AC", "AC-59")]
    public async Task Range_figures_cover_the_period_and_the_rest_is_as_of_the_end_date()
    {
        var athlete = await api.CreateAthleteAsync();
        var (from, to) = (Today.AddDays(-10), Today.AddDays(-8));
        await api.WithDbAsync(async db =>
        {
            db.TrainingSessions.AddRange(
                Session(athlete.Id, Today.AddDays(-10), rpe: 5, minutes: 60),  // 300
                Session(athlete.Id, Today.AddDays(-8), rpe: 4, minutes: 50),   // 200
                Session(athlete.Id, Today.AddDays(-8), rpe: 5, minutes: 40),   // 200
                Session(athlete.Id, Today.AddDays(-3), rpe: 10, minutes: 100)); // outside the range
            db.WellnessChecks.AddRange(
                new WellnessCheck { AthleteId = athlete.Id, Date = Today.AddDays(-9), SleepHours = 8, Soreness = 2, Mood = 4 },
                new WellnessCheck { AthleteId = athlete.Id, Date = Today.AddDays(-8), SleepHours = 6, Soreness = 4, Mood = 2 },
                new WellnessCheck { AthleteId = athlete.Id, Date = Today, SleepHours = 9, Soreness = 1, Mood = 5 });
            await db.SaveChangesAsync();
        });

        var row = (await api.ClientAs(Roles.Coach).GetFromJsonAsync<List<DashboardRowDto>>(Url(from, to)))!
            .Single(r => r.AthleteId == athlete.Id);

        Assert.Equal((from, to, 3), (row.Range.From, row.Range.To, row.Range.Days));
        Assert.Equal((700, 233.3, 3), (row.Range.TotalLoad, row.Range.AverageDailyLoad, row.Range.Sessions));
        Assert.Equal((2, 7.0m, 3.0, 3.0), (row.Range.WellnessDays, row.Range.AverageSleep, row.Range.AverageSoreness, row.Range.AverageMood));

        // As of the end date: that day's load and check, and the 7/28-day windows ending on it.
        Assert.Equal(400, row.TodayLoad);
        Assert.Equal(to, row.Wellness?.Date);
        Assert.Equal(Math.Round(700 / 7.0, 1), row.AcuteLoad);
        Assert.Equal(Math.Round(700 / 28.0, 1), row.ChronicLoad);
        Assert.Contains("High soreness (4/5)", row.Readiness.Reasons);
    }

    private static TrainingSession Session(int athleteId, DateOnly date, int rpe, int minutes) => new()
    {
        AthleteId = athleteId, Date = date, Type = SessionType.Endurance, Rpe = rpe, DurationMinutes = minutes,
        LoggedBy = "test", LoggedAt = DateTimeOffset.UtcNow
    };
}
