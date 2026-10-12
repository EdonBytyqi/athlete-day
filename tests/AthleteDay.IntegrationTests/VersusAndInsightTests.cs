using System.Net;
using System.Net.Http.Json;
using AthleteDay.Api.Domain;
using AthleteDay.Contracts;

namespace AthleteDay.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class VersusAndInsightTests(ApiFactory api)
{
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich")).DateTime);

    private static DateOnly ThisMonday => Today.AddDays(-(((int)Today.DayOfWeek + 6) % 7));

    private static TrainingSession Session(int athleteId, DateOnly date, int minutes, int rpe) => new()
    {
        AthleteId = athleteId, Date = date, Type = SessionType.Strength, DurationMinutes = minutes, Rpe = rpe,
        LoggedBy = "test", LoggedAt = DateTimeOffset.UtcNow
    };

    private async Task<List<AuditEntryDto>> AuditAsync() =>
        (await api.ClientAs(Roles.Admin).GetFromJsonAsync<List<AuditEntryDto>>("/api/audit?take=500"))!;

    // ---- F25 Versus mode -------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-93")]
    public async Task Two_athletes_are_compared_over_the_last_28_days_by_default()
    {
        var a = await api.CreateAthleteAsync();
        var b = await api.CreateAthleteAsync();
        await api.WithDbAsync(async db =>
        {
            var job = new ImportJob { Id = Guid.NewGuid(), FileName = "v.csv", BlobName = "v", UploadedBy = "test", Status = ImportStatus.Completed };
            db.ImportJobs.Add(job);
            db.TrainingSessions.AddRange(
                Session(a.Id, Today, 60, 5), Session(a.Id, Today.AddDays(-27), 30, 10), Session(a.Id, Today.AddDays(-28), 100, 10),
                Session(b.Id, Today.AddDays(-1), 90, 8));
            db.WellnessChecks.AddRange(
                new WellnessCheck { AthleteId = a.Id, Date = Today, SleepHours = 8.0m, Soreness = 2, Mood = 4 },
                new WellnessCheck { AthleteId = b.Id, Date = Today, SleepHours = 6.0m, Soreness = 3, Mood = 4 });
            db.DeviceMeasurements.AddRange(
                new DeviceMeasurement { AthleteId = a.Id, Date = Today.AddDays(-3), TestType = "CMJ", Value = 38.0m, Unit = "cm", ImportJobId = job.Id },
                new DeviceMeasurement { AthleteId = b.Id, Date = Today.AddDays(-3), TestType = "CMJ", Value = 41.0m, Unit = "cm", ImportJobId = job.Id });
            await db.SaveChangesAsync();
        });

        var versus = await api.ClientAs(Roles.Coach).GetFromJsonAsync<VersusDto>($"/api/versus?a={a.Id}&b={b.Id}");

        Assert.Equal((Today.AddDays(-27), Today), (versus!.From, versus.To));
        Assert.Equal((a.Id, a.Code, b.Id), (versus.A.Id, versus.A.Code, versus.B.Id));
        var metrics = versus.Metrics.ToDictionary(m => m.Key);
        Assert.Equal((600m, 720m), (metrics["totalLoad"].A, metrics["totalLoad"].B)); // the session 28 days ago is outside
        Assert.Equal((2m, 1m), (metrics["sessions"].A, metrics["sessions"].B));
        Assert.Equal(VersusWinner.A, metrics["sleep"].Winner);
        Assert.Equal(VersusWinner.Even, metrics["mood"].Winner);
        Assert.Equal(VersusWinner.B, metrics["CMJ"].Winner);
        Assert.Equal(VersusWinner.NotRanked, metrics["totalLoad"].Winner);

        var week = await api.ClientAs(Roles.Physio).GetFromJsonAsync<VersusDto>(
            $"/api/versus?a={a.Id}&b={b.Id}&from={Today.AddDays(-6):yyyy-MM-dd}&to={Today:yyyy-MM-dd}");
        Assert.Equal(300m, week!.Metrics.Single(m => m.Key == "totalLoad").A);
    }

