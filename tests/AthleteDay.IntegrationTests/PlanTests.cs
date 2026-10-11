using System.Net;
using System.Net.Http.Json;
using AthleteDay.Api.Domain;
using AthleteDay.Contracts;

namespace AthleteDay.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class PlanTests(ApiFactory api)
{
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich")).DateTime);

    private static DateOnly ThisMonday => Today.AddDays(-(((int)Today.DayOfWeek + 6) % 7));

    private static PlannedSessionRequest S(SessionType type, int minutes, int rpe, string? note = null) =>
        new() { Type = type, DurationMinutes = minutes, Rpe = rpe, Note = note };

    private static PlanTemplateRequest Template() => new()
    {
        Days =
        [
            new() { DayOfWeek = 1, Sessions = [S(SessionType.Strength, 60, 6)] },
            new() { DayOfWeek = 3, Sessions = [S(SessionType.Speed, 45, 7, "Short sprints")] },
        ]
    };

    private static string Week(int athleteId, DateOnly? start = null) =>
        $"/api/athletes/{athleteId}/plan/week" + (start is { } s ? $"?start={s:yyyy-MM-dd}" : "");

    [Fact]
    [Trait("AC", "AC-78")]
    public async Task The_coach_sets_a_standard_week_that_only_affects_that_athlete()
    {
        var athlete = await api.CreateAthleteAsync();
        var other = await api.CreateAthleteAsync();
        var coach = api.ClientAs(Roles.Coach);

        var saved = await coach.PutAsJsonAsync($"/api/athletes/{athlete.Id}/plan/template", Template());
        var invalid = await coach.PutAsJsonAsync($"/api/athletes/{athlete.Id}/plan/template",
            new PlanTemplateRequest { Days = [new() { DayOfWeek = 1, Sessions = [S(SessionType.Strength, 2, 6)] }] });
        var physio = await api.ClientAs(Roles.Physio).PutAsJsonAsync($"/api/athletes/{athlete.Id}/plan/template", Template());

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, physio.StatusCode);

        var template = await coach.GetFromJsonAsync<PlanTemplateDto>($"/api/athletes/{athlete.Id}/plan/template");
        Assert.Equal(7, template!.Days.Count);
        Assert.Equal(360, template.Days.Single(d => d.DayOfWeek == 1).Sessions.Single().PlannedLoad);
        var untouched = await coach.GetFromJsonAsync<PlanTemplateDto>($"/api/athletes/{other.Id}/plan/template");
        Assert.All(untouched!.Days, d => Assert.Empty(d.Sessions));
    }

    [Fact]
    [Trait("AC", "AC-79")]
    [Trait("AC", "AC-81")]
    public async Task The_week_merges_standard_and_changed_days_and_compares_with_logged_load()
    {
        var athlete = await api.CreateAthleteAsync();
        var coach = api.ClientAs(Roles.Coach);
        (await coach.PutAsJsonAsync($"/api/athletes/{athlete.Id}/plan/template", Template())).EnsureSuccessStatusCode();
        var wednesday = ThisMonday.AddDays(2);
        (await coach.PutAsJsonAsync($"/api/athletes/{athlete.Id}/plan/days/{wednesday:yyyy-MM-dd}", new PlanDayChangeRequest
        {
            Reason = "Match moved", Sessions = [S(SessionType.Technique, 40, 4)]
        })).EnsureSuccessStatusCode();
        await api.WithDbAsync(async db =>
        {
            db.TrainingSessions.Add(new TrainingSession
            {
                AthleteId = athlete.Id, Date = ThisMonday, Type = SessionType.Strength, DurationMinutes = 60, Rpe = 5,
                LoggedBy = "test", LoggedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        });

        var week = await coach.GetFromJsonAsync<WeekPlanDto>(Week(athlete.Id));

        Assert.Equal(ThisMonday, week!.WeekStart);
        Assert.Equal((PlanDaySource.Standard, 360, 300), (week.Days[0].Source, week.Days[0].PlannedLoad, week.Days[0].LoggedLoad));
        Assert.Equal((PlanDaySource.Changed, "Match moved", 160), (week.Days[2].Source, week.Days[2].Reason, week.Days[2].PlannedLoad));
        Assert.Equal((520, 300), (week.PlannedLoad, week.LoggedLoad));

        var next = await coach.GetFromJsonAsync<WeekPlanDto>(Week(athlete.Id, ThisMonday.AddDays(9)));
        Assert.Equal(ThisMonday.AddDays(7), next!.WeekStart);
        Assert.Equal(PlanDaySource.Standard, next.Days[2].Source);
    }

    [Fact]
    [Trait("AC", "AC-80")]
    public async Task A_day_can_be_cancelled_and_reset_without_touching_the_standard_week()
    {
        var athlete = await api.CreateAthleteAsync();
        var coach = api.ClientAs(Roles.Coach);
        (await coach.PutAsJsonAsync($"/api/athletes/{athlete.Id}/plan/template", Template())).EnsureSuccessStatusCode();
        var monday = ThisMonday.AddDays(7);
        var url = $"/api/athletes/{athlete.Id}/plan/days/{monday:yyyy-MM-dd}";

        (await coach.PutAsJsonAsync(url, new PlanDayChangeRequest { Reason = "Rest after travel", Sessions = [] })).EnsureSuccessStatusCode();
        var cancelled = (await coach.GetFromJsonAsync<WeekPlanDto>(Week(athlete.Id, monday)))!.Days[0];
        Assert.Equal((PlanDaySource.Changed, "Rest after travel", 0), (cancelled.Source, cancelled.Reason, cancelled.PlannedLoad));

        Assert.Equal(HttpStatusCode.NoContent, (await coach.DeleteAsync(url)).StatusCode);
        var reset = (await coach.GetFromJsonAsync<WeekPlanDto>(Week(athlete.Id, monday)))!.Days[0];
        Assert.Equal((PlanDaySource.Standard, 360), (reset.Source, reset.PlannedLoad));
        Assert.Equal(HttpStatusCode.NotFound, (await coach.DeleteAsync(url)).StatusCode);

        var template = await coach.GetFromJsonAsync<PlanTemplateDto>($"/api/athletes/{athlete.Id}/plan/template");
        Assert.Single(template!.Days.Single(d => d.DayOfWeek == 1).Sessions);
    }

    [Theory]
    [InlineData(Roles.Coach, false, HttpStatusCode.OK)]
    [InlineData(Roles.Physio, false, HttpStatusCode.OK)]
    [InlineData(Roles.Nutrition, false, HttpStatusCode.OK)]
    [InlineData(Roles.Athlete, true, HttpStatusCode.OK)]
    [InlineData(Roles.Athlete, false, HttpStatusCode.Forbidden)]
    [InlineData(Roles.Kitchen, false, HttpStatusCode.Forbidden)]
    [Trait("AC", "AC-82")]
    public async Task Staff_read_every_plan_and_athletes_only_their_own(string role, bool asThatAthlete, HttpStatusCode expected)
    {
        var athlete = await api.CreateAthleteAsync();
        var client = api.ClientAs(role, asThatAthlete ? athlete.Code : "someone-else");

        Assert.Equal(expected, (await client.GetAsync(Week(athlete.Id))).StatusCode);
    }
}
