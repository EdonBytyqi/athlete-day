using AthleteDay.Api.Data;
using AthleteDay.Api.Infrastructure;
using AthleteDay.Contracts;
using AthleteDay.Core;
using Microsoft.EntityFrameworkCore;

namespace AthleteDay.Api.Reports;

/// <summary>
/// Progress, benchmarks and review snapshots for one athlete. One place computes them, so a review stores exactly
/// the figures the athlete space shows (ADR-13). The calculations themselves are pure functions in AthleteDay.Core.
/// </summary>
public sealed class AthleteFigures(AppDbContext db, AppClock clock)
{
    /// <summary>Weekly progress for <paramref name="weeks"/> weeks; trends always compare 4 weeks with the 4 before.</summary>
    public async Task<ProgressDto> ProgressAsync(int athleteId, int weeks, CancellationToken ct)
    {
        var today = clock.Today;
        var span = Math.Max(weeks, 2 * ProgressCalculator.TrendWeeks);
        var from = ProgressCalculator.PeriodStart(today, span);

        var sessions = await db.TrainingSessions.AsNoTracking()
            .Where(s => s.AthleteId == athleteId && s.Date >= from && s.Date <= today)
            .Select(s => new DailyLoad(s.Date, s.Rpe * s.DurationMinutes))
            .ToListAsync(ct);
        var wellness = await WellnessAsync(athleteId, from, today, ct);

        var all = ProgressCalculator.BuildWeeks(sessions, wellness, today, span);
        return new ProgressDto(athleteId, all.TakeLast(weeks).ToList(), ProgressCalculator.Trends(all));
    }

    /// <summary>Every device test the athlete has done, against their history, the squad of their sport and their goal.</summary>
    public async Task<IReadOnlyList<BenchmarkDto>> BenchmarksAsync(int athleteId, int weeks, CancellationToken ct)
    {
        var today = clock.Today;
        var sportId = await db.Athletes.Where(a => a.Id == athleteId).Select(a => a.SportId).SingleAsync(ct);

        // Results of the active athletes of the same sport (the archive filter applies to the Athletes subquery):
        // the athlete's own history plus everyone's latest result for the squad average.
        var results = await db.DeviceMeasurements.AsNoTracking()
            .Where(m => db.Athletes.Any(a => a.Id == m.AthleteId && a.SportId == sportId))
            .Select(m => new { m.AthleteId, m.TestType, m.Date, m.Value })
            .ToListAsync(ct);
        var goals = await db.Goals.AsNoTracking().Where(g => g.AthleteId == athleteId)
            .Select(g => new GoalDto(g.TestType, g.Target, g.TargetDate, g.Note, g.SetBy))
            .ToListAsync(ct);
        var periodStart = ProgressCalculator.PeriodStart(today, weeks);
        var order = DeviceTests.All.Keys.ToList();

        return results
            .Where(r => r.AthleteId == athleteId && DeviceTests.All.ContainsKey(r.TestType))
            .GroupBy(r => DeviceTests.Get(r.TestType).Type)
            .OrderBy(g => order.IndexOf(g.Key))
            .Select(g =>
            {
                var squad = results
                    .Where(r => string.Equals(r.TestType, g.Key, StringComparison.OrdinalIgnoreCase))
                    .GroupBy(r => r.AthleteId)
                    .Select(a => a.MaxBy(r => r.Date)!.Value)
                    .ToList();
                return BenchmarkCalculator.Build(
                    DeviceTests.Get(g.Key),
                    g.Select(r => new TestResultDto(r.Date, r.Value)),
                    periodStart,
                    squad.Count > 0 ? squad.Average() : null,
                    goals.FirstOrDefault(goal => string.Equals(goal.TestType, g.Key, StringComparison.OrdinalIgnoreCase)),
                    today);
            })
            .ToList();
    }

