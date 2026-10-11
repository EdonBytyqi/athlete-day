using AthleteDay.Api.Audit;
using AthleteDay.Api.Auth;
using AthleteDay.Api.Data;
using AthleteDay.Api.Domain;
using AthleteDay.Api.Infrastructure;
using AthleteDay.Api.Realtime;
using AthleteDay.Api.Reports;
using AthleteDay.Contracts;
using AthleteDay.Core;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AthleteDay.Api.Endpoints;

public static class AthleteEndpoints
{
    public static RouteGroupBuilder MapAthleteEndpoints(this RouteGroupBuilder api)
    {
        var athletes = api.MapGroup("/athletes").WithTags("Athletes");

        athletes.MapGet("/", async (AppDbContext db, CancellationToken ct) =>
                await db.Athletes.AsNoTracking()
                    .OrderBy(a => a.Code)
                    .Select(a => new AthleteDto(a.Id, a.Code, a.DisplayName, a.Sport))
                    .ToListAsync(ct))
            .RequireAuthorization(Policies.ReadAthletes)
            .WithSummary("List athletes (pseudonymous codes only)");

        // ---- Squad management (coach). Archive instead of delete, see ADR-07. ----

        athletes.MapGet("/archived", async (AppDbContext db, CancellationToken ct) =>
                await db.Athletes.IgnoreQueryFilters().AsNoTracking()
                    .Where(a => a.ArchivedAt != null)
                    .OrderByDescending(a => a.ArchivedAt)
                    .Select(a => ToDetails(a))
                    .ToListAsync(ct))
            .RequireAuthorization(Policies.ManageSquad)
            .WithSummary("Archived athletes, most recently archived first");

        athletes.MapGet("/{athleteId:int}", async Task<Results<Ok<AthleteDetailsDto>, NotFound>> (
                    int athleteId, AppDbContext db, CancellationToken ct) =>
                await db.Athletes.AsNoTracking().Where(a => a.Id == athleteId).Select(a => ToDetails(a)).SingleOrDefaultAsync(ct)
                    is { } athlete ? TypedResults.Ok(athlete) : TypedResults.NotFound())
            .RequireAuthorization(Policies.ViewDashboard)
            .WithSummary("Name, sport, birth year, sex and body mass of an athlete");

        athletes.MapPost("/", AddAthlete)
            .RequireAuthorization(Policies.ManageSquad)
            .Audited("Athlete")
            .WithSummary("Add an athlete; the system assigns the next free code");

        athletes.MapPut("/{athleteId:int}", EditAthlete)
            .RequireAuthorization(Policies.ManageSquad)
            .Audited("Athlete")
            .WithSummary("Edit an athlete's details; the code never changes");

        athletes.MapDelete("/{athleteId:int}", ArchiveAthlete)
            .RequireAuthorization(Policies.ManageSquad)
            .Audited("Athlete")
            .WithSummary("Archive an athlete: hidden everywhere, history kept, can be restored");

        athletes.MapPost("/{athleteId:int}/restore", RestoreAthlete)
            .RequireAuthorization(Policies.ManageSquad)
            .Audited("Athlete")
            .WithSummary("Bring an archived athlete back into the squad");

        athletes.MapGet("/{athleteId:int}/load-trend",
                async Task<Results<Ok<LoadTrendDto>, NotFound>> (int athleteId, int? days, AppDbContext db,
                    DashboardReport report, AppClock clock, CancellationToken ct) =>
                {
                    var code = await db.Athletes.Where(a => a.Id == athleteId).Select(a => a.Code).SingleOrDefaultAsync(ct);
                    if (code is null) return TypedResults.NotFound();

                    var window = Math.Clamp(days ?? 28, 7, 90);
                    var loads = await report.GetDailyLoadsAsync(athleteId, clock.Today, window, ct);
                    return TypedResults.Ok(new LoadTrendDto(athleteId, code, LoadCalculator.BuildTrend(loads, clock.Today, window)));
                })
            .RequireAuthorization(Policies.ViewDashboard)
            .Audited("LoadTrend")
            .WithSummary("Daily session-RPE load with rolling 7-day and 28-day averages");

        // Spec F13 (AC-43 – AC-48)
        athletes.MapGet("/{athleteId:int}/wellness",
                async Task<Results<Ok<List<WellnessDayDto>>, ValidationProblem, NotFound>> (int athleteId, int? days,
                    AppDbContext db, AppClock clock, CancellationToken ct) =>
                {
                    var window = days ?? 14;
                    if (window is < 1 or > 28)
                        return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                        {
                            ["days"] = ["days must be between 1 and 28."]
                        });

                    if (!await db.Athletes.AnyAsync(a => a.Id == athleteId, ct)) return TypedResults.NotFound();

                    var today = clock.Today;
                    var from = today.AddDays(-(window - 1));
                    var checks = await db.WellnessChecks.AsNoTracking()
                        .Where(w => w.AthleteId == athleteId && w.Date >= from && w.Date <= today)
                        .OrderByDescending(w => w.Date)
                        .Select(w => new WellnessSummaryDto(w.Date, w.SleepHours, w.Soreness, w.Mood))
                        .ToListAsync(ct);

                    return TypedResults.Ok(checks
                        .Select(c => new WellnessDayDto(c.Date, c.SleepHours, c.Soreness, c.Mood, ReadinessEvaluator.WellnessFlags(c)))
                        .ToList());
                })
            .RequireAuthorization(Policies.WellnessHistory)
            .Audited("WellnessHistory")
            .WithSummary("Morning wellness checks of the last days, with flags (coach and physio only)");

