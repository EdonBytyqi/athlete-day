using AthleteDay.Contracts;

namespace AthleteDay.Core;

public sealed record DailyLoad(DateOnly Date, int Load);

public sealed record LoadSummary(int TodayLoad, double Acute7, double Chronic28, double? Ratio);

/// <summary>
/// Session-RPE training load (Foster): RPE (1–10) × duration in minutes, in arbitrary units (AU).
/// The 7-day vs 28-day comparison is a monitoring aid for the coach, not an injury predictor.
/// </summary>
public static class LoadCalculator
{
    public const int AcuteDays = 7;
    public const int ChronicDays = 28;

    public static int SessionLoad(int rpe, int durationMinutes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rpe, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rpe, 10);
        ArgumentOutOfRangeException.ThrowIfNegative(durationMinutes);
        return rpe * durationMinutes;
    }

    /// <summary>Average daily load over the window ending on <paramref name="end"/>. Days without training count as 0.</summary>
    public static double RollingAverage(IReadOnlyDictionary<DateOnly, int> loadsByDay, DateOnly end, int days)
    {
        var sum = 0;
        for (var i = 0; i < days; i++)
        {
            sum += loadsByDay.GetValueOrDefault(end.AddDays(-i));
        }
        return (double)sum / days;
    }

    public static double? Ratio(double acute, double chronic) =>
        chronic <= 0 ? null : Math.Round(acute / chronic, 2);

    /// <summary>Average daily load over a period of <paramref name="days"/> days; rest days count as 0.</summary>
    public static double RangeAverage(int totalLoad, int days)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(days, 1);
        return Math.Round((double)totalLoad / days, 1);
    }

    /// <summary>Builds the summary from window sums, as returned by the dashboard SQL report.</summary>
    public static LoadSummary FromSums(int todayLoad, int acuteSum, int chronicSum)
    {
        var acute = Math.Round((double)acuteSum / AcuteDays, 1);
        var chronic = Math.Round((double)chronicSum / ChronicDays, 1);
        return new LoadSummary(todayLoad, acute, chronic, Ratio(acute, chronic));
    }

    /// <summary>One point per day for the last <paramref name="days"/> days, with rolling 7- and 28-day averages.</summary>
    public static IReadOnlyList<LoadTrendPointDto> BuildTrend(IEnumerable<DailyLoad> dailyLoads, DateOnly end, int days = 28)
    {
        var byDay = dailyLoads
            .GroupBy(d => d.Date)
            .ToDictionary(g => g.Key, g => g.Sum(d => d.Load));

        var points = new List<LoadTrendPointDto>(days);
        for (var date = end.AddDays(-(days - 1)); date <= end; date = date.AddDays(1))
        {
            points.Add(new LoadTrendPointDto(
                date,
                byDay.GetValueOrDefault(date),
                Math.Round(RollingAverage(byDay, date, AcuteDays), 1),
                Math.Round(RollingAverage(byDay, date, ChronicDays), 1)));
        }
        return points;
    }
}
