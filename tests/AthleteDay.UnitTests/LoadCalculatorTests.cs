using AthleteDay.Core;

namespace AthleteDay.UnitTests;

public class LoadCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 10, 10);

    [Theory]
    [InlineData(7, 60, 420)]
    [InlineData(1, 30, 30)]
    [InlineData(10, 120, 1200)]
    [Trait("AC", "AC-05")]
    public void Session_load_is_rpe_times_minutes(int rpe, int minutes, int expected) =>
        Assert.Equal(expected, LoadCalculator.SessionLoad(rpe, minutes));

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [Trait("AC", "AC-06")]
    public void Session_load_rejects_rpe_outside_1_to_10(int rpe) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => LoadCalculator.SessionLoad(rpe, 60));

    [Fact]
    [Trait("AC", "AC-11")]
    public void Rolling_average_counts_rest_days_as_zero()
    {
        var loads = new Dictionary<DateOnly, int>
        {
            [Today] = 700,
            [Today.AddDays(-2)] = 700
        };

        Assert.Equal(200, LoadCalculator.RollingAverage(loads, Today, 7));
    }

    [Fact]
    [Trait("AC", "AC-11")]
    public void Rolling_average_ignores_days_outside_the_window()
    {
        var loads = new Dictionary<DateOnly, int>
        {
            [Today] = 70,
            [Today.AddDays(-7)] = 10_000 // one day before the 7-day window
        };

        Assert.Equal(10, LoadCalculator.RollingAverage(loads, Today, 7));
    }

    [Fact]
    [Trait("AC", "AC-12")]
    public void Summary_from_sums_divides_by_window_length_and_computes_ratio()
    {
        var summary = LoadCalculator.FromSums(todayLoad: 500, acuteSum: 3500, chronicSum: 11200);

        Assert.Equal(500, summary.TodayLoad);
        Assert.Equal(500, summary.Acute7);
        Assert.Equal(400, summary.Chronic28);
        Assert.Equal(1.25, summary.Ratio);
    }

    [Fact]
    [Trait("AC", "AC-13")]
    public void Ratio_is_null_without_chronic_load()
    {
        Assert.Null(LoadCalculator.Ratio(acute: 300, chronic: 0));
    }

    [Fact]
    [Trait("AC", "AC-14")]
    public void Trend_has_one_point_per_day_ending_today()
    {
        var trend = LoadCalculator.BuildTrend([], Today, days: 28);

        Assert.Equal(28, trend.Count);
        Assert.Equal(Today.AddDays(-27), trend[0].Date);
        Assert.Equal(Today, trend[^1].Date);
        Assert.All(trend, p => Assert.Equal(0, p.DailyLoad));
    }

    [Fact]
    [Trait("AC", "AC-14")]
    public void Trend_sums_multiple_sessions_on_the_same_day()
    {
        var trend = LoadCalculator.BuildTrend(
            [new DailyLoad(Today, 300), new DailyLoad(Today, 400)], Today, days: 7);

        Assert.Equal(700, trend[^1].DailyLoad);
        Assert.Equal(100, trend[^1].Acute7);
        Assert.Equal(25, trend[^1].Chronic28);
    }

    [Fact]
    [Trait("AC", "AC-14")]
    public void Constant_training_gives_equal_acute_and_chronic_averages()
    {
        var history = Enumerable.Range(0, 60).Select(i => new DailyLoad(Today.AddDays(-i), 400));

        var last = LoadCalculator.BuildTrend(history, Today, days: 28)[^1];

        Assert.Equal(400, last.Acute7);
        Assert.Equal(400, last.Chronic28);
    }

    [Theory]
    [InlineData(700, 3, 233.3)]
    [InlineData(0, 1, 0)]
    [InlineData(1400, 7, 200)]
    [Trait("AC", "AC-59")]
    public void Range_average_counts_rest_days_as_zero(int totalLoad, int days, double expected) =>
        Assert.Equal(expected, LoadCalculator.RangeAverage(totalLoad, days));
}
