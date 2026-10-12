using AthleteDay.Contracts;

namespace AthleteDay.Core;

/// <summary>One device test result.</summary>
public sealed record DeviceResult(string TestType, DateOnly Date, decimal Value);

/// <summary>
/// One athlete's data for a comparison. <see cref="Loads"/> has one entry per session and should reach 27 days before
/// the period, so the load ratio at its end is complete; results outside the period are ignored.
/// </summary>
public sealed record VersusData(IReadOnlyList<DailyLoad> Loads, IReadOnlyList<WellnessSummaryDto> Wellness, IReadOnlyList<DeviceResult> Tests);

public enum MetricDirection
{
    HigherIsBetter,
    LowerIsBetter,
    NotRanked
}

/// <summary>Versus mode (AC-93, AC-94): the same figures for two athletes and, per metric, who is better.</summary>
public static class VersusCalculator
{
    public static VersusWinner Rank(decimal? a, decimal? b, MetricDirection direction)
    {
        if (direction == MetricDirection.NotRanked || a is not { } x || b is not { } y) return VersusWinner.NotRanked;
        if (x == y) return VersusWinner.Even;
        return (x > y) == (direction == MetricDirection.HigherIsBetter) ? VersusWinner.A : VersusWinner.B;
    }

    public static IReadOnlyList<VersusMetricDto> Compare(VersusData a, VersusData b, DateOnly from, DateOnly to)
    {
        var (figuresA, figuresB) = (Figures(a, from, to), Figures(b, from, to));
        var metrics = new List<VersusMetricDto>
        {
            Metric("totalLoad", "AU", MetricDirection.NotRanked, f => f.TotalLoad),
            Metric("sessions", null, MetricDirection.NotRanked, f => f.Sessions),
            Metric("averageLoad", "AU", MetricDirection.NotRanked, f => f.AverageLoad),
            Metric("ratio", null, MetricDirection.NotRanked, f => f.Ratio),
            Metric("sleep", "h", MetricDirection.HigherIsBetter, f => f.Sleep),
            Metric("soreness", "/5", MetricDirection.LowerIsBetter, f => f.Soreness),
            Metric("mood", "/5", MetricDirection.HigherIsBetter, f => f.Mood),
            Metric("checkIns", null, MetricDirection.HigherIsBetter, f => f.CheckIns),
        };

        // Device tests either athlete did in the period, in the usual order, ranked by the test's direction.
        foreach (var test in DeviceTests.All.Values)
        {
            var (bestA, bestB) = (Best(a, test, from, to), Best(b, test, from, to));
            if (bestA is null && bestB is null) continue;
            var direction = test.HigherIsBetter ? MetricDirection.HigherIsBetter : MetricDirection.LowerIsBetter;
            metrics.Add(new VersusMetricDto(test.Type, test.Unit, bestA, bestB, Rank(bestA, bestB, direction)));
        }
        return metrics;

        VersusMetricDto Metric(string key, string? unit, MetricDirection direction, Func<PeriodFigures, decimal?> pick) =>
            new(key, unit, pick(figuresA), pick(figuresB), Rank(pick(figuresA), pick(figuresB), direction));
    }

    private sealed record PeriodFigures(
        decimal TotalLoad, decimal Sessions, decimal AverageLoad, decimal? Ratio,
        decimal? Sleep, decimal? Soreness, decimal? Mood, decimal CheckIns);

    private static PeriodFigures Figures(VersusData data, DateOnly from, DateOnly to)
    {
        var inPeriod = data.Loads.Where(l => l.Date >= from && l.Date <= to).ToList();
        var total = inPeriod.Sum(l => l.Load);
        var byDay = data.Loads.GroupBy(l => l.Date).ToDictionary(g => g.Key, g => g.Sum(l => l.Load));
        var acute = Math.Round(LoadCalculator.RollingAverage(byDay, to, LoadCalculator.AcuteDays), 1);
        var chronic = Math.Round(LoadCalculator.RollingAverage(byDay, to, LoadCalculator.ChronicDays), 1);
        var checks = data.Wellness.Where(w => w.Date >= from && w.Date <= to).ToList();

        return new PeriodFigures(
            total,
            inPeriod.Count,
            (decimal)LoadCalculator.RangeAverage(total, to.DayNumber - from.DayNumber + 1),
            LoadCalculator.Ratio(acute, chronic) is { } ratio ? (decimal)ratio : null,
            checks.Count == 0 ? null : Math.Round(checks.Average(c => c.SleepHours), 1),
            checks.Count == 0 ? null : Math.Round((decimal)checks.Average(c => c.Soreness), 1),
            checks.Count == 0 ? null : Math.Round((decimal)checks.Average(c => c.Mood), 1),
            checks.Select(c => c.Date).Distinct().Count());
    }

    private static decimal? Best(VersusData data, DeviceTest test, DateOnly from, DateOnly to)
    {
        var values = data.Tests
            .Where(t => string.Equals(t.TestType, test.Type, StringComparison.OrdinalIgnoreCase) && t.Date >= from && t.Date <= to)
            .Select(t => t.Value)
            .ToList();
        if (values.Count == 0) return null;
        return test.HigherIsBetter ? values.Max() : values.Min();
    }
}