    [Fact]
    [Trait("AC", "AC-93")]
    public async Task Versus_checks_roles_athletes_and_the_period()
    {
        var a = await api.CreateAthleteAsync();
        var b = await api.CreateAthleteAsync();
        var archived = await api.CreateAthleteAsync();
        await api.WithDbAsync(async db =>
        {
            (await db.Athletes.FindAsync(archived.Id))!.ArchivedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        });
        var url = $"/api/versus?a={a.Id}&b={b.Id}";

        Assert.Equal(HttpStatusCode.OK, (await api.ClientAs(Roles.Nutrition).GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.ClientAs(Roles.Kitchen).GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.ClientAs(Roles.Athlete, a.Code).GetAsync(url)).StatusCode);

        var coach = api.ClientAs(Roles.Coach);
        Assert.Equal(HttpStatusCode.BadRequest, (await coach.GetAsync($"/api/versus?a={a.Id}&b={a.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await coach.GetAsync($"{url}&to={Today.AddDays(1):yyyy-MM-dd}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await coach.GetAsync($"{url}&from={Today.AddDays(-400):yyyy-MM-dd}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await coach.GetAsync($"/api/versus?a={a.Id}&b=999999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await coach.GetAsync($"/api/versus?a={archived.Id}&b={b.Id}")).StatusCode);
    }

    [Fact]
    [Trait("AC", "AC-95")]
    public async Task A_comparison_is_audited_for_both_athletes()
    {
        var a = await api.CreateAthleteAsync();
        var b = await api.CreateAthleteAsync();

        (await api.ClientAs(Roles.Coach).GetAsync($"/api/versus?a={a.Id}&b={b.Id}")).EnsureSuccessStatusCode();

        var audit = await AuditAsync();
        Assert.Contains(audit, e => e is { Resource: "Versus", Action: "Viewed", User: "coach" } && e.AthleteId == a.Id);
        Assert.Contains(audit, e => e is { Resource: "Versus", Action: "Viewed", User: "coach" } && e.AthleteId == b.Id);
    }

    // ---- F26 Insights ----------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-98")]
    public async Task Insights_come_from_the_athletes_data_attention_first()
    {
        var athlete = await api.CreateAthleteAsync();
        var coach = api.ClientAs(Roles.Coach);
        // A standard week with one Monday session of 300 AU; last week four times that was logged.
        (await coach.PutAsJsonAsync($"/api/athletes/{athlete.Id}/plan/template", new PlanTemplateRequest
        {
            Days = [new() { DayOfWeek = 1, Sessions = [new() { Type = SessionType.Strength, DurationMinutes = 60, Rpe = 5 }] }]
        })).EnsureSuccessStatusCode();
        await api.WithDbAsync(async db =>
        {
            // 21 quiet days, then 7 hard ones: a load spike.
            for (var i = 0; i < 28; i++)
                db.TrainingSessions.Add(Session(athlete.Id, Today.AddDays(-i), 60, i < 7 ? 8 : 3));
            db.TrainingSessions.Add(Session(athlete.Id, ThisMonday.AddDays(-7), 120, 6));
            await db.SaveChangesAsync();
        });

        var insights = await api.ClientAs(Roles.Nutrition).GetFromJsonAsync<List<InsightDto>>($"/api/athletes/{athlete.Id}/insights");

        Assert.Equal(InsightKind.LoadSpike, insights![0].Kind);
        Assert.Equal(InsightTone.Attention, insights[0].Tone);
        Assert.True(insights[0].Figures["ratio"] > 1.3m);
        Assert.Contains(insights, i => i.Kind == InsightKind.MissingCheckIns);
        Assert.Contains(insights, i => i.Kind == InsightKind.PlanOffTrack && i.Figures["planned"] == 300m);
        Assert.Equal(InsightTone.Info, insights[^1].Tone);
    }

    [Fact]
    [Trait("AC", "AC-98")]
    public async Task Insights_are_for_coach_physio_and_nutrition_and_reads_are_audited()
    {
        var athlete = await api.CreateAthleteAsync();
        var url = $"/api/athletes/{athlete.Id}/insights";

        Assert.Equal(HttpStatusCode.OK, (await api.ClientAs(Roles.Physio).GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.ClientAs(Roles.Kitchen).GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.ClientAs(Roles.Athlete, athlete.Code).GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.ClientAs(Roles.Coach).GetAsync("/api/athletes/999999/insights")).StatusCode);

        Assert.Contains(await AuditAsync(), e => e is { Resource: "Insights", Action: "Viewed", User: "physio" } && e.AthleteId == athlete.Id);
    }
}
