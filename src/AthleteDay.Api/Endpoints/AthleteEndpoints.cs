using AthleteDay.Api.Audit;
using AthleteDay.Api.Auth;
using AthleteDay.Api.Data;
using AthleteDay.Api.Infrastructure;
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
}
