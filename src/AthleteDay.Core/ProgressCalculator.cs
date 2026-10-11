using AthleteDay.Contracts;

namespace AthleteDay.Core;

/// <summary>
/// Weekly progress and trends for the athlete space (AC-67). Weeks run Monday to Sunday; trends compare the last
/// 4 weeks with the 4 before, using the thresholds in section 4 of the spec.
/// </summary>
public static class ProgressCalculator
{
    public const int TrendWeeks = 4;
    public const double AmountThreshold = 0.10;
    public const double ScoreThreshold = 0.3;

    public static DateOnly WeekStart(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    /// <summary>The first day of a period of <paramref name="weeks"/> whole weeks ending with the week of <paramref name="today"/>.</summary>
    public static DateOnly PeriodStart(DateOnly today, int weeks) => WeekStart(today).AddDays(-7 * (weeks - 1));

    /// <summary>One entry per week, oldest first, ending with the week of <paramref name="today"/>.</summary>
    public static IReadOnlyList<WeeklyProgressDto> BuildWeeks(
        IEnumerable<DailyLoad> sessions, IEnumerable<WellnessSummaryDto> wellness, DateOnly today, int weeks)
    {
        var sessionsByWeek = sessions.ToLookup(s => WeekStart(s.Date));
        var wellnessByWeek = wellness.ToLookup(w => WeekStart(w.Date));

        return Enumerable.Range(0, weeks)
            .Select(i => PeriodStart(today, weeks).AddDays(7 * i))
            .Select(start =>
            {
                var load = sessionsByWeek[start].ToList();
                var checks = wellnessByWeek[start].ToList();
                return new WeeklyProgressDto(
                    start,
                    load.Sum(s => s.Load),
                    load.Count,
                    checks.Count == 0 ? null : Math.Round(checks.Average(c => c.SleepHours), 1),
                    checks.Count == 0 ? null : Math.Round(checks.Average(c => (double)c.Soreness), 1),
                    checks.Count == 0 ? null : Math.Round(checks.Average(c => (double)c.Mood), 1));
            })
            .ToList();
    }

    /// <summary>Last 4 weeks against the 4 before; needs at least 8 weeks for a full comparison.</summary>
    public static IReadOnlyList<TrendDto> Trends(IReadOnlyList<WeeklyProgressDto> weeks)
    {
        var recent = weeks.TakeLast(TrendWeeks).ToList();
        var previous = weeks.SkipLast(TrendWeeks).TakeLast(TrendWeeks).ToList();

        TrendDto Amount(string metric, Func<WeeklyProgressDto, int> pick)
        {
            double r = recent.Sum(pick), p = previous.Sum(pick);
            return new(metric, r, p, TrendOfAmount(r, p));
        }

        TrendDto Score(string metric, Func<WeeklyProgressDto, double?> pick)
        {
            double? r = Average(recent, pick), p = Average(previous, pick);
            return new(metric, r, p, TrendOfScore(r, p));
        }

        return
        [
            Amount("load", w => w.TotalLoad),
            Amount("sessions", w => w.Sessions),
            Score("sleep", w => (double?)w.AverageSleep),
            Score("soreness", w => w.AverageSoreness),
            Score("mood", w => w.AverageMood)
        ];
    }

    /// <summary>Load and sessions: a change of more than 10 % counts.</summary>
    public static Trend TrendOfAmount(double recent, double previous)
    {
        if (previous == 0) return recent > 0 ? Trend.Up : Trend.Stable;
        var change = (recent - previous) / previous;
        return change > AmountThreshold ? Trend.Up : change < -AmountThreshold ? Trend.Down : Trend.Stable;
    }

    /// <summary>Wellness scores: a difference of more than 0.3 counts; exactly 0.3 does not.</summary>
    public static Trend TrendOfScore(double? recent, double? previous)
    {
        if (recent is not { } r || previous is not { } p) return Trend.Stable;
        var difference = Math.Round(r - p, 2); // 7.0 - 6.7 is 0.30000000000000027 in binary floating point
        return difference > ScoreThreshold ? Trend.Up : difference < -ScoreThreshold ? Trend.Down : Trend.Stable;
    }

    // The average of the weekly averages, over the weeks that have morning checks.
    private static double? Average(IEnumerable<WeeklyProgressDto> weeks, Func<WeeklyProgressDto, double?> pick)
    {
        var values = weeks.Select(pick).OfType<double>().ToList();
        return values.Count == 0 ? null : Math.Round(values.Average(), 1);
    }
}
