using AthleteDay.Api.Audit;
using AthleteDay.Api.Auth;
using AthleteDay.Api.Data;
using AthleteDay.Api.Infrastructure;
using AthleteDay.Api.Reports;
using AthleteDay.Api.Resources;
using AthleteDay.Contracts;
using AthleteDay.Core;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AthleteDay.Api.Endpoints;

public static class ReportEndpoints
{
    public const int MaxRangeDays = 366;

    public static RouteGroupBuilder MapReportEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/dashboard", async Task<Results<Ok<IReadOnlyList<DashboardRowDto>>, ValidationProblem>> (
                    DateOnly? from, DateOnly? to, DashboardReport report, AppClock clock, CancellationToken ct) =>
                {
                    // A period (AC-58): both default to today; figures "as of" are those of the end date.
                    var today = clock.Today;
                    var end = to ?? today;
                    var start = from ?? end;
                    if (end > today) return RangeProblem("to", ApiText.RangeToInFuture);
                    if (start > end) return RangeProblem("from", ApiText.RangeFromAfterTo);
                    if (end.DayNumber - start.DayNumber + 1 > MaxRangeDays) return RangeProblem("from", ApiText.RangeTooLong);

                    return TypedResults.Ok(await report.GetAsync(today, start, end, ct));
                })
            .RequireAuthorization(Policies.ViewDashboard)
            .Audited("Dashboard")
            .WithTags("Dashboard")
            .WithSummary("Per-athlete load, 7/28-day trend, readiness flags and today's nutrition (Dapper + T-SQL)");

        api.MapGet("/kitchen/today", async (AppDbContext db, AppClock clock, CancellationToken ct) =>
            {
                var today = clock.Today;
                var rows = await db.NutritionTargets.AsNoTracking()
                    .Where(n => n.Date == today)
                    .OrderBy(n => n.Athlete.Code)
                    .Select(n => new { n.Athlete.Code, n.Athlete.DisplayName, n.Athlete.BodyMassKg, n.EnergyKcal, n.CarbsGPerKg, n.KitchenNote })
                    .ToListAsync(ct);

                // Privacy by design: this shape has no field that could carry health data.
                return rows.Select(r => new KitchenRowDto(
                    r.Code, r.DisplayName, r.EnergyKcal,
                    NutritionCalculator.CarbsGrams(r.CarbsGPerKg, r.BodyMassKg),
                    NutritionCalculator.Portion(r.EnergyKcal),
                    r.KitchenNote)).ToList();
            })
            .RequireAuthorization(Policies.ViewKitchen)
            .WithTags("Kitchen")
            .WithSummary("Today's portions and carbohydrate needs per athlete, without health data");

        api.MapGet("/audit", async (int? take, AppDbContext db, CancellationToken ct) =>
                await db.AuditEntries.AsNoTracking()
                    .OrderByDescending(a => a.Id)
                    .Take(Math.Clamp(take ?? 100, 1, 500))
                    .Select(a => new AuditEntryDto(a.At, a.User, a.Role, a.Action, a.Resource, a.AthleteId))
                    .ToListAsync(ct))
            .RequireAuthorization(Policies.ViewAudit)
            .WithTags("Audit")
            .WithSummary("Who viewed or changed health-related data");

        return api;
    }

    private static ValidationProblem RangeProblem(string field, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}
