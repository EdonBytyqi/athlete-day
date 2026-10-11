using AthleteDay.Contracts;

namespace AthleteDay.Core;

/// <summary>
/// One device test of an athlete against their history, the squad and their goal (AC-69).
/// "Better" follows the test's direction: higher for jumps and distances, lower for sprint times.
/// </summary>
public static class BenchmarkCalculator
{
    /// <param name="results">All results of the athlete for this test; at least one.</param>
    /// <param name="periodStart">First day of the period for "first in period", the change and the history.</param>
    public static BenchmarkDto Build(
        DeviceTest test, IEnumerable<TestResultDto> results, DateOnly periodStart, decimal? squadAverage, GoalDto? goal, DateOnly today)
    {
        var ordered = results.OrderBy(r => r.Date).ToList();
        if (ordered.Count == 0) throw new ArgumentException("A benchmark needs at least one result.", nameof(results));

        var latest = ordered[^1];
        var best = test.HigherIsBetter ? ordered.MaxBy(r => r.Value)! : ordered.MinBy(r => r.Value)!;
        var inPeriod = ordered.Where(r => r.Date >= periodStart).ToList();
        var first = inPeriod.FirstOrDefault();

        decimal? gap = goal is null ? null : test.HigherIsBetter ? goal.Target - latest.Value : latest.Value - goal.Target;
        GoalStatus? status = goal is null ? null
            : test.Meets(latest.Value, goal.Target) ? GoalStatus.Reached
            : goal.TargetDate is { } date && date < today ? GoalStatus.Behind
            : GoalStatus.Open;

        return new BenchmarkDto(
            test.Type, test.Unit, test.HigherIsBetter,
            latest, best,
            first, first is null ? null : latest.Value - first.Value,
            squadAverage is { } average ? Math.Round(average, 2) : null,
            goal, gap, status,
            inPeriod);
    }
}