    /// <summary>The figures of the last <paramref name="weeks"/> weeks, as a review stores them.</summary>
    public async Task<(DateOnly From, DateOnly To, ReviewSnapshotDto Snapshot)> SnapshotAsync(int athleteId, int weeks, CancellationToken ct)
    {
        var today = clock.Today;
        var from = ProgressCalculator.PeriodStart(today, weeks);
        var progress = await ProgressAsync(athleteId, weeks, ct);
        var checks = await WellnessAsync(athleteId, from, today, ct);

        var snapshot = new ReviewSnapshotDto(
            progress.Weeks,
            progress.Weeks.Sum(w => w.TotalLoad),
            progress.Weeks.Sum(w => w.Sessions),
            checks.Count == 0 ? null : Math.Round(checks.Average(c => c.SleepHours), 1),
            checks.Count == 0 ? null : Math.Round(checks.Average(c => (double)c.Soreness), 1),
            checks.Count == 0 ? null : Math.Round(checks.Average(c => (double)c.Mood), 1),
            await BenchmarksAsync(athleteId, weeks, ct));
        return (from, today, snapshot);
    }

    /// <summary>One athlete's side of a comparison (AC-93): sessions from 27 days before the end, for the ratio.</summary>
    public async Task<VersusData> VersusDataAsync(int athleteId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var loadFrom = new[] { from, to.AddDays(-(LoadCalculator.ChronicDays - 1)) }.Min();
        var loads = await LoadsAsync(athleteId, loadFrom, to, ct);
        var tests = await db.DeviceMeasurements.AsNoTracking()
            .Where(m => m.AthleteId == athleteId && m.Date >= from && m.Date <= to)
            .Select(m => new DeviceResult(m.TestType, m.Date, m.Value))
            .ToListAsync(ct);
        return new VersusData(loads, await WellnessAsync(athleteId, from, to, ct), tests);
    }

    /// <summary>The athlete's current insights (AC-97, AC-98).</summary>
    public async Task<IReadOnlyList<InsightDto>> InsightsAsync(int athleteId, CancellationToken ct)
    {
        var today = clock.Today;
        var from = today.AddDays(-(LoadCalculator.ChronicDays - 1));
        var tests = await db.DeviceMeasurements.AsNoTracking()
            .Where(m => m.AthleteId == athleteId)
            .Select(m => new DeviceResult(m.TestType, m.Date, m.Value))
            .ToListAsync(ct);
        var goals = await db.Goals.AsNoTracking().Where(g => g.AthleteId == athleteId)
            .Select(g => new GoalDto(g.TestType, g.Target, g.TargetDate, g.Note, g.SetBy))
            .ToListAsync(ct);
        var lastWeek = await Endpoints.PlanEndpoints.WeekAsync(db, athleteId, ProgressCalculator.WeekStart(today).AddDays(-7), ct);

        return InsightRules.Evaluate(new InsightInput(
            today,
            await LoadsAsync(athleteId, from, today, ct),
            await WellnessAsync(athleteId, from, today, ct),
            tests,
            goals,
            new PlanWeekLoad(lastWeek.PlannedLoad, lastWeek.LoggedLoad)));
    }

    private Task<List<DailyLoad>> LoadsAsync(int athleteId, DateOnly from, DateOnly to, CancellationToken ct) =>
        db.TrainingSessions.AsNoTracking()
            .Where(s => s.AthleteId == athleteId && s.Date >= from && s.Date <= to)
            .Select(s => new DailyLoad(s.Date, s.Rpe * s.DurationMinutes))
            .ToListAsync(ct);

    private Task<List<WellnessSummaryDto>> WellnessAsync(int athleteId, DateOnly from, DateOnly to, CancellationToken ct) =>
        db.WellnessChecks.AsNoTracking()
            .Where(w => w.AthleteId == athleteId && w.Date >= from && w.Date <= to)
            .Select(w => new WellnessSummaryDto(w.Date, w.SleepHours, w.Soreness, w.Mood))
            .ToListAsync(ct);
}