        athletes.MapGet("/{athleteId:int}/sessions", async (int athleteId, AppDbContext db, CancellationToken ct) =>
                await db.TrainingSessions.AsNoTracking()
                    .Where(s => s.AthleteId == athleteId)
                    .OrderByDescending(s => s.Date).ThenByDescending(s => s.Id)
                    .Take(20)
                    .Select(s => new TrainingSessionDto(s.Id, s.AthleteId, s.Athlete.Code, s.Date, s.Type,
                        s.DurationMinutes, s.Rpe, s.Rpe * s.DurationMinutes, s.LoggedBy))
                    .ToListAsync(ct))
            .RequireAuthorization(Policies.ViewDashboard)
            .Audited("Sessions")
            .WithSummary("Most recent training sessions of an athlete");

        athletes.MapGet("/{athleteId:int}/measurements", async (int athleteId, AppDbContext db, CancellationToken ct) =>
                await db.DeviceMeasurements.AsNoTracking()
                    .Where(m => m.AthleteId == athleteId)
                    .OrderByDescending(m => m.Date).ThenBy(m => m.TestType)
                    .Take(50)
                    .Select(m => new MeasurementDto(m.Date, m.TestType, m.Value, m.Unit, m.ImportJobId))
                    .ToListAsync(ct))
            .RequireAuthorization(Policies.ViewDashboard)
            .Audited("Measurements")
            .WithSummary("Imported device measurements (jump tests, GPS)");

        return api;
    }

    private static async Task<Created<AthleteDetailsDto>> AddAthlete(
        AthleteRequest request, AppDbContext db, IDashboardNotifier notifier, CancellationToken ct)
    {
        // "Read the highest code, then insert" must not interleave, or two coaches get the same code.
        // An application lock serialises it for the length of the transaction. The execution strategy
        // (retry on transient failures) requires the transaction to run inside it.
        var athlete = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear(); // a retried attempt starts clean
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlRawAsync(
                "EXEC sp_getapplock @Resource = 'AthleteCode', @LockMode = 'Exclusive', @LockOwner = 'Transaction';", ct);

            var added = new Athlete
            {
                Code = await NextCodeAsync(db, ct),
                DisplayName = request.DisplayName.Trim(),
                Sport = request.Sport.Trim()
            };
            Apply(request, added);
            db.Athletes.Add(added);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return added;
        });
        await notifier.DataChangedAsync("athlete", ct);

        return TypedResults.Created($"/api/athletes/{athlete.Id}", ToDetails(athlete));
    }

    private static async Task<Results<Ok<AthleteDetailsDto>, NotFound>> EditAthlete(
        int athleteId, AthleteRequest request, AppDbContext db, IDashboardNotifier notifier, CancellationToken ct)
    {
        var athlete = await db.Athletes.FindAsync([athleteId], ct);
        if (athlete is null) return TypedResults.NotFound();

        Apply(request, athlete);
        await db.SaveChangesAsync(ct);
        await notifier.DataChangedAsync("athlete", ct);
        return TypedResults.Ok(ToDetails(athlete));
    }

    private static async Task<Results<NoContent, NotFound>> ArchiveAthlete(
        int athleteId, AppDbContext db, AppClock clock, IDashboardNotifier notifier, CancellationToken ct)
    {
        var athlete = await db.Athletes.FindAsync([athleteId], ct);
        if (athlete is null) return TypedResults.NotFound();

        athlete.ArchivedAt = clock.Now;
        await db.SaveChangesAsync(ct);
        await notifier.DataChangedAsync("athlete", ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<AthleteDetailsDto>, NotFound>> RestoreAthlete(
        int athleteId, AppDbContext db, IDashboardNotifier notifier, CancellationToken ct)
    {
        var athlete = await db.Athletes.IgnoreQueryFilters().SingleOrDefaultAsync(a => a.Id == athleteId, ct);
        if (athlete is null) return TypedResults.NotFound();

        athlete.ArchivedAt = null;
        await db.SaveChangesAsync(ct);
        await notifier.DataChangedAsync("athlete", ct);
        return TypedResults.Ok(ToDetails(athlete));
    }

    private static void Apply(AthleteRequest request, Athlete athlete)
    {
        athlete.DisplayName = request.DisplayName.Trim();
        athlete.Sport = request.Sport.Trim();
        athlete.BirthYear = request.BirthYear;
        athlete.Sex = request.Sex;
        athlete.BodyMassKg = request.BodyMassKg;
    }

    /// <summary>ATH- plus the highest number so far + 1. Archived athletes count too, so a code is never reused.</summary>
    private static async Task<string> NextCodeAsync(AppDbContext db, CancellationToken ct)
    {
        var codes = await db.Athletes.IgnoreQueryFilters()
            .Where(a => a.Code.StartsWith("ATH-"))
            .Select(a => a.Code)
            .ToListAsync(ct);
        var highest = codes.Select(c => int.TryParse(c[4..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"ATH-{highest + 1:000}";
    }

    private static AthleteDetailsDto ToDetails(Athlete a) =>
        new(a.Id, a.Code, a.DisplayName, a.Sport, a.BirthYear, a.Sex, a.BodyMassKg, a.ArchivedAt);
}
