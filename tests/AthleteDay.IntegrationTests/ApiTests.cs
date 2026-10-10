using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AthleteDay.Api.Domain;
using AthleteDay.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AthleteDay.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class ApiTests(ApiFactory api)
{
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich")).DateTime);

    [Fact]
    [Trait("AC", "AC-01")]
    public async Task Requests_without_a_persona_are_rejected()
    {
        var response = await api.CreateClient().GetAsync("/api/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("AC", "AC-05")]
    [Trait("AC", "AC-12")]
    public async Task Coach_logs_a_session_and_the_dashboard_reflects_the_load()
    {
        var athlete = await api.CreateAthleteAsync();
        var coach = api.ClientAs(Roles.Coach);

        var created = await coach.PostAsJsonAsync("/api/sessions", new LogSessionRequest
        {
            AthleteId = athlete.Id, Date = Today, Type = SessionType.Speed, DurationMinutes = 90, Rpe = 8
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(720, (await created.ReadAsync<TrainingSessionDto>()).Load);

        var row = (await coach.GetFromJsonAsync<List<DashboardRowDto>>("/api/dashboard"))!.Single(r => r.AthleteId == athlete.Id);
        Assert.Equal(720, row.TodayLoad);
        Assert.Equal(Math.Round(720 / 7.0, 1), row.AcuteLoad);
        Assert.Equal(Math.Round(720 / 28.0, 1), row.ChronicLoad);
    }

    [Fact]
    [Trait("AC", "AC-12")]
    public async Task Dashboard_sql_report_uses_the_7_and_28_day_windows()
    {
        var athlete = await api.CreateAthleteAsync();
        await api.WithDbAsync(async db =>
        {
            // 400 AU every day for 28 days, plus one session just outside the 28-day window that must be ignored.
            for (var i = 0; i < 28; i++)
                db.TrainingSessions.Add(Session(athlete.Id, Today.AddDays(-i), rpe: 5, minutes: 80));
            db.TrainingSessions.Add(Session(athlete.Id, Today.AddDays(-28), rpe: 10, minutes: 300));
            await db.SaveChangesAsync();
        });

        var row = (await api.ClientAs(Roles.Coach).GetFromJsonAsync<List<DashboardRowDto>>("/api/dashboard"))!
            .Single(r => r.AthleteId == athlete.Id);

        Assert.Equal(400, row.AcuteLoad);
        Assert.Equal(400, row.ChronicLoad);
        Assert.Equal(1.0, row.AcuteChronicRatio);
    }

    [Fact]
    [Trait("AC", "AC-07")]
    public async Task Invalid_session_returns_400_with_an_error_per_field()
    {
        var response = await api.ClientAs(Roles.Coach).PostAsJsonAsync("/api/sessions",
            new LogSessionRequest { AthleteId = 0, DurationMinutes = 2, Rpe = 11 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadAsync<ValidationProblemDetails>();
        Assert.Contains("AthleteId", problem.Errors.Keys);
        Assert.Contains("DurationMinutes", problem.Errors.Keys);
        Assert.Contains("Rpe", problem.Errors.Keys);
    }

    [Theory]
    [InlineData(Roles.Kitchen, "/api/dashboard")]
    [InlineData(Roles.Kitchen, "/api/athletes")]
    [InlineData(Roles.Coach, "/api/athletes/1/physio-notes")]
    [InlineData(Roles.Nutrition, "/api/athletes/1/physio-notes")]
    [InlineData(Roles.Coach, "/api/audit")]
    [Trait("AC", "AC-02")]
    public async Task Each_role_is_denied_what_it_does_not_need(string role, string url)
    {
        var response = await api.ClientAs(role).GetAsync(url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    [Trait("AC", "AC-20")]
    public async Task Kitchen_sees_portions_and_carbs_but_no_health_data()
    {
        var athlete = await api.CreateAthleteAsync(bodyMassKg: 80m);
        var physio = api.ClientAs(Roles.Physio);
        await physio.PostAsJsonAsync("/api/physio-notes", new PhysioNoteRequest
        {
            AthleteId = athlete.Id, Date = Today, Restriction = TrainingRestriction.Modified, Note = "Secret knee note"
        });
        var nutrition = api.ClientAs(Roles.Nutrition);
        (await nutrition.PutAsJsonAsync("/api/nutrition-targets", new NutritionTargetRequest
        {
            AthleteId = athlete.Id, Date = Today, EnergyKcal = 3900, CarbsGPerKg = 6, KitchenNote = "Vegetarian"
        })).EnsureSuccessStatusCode();

        var json = await api.ClientAs(Roles.Kitchen).GetStringAsync("/api/kitchen/today");

        var row = JsonSerializer.Deserialize<List<JsonElement>>(json)!
            .Single(r => r.GetProperty("athleteCode").GetString() == athlete.Code);
        Assert.Equal(480, row.GetProperty("carbsG").GetInt32());
        Assert.Equal("XL", row.GetProperty("portion").GetString());
        Assert.Equal(
            ["athleteCode", "displayName", "energyKcal", "carbsG", "portion", "kitchenNote"],
            row.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.DoesNotContain("knee", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("AC", "AC-09")]
    public async Task Athletes_can_only_submit_their_own_wellness_check()
    {
        var me = await api.CreateAthleteAsync();
        var someoneElse = await api.CreateAthleteAsync();
        var client = api.ClientAs(Roles.Athlete, user: me.Code);

        var own = await client.PutAsJsonAsync("/api/wellness", new WellnessCheckRequest { AthleteId = me.Id, Date = Today });
        var other = await client.PutAsJsonAsync("/api/wellness", new WellnessCheckRequest { AthleteId = someoneElse.Id, Date = Today });

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
    }

    [Fact]
    [Trait("AC", "AC-10")]
    public async Task Poor_wellness_raises_a_readiness_check_in()
    {
        var athlete = await api.CreateAthleteAsync();
        await api.ClientAs(Roles.Athlete, athlete.Code).PutAsJsonAsync("/api/wellness",
            new WellnessCheckRequest { AthleteId = athlete.Id, Date = Today, SleepHours = 5, Soreness = 4, Mood = 4 });

        var row = (await api.ClientAs(Roles.Coach).GetFromJsonAsync<List<DashboardRowDto>>("/api/dashboard"))!
            .Single(r => r.AthleteId == athlete.Id);

        Assert.Equal(ReadinessStatus.CheckIn, row.Readiness.Status);
        Assert.Contains("High soreness (4/5)", row.Readiness.Reasons);
    }

    [Fact]
    [Trait("AC", "AC-22")]
    public async Task Viewing_health_notes_is_written_to_the_audit_log()
    {
        var athlete = await api.CreateAthleteAsync();

        (await api.ClientAs(Roles.Physio, "jonas").GetAsync($"/api/athletes/{athlete.Id}/physio-notes")).EnsureSuccessStatusCode();

        var audit = await api.ClientAs(Roles.Admin).GetFromJsonAsync<List<AuditEntryDto>>("/api/audit?take=500");
        Assert.Contains(audit!, a => a is { User: "jonas", Role: Roles.Physio, Action: "Viewed", Resource: "PhysioNotes" }
                                     && a.AthleteId == athlete.Id);
    }

    [Fact]
    [Trait("AC", "AC-23")]
    public async Task Device_import_runs_through_the_queue_and_reports_rejected_rows()
    {
        var athlete = await api.CreateAthleteAsync();
        var csv = $"""
            athlete_code,date,test_type,value,unit
            {athlete.Code},{Today.AddDays(-1):yyyy-MM-dd},CMJ,38.4,cm
            {athlete.Code},{Today.AddDays(-1):yyyy-MM-dd},CMJ,140,cm
            ATH-UNKNOWN,{Today.AddDays(-1):yyyy-MM-dd},CMJ,38.4,cm
            """;
        var coach = api.ClientAs(Roles.Coach);

        var accepted = await coach.PostCsvAsync("jump-tests.csv", csv);
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        var jobId = (await accepted.ReadAsync<ImportJobDto>()).Id;

        var job = await PollUntilFinishedAsync(coach, jobId);

        Assert.Equal(ImportStatus.Completed, job.Status);
        Assert.Equal(3, job.TotalRows);
        Assert.Equal(1, job.ImportedRows);
        Assert.Equal(2, job.RejectedRows);
        Assert.Equal([3, 4], job.Errors.Select(e => e.RowNumber));
        Assert.Contains(job.Errors, e => e.Column == "value" && e.Reason.Contains("plausible range"));

        await api.WithDbAsync(async db =>
            Assert.Equal(1, await db.DeviceMeasurements.CountAsync(m => m.AthleteId == athlete.Id)));
    }

    [Fact]
    [Trait("AC", "AC-26")]
    public async Task Non_csv_uploads_are_rejected()
    {
        var response = await api.ClientAs(Roles.Coach).PostCsvAsync("photo.png", "not a csv");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<ImportJobDto> PollUntilFinishedAsync(HttpClient client, Guid jobId)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var job = (await client.GetFromJsonAsync<ImportJobDto>($"/api/imports/{jobId}"))!;
            if (job.Status is ImportStatus.Completed or ImportStatus.Failed) return job;
            await Task.Delay(100);
        }
        throw new TimeoutException("The import was not processed in time.");
    }

    private static TrainingSession Session(int athleteId, DateOnly date, int rpe, int minutes) => new()
    {
        AthleteId = athleteId, Date = date, Type = SessionType.Endurance, Rpe = rpe, DurationMinutes = minutes,
        LoggedBy = "test", LoggedAt = DateTimeOffset.UtcNow
    };
}
