using System.Net;
using System.Net.Http.Json;
using AthleteDay.Api.Domain;
using AthleteDay.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace AthleteDay.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class AthleteSpaceTests(ApiFactory api)
{
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich")).DateTime);

    private static DateOnly ThisMonday => Today.AddDays(-(((int)Today.DayOfWeek + 6) % 7));

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..6]}";

    private async Task<List<AuditEntryDto>> AuditAsync() =>
        (await api.ClientAs(Roles.Admin).GetFromJsonAsync<List<AuditEntryDto>>("/api/audit?take=500"))!;

    // ---- F19 progress ----------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-67")]
    public async Task Progress_has_one_entry_per_week_and_compares_the_last_four_weeks()
    {
        var athlete = await api.CreateAthleteAsync();
        await api.WithDbAsync(async db =>
        {
            for (var week = 0; week < 8; week++)
            {
                var monday = ThisMonday.AddDays(-7 * week);
                db.TrainingSessions.Add(Session(athlete.Id, monday, rpe: week < 4 ? 10 : 5, minutes: 100)); // 1000 recent, 500 before
                db.WellnessChecks.Add(new WellnessCheck
                {
                    AthleteId = athlete.Id, Date = monday, SleepHours = week < 4 ? 6.0m : 7.0m, Soreness = 2, Mood = 4
                });
            }
            await db.SaveChangesAsync();
        });

        var progress = await api.ClientAs(Roles.Physio, "jonas").GetFromJsonAsync<ProgressDto>($"/api/athletes/{athlete.Id}/progress?weeks=8");

        Assert.Equal(8, progress!.Weeks.Count);
        Assert.Equal(ThisMonday, progress.Weeks[^1].WeekStart);
        Assert.All(progress.Weeks, w => Assert.Equal(1, w.Sessions));
        Assert.Equal(1000, progress.Weeks[^1].TotalLoad);
        var trends = progress.Trends.ToDictionary(t => t.Metric, t => t.Trend);
        Assert.Equal(Trend.Up, trends["load"]);
        Assert.Equal(Trend.Stable, trends["sessions"]);
        Assert.Equal(Trend.Down, trends["sleep"]);
        Assert.Equal(Trend.Stable, trends["mood"]);
        Assert.Contains(await AuditAsync(), a => a is { User: "jonas", Resource: "Progress" } && a.AthleteId == athlete.Id);
    }

    [Theory]
    [InlineData(Roles.Coach, "?weeks=3", HttpStatusCode.BadRequest)]
    [InlineData(Roles.Coach, "?weeks=53", HttpStatusCode.BadRequest)]
    [InlineData(Roles.Nutrition, "", HttpStatusCode.OK)]
    [InlineData(Roles.Kitchen, "", HttpStatusCode.Forbidden)]
    [InlineData(Roles.Athlete, "", HttpStatusCode.Forbidden)]
    [Trait("AC", "AC-67")]
    public async Task Progress_validates_the_period_and_is_for_dashboard_roles_only(string role, string query, HttpStatusCode expected)
    {
        var athlete = await api.CreateAthleteAsync();

        Assert.Equal(expected, (await api.ClientAs(role).GetAsync($"/api/athletes/{athlete.Id}/progress{query}")).StatusCode);
    }

    // ---- F20 benchmarks and goals --------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-69")]
    public async Task Benchmarks_compare_results_with_the_athletes_history_the_squad_and_the_goal()
    {
        var sport = Unique("Bench");
        var athlete = await api.CreateAthleteAsync(sport: sport);
        var teammate = await api.CreateAthleteAsync(sport: sport);
        await api.WithDbAsync(async db =>
        {
            var job = new ImportJob { Id = Guid.NewGuid(), FileName = "t.csv", BlobName = "t", UploadedBy = "test", Status = ImportStatus.Completed };
            db.ImportJobs.Add(job);
            DeviceMeasurement M(int athleteId, int daysAgo, string test, decimal value, string unit) => new()
            {
                AthleteId = athleteId, Date = Today.AddDays(-daysAgo), TestType = test, Value = value, Unit = unit, ImportJobId = job.Id
            };
            db.DeviceMeasurements.AddRange(
                M(athlete.Id, 60, "CMJ", 30.0m, "cm"), M(athlete.Id, 30, "CMJ", 34.0m, "cm"), M(athlete.Id, 10, "CMJ", 33.0m, "cm"),
                M(athlete.Id, 20, "SPRINT_30M", 4.40m, "s"), M(athlete.Id, 5, "SPRINT_30M", 4.30m, "s"),
                M(teammate.Id, 7, "CMJ", 40.0m, "cm"), M(teammate.Id, 7, "SPRINT_30M", 4.10m, "s"));
            await db.SaveChangesAsync();
        });
        var coach = api.ClientAs(Roles.Coach);
        (await coach.PutAsJsonAsync($"/api/athletes/{athlete.Id}/goals/CMJ", new GoalRequest { Target = 36.0m, TargetDate = Today.AddDays(30) })).EnsureSuccessStatusCode();
        (await coach.PutAsJsonAsync($"/api/athletes/{athlete.Id}/goals/SPRINT_30M", new GoalRequest { Target = 4.35m })).EnsureSuccessStatusCode();

        var benchmarks = (await coach.GetFromJsonAsync<List<BenchmarkDto>>($"/api/athletes/{athlete.Id}/benchmarks?weeks=8"))!
            .ToDictionary(b => b.TestType);

        var cmj = benchmarks["CMJ"];
        Assert.Equal((33.0m, 34.0m), (cmj.Latest.Value, cmj.Best.Value));
        Assert.Equal((34.0m, -1.0m), (cmj.FirstInPeriod!.Value, cmj.ChangeInPeriod));
        Assert.Equal(36.5m, cmj.SquadAverage);
        Assert.Equal((3.0m, GoalStatus.Open), (cmj.GapToGoal, cmj.Status));

        var sprint = benchmarks["SPRINT_30M"];
        Assert.False(sprint.HigherIsBetter);
        Assert.Equal((4.20m, GoalStatus.Reached), (sprint.SquadAverage, sprint.Status));
    }

    [Fact]
    [Trait("AC", "AC-70")]
    public async Task Only_the_coach_sets_goals_within_the_plausible_range()
    {
        var athlete = await api.CreateAthleteAsync();
        var coach = api.ClientAs(Roles.Coach);

        var implausible = await coach.PutAsJsonAsync($"/api/athletes/{athlete.Id}/goals/CMJ", new GoalRequest { Target = 500 });
        var unknownTest = await coach.PutAsJsonAsync($"/api/athletes/{athlete.Id}/goals/BENCH_PRESS", new GoalRequest { Target = 100 });
        var physio = await api.ClientAs(Roles.Physio).PutAsJsonAsync($"/api/athletes/{athlete.Id}/goals/CMJ", new GoalRequest { Target = 40 });
        var set = await coach.PutAsJsonAsync($"/api/athletes/{athlete.Id}/goals/CMJ", new GoalRequest { Target = 40, Note = "Before the camp" });

        Assert.Equal(HttpStatusCode.BadRequest, implausible.StatusCode);
        Assert.Contains("Target", (await implausible.ReadAsync<ValidationProblemDetails>()).Errors.Keys);
        Assert.Equal(HttpStatusCode.BadRequest, unknownTest.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, physio.StatusCode);
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        var goal = await set.ReadAsync<GoalDto>();
        Assert.Equal(("CMJ", 40m, "Before the camp"), (goal.TestType, goal.Target, goal.Note));

        Assert.Equal(HttpStatusCode.NoContent, (await coach.DeleteAsync($"/api/athletes/{athlete.Id}/goals/CMJ")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await coach.DeleteAsync($"/api/athletes/{athlete.Id}/goals/CMJ")).StatusCode);
    }

    // ---- F21 session notes -------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-72")]
    public async Task The_coach_adds_notes_to_sessions_including_while_logging()
    {
        var athlete = await api.CreateAthleteAsync();
        var coach = api.ClientAs(Roles.Coach, "lena");
        var logged = await (await coach.PostAsJsonAsync("/api/sessions", new LogSessionRequest
        {
            AthleteId = athlete.Id, Date = Today, DurationMinutes = 60, Rpe = 7, Notes = "Felt heavy in the warm-up"
        })).ReadAsync<TrainingSessionDto>();

        var added = await coach.PostAsJsonAsync($"/api/sessions/{logged.Id}/notes", new SessionNoteRequest { Text = "Good last sprint set" });
        var empty = await coach.PostAsJsonAsync($"/api/sessions/{logged.Id}/notes", new SessionNoteRequest { Text = "" });
        var physio = await api.ClientAs(Roles.Physio).PostAsJsonAsync($"/api/sessions/{logged.Id}/notes", new SessionNoteRequest { Text = "x" });
        var unknown = await coach.PostAsJsonAsync("/api/sessions/999999/notes", new SessionNoteRequest { Text = "x" });

        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var note = await added.ReadAsync<SessionNoteDto>();
        Assert.Equal((athlete.Id, Today, "lena", 420), (note.AthleteId, note.SessionDate, note.Author, note.SessionLoad));
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, physio.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        var notes = await coach.GetFromJsonAsync<List<SessionNoteDto>>($"/api/athletes/{athlete.Id}/session-notes");
        Assert.Equal(["Good last sprint set", "Felt heavy in the warm-up"], notes!.Select(n => n.Text));
    }

    [Theory]
    [InlineData(Roles.Coach, HttpStatusCode.OK)]
    [InlineData(Roles.Physio, HttpStatusCode.OK)]
    [InlineData(Roles.Nutrition, HttpStatusCode.Forbidden)]
    [InlineData(Roles.Kitchen, HttpStatusCode.Forbidden)]
    [InlineData(Roles.Athlete, HttpStatusCode.Forbidden)]
    [Trait("AC", "AC-73")]
    public async Task Coach_and_physio_read_session_notes_and_reads_are_audited(string role, HttpStatusCode expected)
    {
        var athlete = await api.CreateAthleteAsync();
        var user = Unique("reader");

        var response = await api.ClientAs(role, user).GetAsync($"/api/athletes/{athlete.Id}/session-notes");

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(expected == HttpStatusCode.OK,
            (await AuditAsync()).Any(a => a is { Resource: "SessionNotes", Action: "Viewed" } && a.User == user && a.AthleteId == athlete.Id));
    }

    // ---- F22 progress reviews ------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-75")]
    public async Task A_review_keeps_a_snapshot_of_the_figures_of_its_period()
    {
        var athlete = await api.CreateAthleteAsync();
        await api.WithDbAsync(async db =>
        {
            db.TrainingSessions.Add(Session(athlete.Id, Today, rpe: 6, minutes: 50)); // 300
            await db.SaveChangesAsync();
        });
        var coach = api.ClientAs(Roles.Coach, "lena");

        var created = await coach.PostAsJsonAsync($"/api/athletes/{athlete.Id}/reviews", new ProgressReviewRequest
        {
            Weeks = 4, Observations = "Steady block", Recommendations = "Keep the volume"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var review = await created.ReadAsync<ProgressReviewDto>();
        Assert.Equal((4, 300, 1, "lena"), (review.Snapshot.Weeks.Count, review.Snapshot.TotalLoad, review.Snapshot.Sessions, review.Author));
        Assert.Equal(Today, review.To);

        await api.WithDbAsync(async db =>
        {
            db.TrainingSessions.Add(Session(athlete.Id, Today, rpe: 8, minutes: 60)); // arrives after the review
            await db.SaveChangesAsync();
        });
        var reread = (await coach.GetFromJsonAsync<List<ProgressReviewDto>>($"/api/athletes/{athlete.Id}/reviews"))!.Single();
        Assert.Equal(300, reread.Snapshot.TotalLoad);
    }

    [Fact]
    [Trait("AC", "AC-75")]
    public async Task A_review_needs_some_text_within_the_limits_and_is_written_by_the_coach()
    {
        var athlete = await api.CreateAthleteAsync();
        var url = $"/api/athletes/{athlete.Id}/reviews";

        var empty = await api.ClientAs(Roles.Coach).PostAsJsonAsync(url, new ProgressReviewRequest { Weeks = 4 });
        var tooLong = await api.ClientAs(Roles.Coach).PostAsJsonAsync(url, new ProgressReviewRequest { Weeks = 4, Challenges = new string('x', 2001) });
        var physio = await api.ClientAs(Roles.Physio).PostAsJsonAsync(url, new ProgressReviewRequest { Weeks = 4, Observations = "x" });

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, physio.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Physio, HttpStatusCode.OK)]
    [InlineData(Roles.Nutrition, HttpStatusCode.Forbidden)]
    [InlineData(Roles.Kitchen, HttpStatusCode.Forbidden)]
    [Trait("AC", "AC-76")]
    public async Task Coach_and_physio_read_reviews_and_reads_are_audited(string role, HttpStatusCode expected)
    {
        var athlete = await api.CreateAthleteAsync();
        var user = Unique("reader");

        var response = await api.ClientAs(role, user).GetAsync($"/api/athletes/{athlete.Id}/reviews");

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(expected == HttpStatusCode.OK,
            (await AuditAsync()).Any(a => a is { Resource: "Reviews", Action: "Viewed" } && a.User == user));
    }

    private static TrainingSession Session(int athleteId, DateOnly date, int rpe, int minutes) => new()
    {
        AthleteId = athleteId, Date = date, Type = SessionType.Strength, Rpe = rpe, DurationMinutes = minutes,
        LoggedBy = "test", LoggedAt = DateTimeOffset.UtcNow
    };
}
