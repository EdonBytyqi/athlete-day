using AthleteDay.Contracts;
using AthleteDay.Core;

namespace AthleteDay.UnitTests;

public class ProgressAndBenchmarkTests
{
    private static readonly DateOnly Wednesday = new(2026, 10, 14);

    [Fact]
    [Trait("AC", "AC-67")]
    public void Weeks_run_Monday_to_Sunday_and_end_with_the_current_week()
    {
        var weeks = ProgressCalculator.BuildWeeks(
            sessions: [new(Wednesday, 600), new(Wednesday.AddDays(-2), 400), new(Wednesday.AddDays(-3), 300)],
            wellness: [new(Wednesday, 7.0m, 2, 4), new(Wednesday.AddDays(-1), 8.0m, 4, 2)],
            today: Wednesday, weeks: 3);

        Assert.Equal([new DateOnly(2026, 9, 28), new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 12)], weeks.Select(w => w.WeekStart));
        var current = weeks[^1];
        Assert.Equal((1000, 2), (current.TotalLoad, current.Sessions));            // Monday 12th and Wednesday 14th
        Assert.Equal((7.5m, 3.0, 3.0), (current.AverageSleep, current.AverageSoreness, current.AverageMood));
        Assert.Equal((300, 1), (weeks[1].TotalLoad, weeks[1].Sessions));          // Sunday 11th belongs to the week before
        Assert.Null(weeks[0].AverageSleep);
    }

    [Theory]
    [InlineData(1000, 900, Trend.Up)]      // 11 % more
    [InlineData(1000, 910, Trend.Stable)]  // under 10 %
    [InlineData(800, 1000, Trend.Down)]
    [InlineData(500, 0, Trend.Up)]
    [InlineData(0, 0, Trend.Stable)]
    [Trait("AC", "AC-67")]
    public void Load_and_sessions_change_when_they_differ_by_more_than_10_percent(double recent, double previous, Trend expected) =>
        Assert.Equal(expected, ProgressCalculator.TrendOfAmount(recent, previous));

    [Theory]
    [InlineData(7.0, 6.6, Trend.Up)]
    [InlineData(7.0, 6.7, Trend.Stable)]   // exactly 0.3 is still stable
    [InlineData(2.0, 2.5, Trend.Down)]
    [InlineData(null, 6.0, Trend.Stable)]  // nothing to compare
    [Trait("AC", "AC-67")]
    public void Wellness_changes_when_averages_differ_by_more_than_0_3(double? recent, double? previous, Trend expected) =>
        Assert.Equal(expected, ProgressCalculator.TrendOfScore(recent, previous));

    [Fact]
    [Trait("AC", "AC-69")]
    public void Lower_is_better_for_sprints_and_a_met_goal_is_reached()
    {
        var sprint = BenchmarkCalculator.Build(
            DeviceTests.Get("SPRINT_30M"),
            results: [new(Wednesday.AddDays(-40), 4.50m), new(Wednesday.AddDays(-20), 4.40m), new(Wednesday.AddDays(-5), 4.30m)],
            periodStart: Wednesday.AddDays(-30),
            squadAverage: 4.20m,
            goal: new GoalDto("SPRINT_30M", 4.35m, Wednesday.AddDays(-1), null, "lena"),
            today: Wednesday);

        Assert.False(sprint.HigherIsBetter);
        Assert.Equal(4.30m, sprint.Best.Value);
        Assert.Equal((4.40m, -0.10m), (sprint.FirstInPeriod!.Value, sprint.ChangeInPeriod));
        Assert.Equal(GoalStatus.Reached, sprint.Status);
        Assert.Equal(-0.05m, sprint.GapToGoal);                               // negative: past the target
        Assert.Equal([4.40m, 4.30m], sprint.History.Select(h => h.Value));     // results in the period
    }

    [Theory]
    [InlineData(10, GoalStatus.Open)]
    [InlineData(-1, GoalStatus.Behind)]
    [Trait("AC", "AC-69")]
    public void An_unmet_goal_is_open_until_its_date_then_behind(int daysToGoal, GoalStatus expected)
    {
        var jump = BenchmarkCalculator.Build(
            DeviceTests.Get("CMJ"),
            results: [new(Wednesday.AddDays(-10), 33.0m), new(Wednesday.AddDays(-30), 34.0m)],
            periodStart: Wednesday.AddDays(-60),
            squadAverage: null,
            goal: new GoalDto("CMJ", 36.0m, Wednesday.AddDays(daysToGoal), null, "lena"),
            today: Wednesday);

        Assert.True(jump.HigherIsBetter);
        Assert.Equal((33.0m, 34.0m), (jump.Latest.Value, jump.Best.Value));
        Assert.Equal(3.0m, jump.GapToGoal);
        Assert.Equal(expected, jump.Status);
    }
}
