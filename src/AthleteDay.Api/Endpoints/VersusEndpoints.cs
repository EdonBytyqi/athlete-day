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

/// <summary>
/// Versus mode and insights (spec v1.10, ADR-16): the same data as the athlete space, so the same readers
/// (coach, physio, nutrition), and every read audited per athlete.
/// </summary>
public static class VersusEndpoints
{
    public const int DefaultDays = 28;

    public static RouteGroupBuilder MapVersusEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/versus", Compare)
            .RequireAuthorization(Policies.ViewDashboard)
            .Audited("Versus")
            .WithTags("Versus")
            .WithSummary("Two athletes side by side over a period (default the last 28 days), with the better one per metric");

        api.MapGet("/athletes/{athleteId:int}/insights", async Task<Results<Ok<IReadOnlyList<InsightDto>>, NotFound>> (
                    int athleteId, AppDbContext db, AthleteFigures figures, CancellationToken ct) =>
                await db.Athletes.AnyAsync(a => a.Id == athleteId, ct)
                    ? TypedResults.Ok(await figures.InsightsAsync(athleteId, ct))
                    : TypedResults.NotFound())
            .RequireAuthorization(Policies.ViewDashboard)
            .Audited("Insights")
            .WithTags("Athlete space")
            .WithSummary("Rule-based insights with the figures behind them, attention first");

        return api;
    }

    private static async Task<Results<Ok<VersusDto>, ValidationProblem, NotFound>> Compare(
        int a, int b, DateOnly? from, DateOnly? to, AppDbContext db, AthleteFigures figures, AppClock clock, CancellationToken ct)
    {
        // The dashboard's date range rules (AC-58): both days included, not in the future, at most 366 days.
        var today = clock.Today;
        var end = to ?? today;
        var start = from ?? end.AddDays(-(DefaultDays - 1));
        if (a == b) return Problem("b", ApiText.VersusSameAthlete);
        if (end > today) return Problem("to", ApiText.RangeToInFuture);
        if (start > end) return Problem("from", ApiText.RangeFromAfterTo);
        if (end.DayNumber - start.DayNumber + 1 > ReportEndpoints.MaxRangeDays) return Problem("from", ApiText.RangeTooLong);

        // The archive filter applies: an archived athlete is not found.
        var athletes = await db.Athletes.AsNoTracking()
            .Where(x => x.Id == a || x.Id == b)
            .Select(x => new VersusAthleteDto(x.Id, x.Code, x.DisplayName, x.Sport.Name))
            .ToListAsync(ct);
        if (athletes.Count < 2) return TypedResults.NotFound();

        var metrics = VersusCalculator.Compare(
            await figures.VersusDataAsync(a, start, end, ct),
            await figures.VersusDataAsync(b, start, end, ct),
            start, end);
        return TypedResults.Ok(new VersusDto(start, end, athletes.Single(x => x.Id == a), athletes.Single(x => x.Id == b), metrics));
    }

    private static ValidationProblem Problem(string field, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}
