using AthleteDay.Contracts;
using AthleteDay.Core;

namespace AthleteDay.UnitTests;

public sealed class VersusTests
{
    private static readonly DateOnly To = new(2026, 10, 11);
    private static readonly DateOnly From = To.AddDays(-27);

    [Theory]
    [Trait("AC", "AC-94")]
    [InlineData(7.5, 7.0, MetricDirection.HigherIsBetter, VersusWinner.A)]
    [InlineData(2.0, 3.0, MetricDirection.LowerIsBetter, VersusWinner.A)]
    [InlineData(4.1, 4.0, MetricDirection.LowerIsBetter, VersusWinner.B)]
    [InlineData(3.0, 3.0, MetricDirection.HigherIsBetter, VersusWinner.Even)]
    [InlineData(900.0, 400.0, MetricDirection.NotRanked, VersusWinner.NotRanked)]
    [InlineData(null, 7.0, MetricDirection.HigherIsBetter, VersusWinner.NotRanked)]
    [InlineData(7.0, null, MetricDirection.LowerIsBetter, VersusWinner.NotRanked)]
    public void Each_metric_is_ranked_by_its_direction(double? a, double? b, MetricDirection direction, VersusWinner expected) =>
        Assert.Equal(expected, VersusCalculator.Rank((decimal?)a, (decimal?)b, direction));

    [Fact]
    [Trait("AC", "AC-93")]
    [Trait("AC", "AC-94")]
    public void The_comparison_has_load_wellness_and_the_best_test_results_of_the_period()
    {
        var a = new VersusData(
            Loads: [new(To, 400), new(To.AddDays(-1), 300), new(From.AddDays(-1), 999)], // the last one is before the period
            Wellness: [new(To, 8.0m, 2, 4), new(To.AddDays(-1), 7.0m, 3, 4)],
            Tests: [new("CMJ", To.AddDays(-3), 40.0m), new("CMJ", To.AddDays(-10), 42.5m), new("SPRINT_30M", To.AddDays(-2), 4.20m)]);
        var b = new VersusData(
            Loads: [new(To, 700)],
            Wellness: [new(To, 6.5m, 4, 3)],
            Tests: [new("CMJ", To.AddDays(-1), 41.0m), new("CMJ", From.AddDays(-5), 50.0m), new("SPRINT_30M", To.AddDays(-1), 4.05m)]);

        var metrics = VersusCalculator.Compare(a, b, From, To).ToDictionary(m => m.Key);

        Assert.Equal(
            ["totalLoad", "sessions", "averageLoad", "ratio", "sleep", "soreness", "mood", "checkIns", "CMJ", "SPRINT_30M"],
            VersusCalculator.Compare(a, b, From, To).Select(m => m.Key));
        Assert.Equal((700m, 700m, VersusWinner.NotRanked), (metrics["totalLoad"].A, metrics["totalLoad"].B, metrics["totalLoad"].Winner));
        Assert.Equal((2m, 1m), (metrics["sessions"].A, metrics["sessions"].B));
        Assert.Equal(25.0m, metrics["averageLoad"].A); // 700 AU over 28 days
        Assert.Equal(VersusWinner.NotRanked, metrics["ratio"].Winner);
        Assert.Equal((7.5m, 6.5m, VersusWinner.A), (metrics["sleep"].A, metrics["sleep"].B, metrics["sleep"].Winner));
        Assert.Equal((2.5m, 4.0m, VersusWinner.A), (metrics["soreness"].A, metrics["soreness"].B, metrics["soreness"].Winner));
        Assert.Equal(VersusWinner.A, metrics["mood"].Winner);
        Assert.Equal((2m, 1m, VersusWinner.A), (metrics["checkIns"].A, metrics["checkIns"].B, metrics["checkIns"].Winner));
        // Best in the period: A's 42.5 beats B's 41.0; B's 50.0 is from before the period.
        Assert.Equal((42.5m, 41.0m, VersusWinner.A), (metrics["CMJ"].A, metrics["CMJ"].B, metrics["CMJ"].Winner));
        Assert.Equal("cm", metrics["CMJ"].Unit);
        Assert.Equal(VersusWinner.B, metrics["SPRINT_30M"].Winner); // lower is better
    }

    [Fact]
    [Trait("AC", "AC-93")]
    public void Missing_wellness_is_not_ranked_and_the_ratio_uses_the_end_of_the_period()
    {
        var loads = Enumerable.Range(0, 28).Select(i => new DailyLoad(To.AddDays(-i), i < 7 ? 600 : 300)).ToList();
        var a = new VersusData(loads, [], []);
        var b = new VersusData([], [new(To, 7.0m, 2, 4)], []);

        var metrics = VersusCalculator.Compare(a, b, From, To).ToDictionary(m => m.Key);

        Assert.Equal(1.6m, metrics["ratio"].A); // 600 / ((7 × 600 + 21 × 300) / 28) = 600 / 375
        Assert.Null(metrics["ratio"].B);
        Assert.Null(metrics["sleep"].A);
        Assert.Equal(VersusWinner.NotRanked, metrics["sleep"].Winner);
        Assert.Equal((0m, 1m, VersusWinner.B), (metrics["checkIns"].A, metrics["checkIns"].B, metrics["checkIns"].Winner));
    }
}

public sealed class InsightTests
{
    private static readonly DateOnly Today = new(2026, 10, 11);

    private static InsightInput Input(
        IReadOnlyList<DailyLoad>? loads = null,
        IReadOnlyList<WellnessSummaryDto>? wellness = null,
        IReadOnlyList<DeviceResult>? tests = null,
        IReadOnlyList<GoalDto>? goals = null,
        PlanWeekLoad? lastWeek = null) =>
        new(Today, loads ?? [], wellness ?? DailyChecks(28, sleep: 8.0m, soreness: 2), tests ?? [], goals ?? [], lastWeek);

    /// <summary>One check a day for the last <paramref name="days"/> days.</summary>
    private static List<WellnessSummaryDto> DailyChecks(int days, decimal sleep, int soreness, int from = 0) =>
        Enumerable.Range(from, days).Select(i => new WellnessSummaryDto(Today.AddDays(-i), sleep, soreness, 4)).ToList();

    private static List<DailyLoad> Loads(int recentPerDay, int earlierPerDay) =>
        Enumerable.Range(0, 28).Select(i => new DailyLoad(Today.AddDays(-i), i < 7 ? recentPerDay : earlierPerDay)).ToList();

    [Fact]
    [Trait("AC", "AC-97")]
    public void A_steady_athlete_has_no_insights() =>
        Assert.Empty(InsightRules.Evaluate(Input(loads: Loads(400, 400))));

    [Fact]
    [Trait("AC", "AC-97")]
    public void A_load_spike_and_a_load_drop_follow_the_ratio()
    {
        var spike = Assert.Single(InsightRules.Evaluate(Input(loads: Loads(600, 300))));
        Assert.Equal((InsightKind.LoadSpike, InsightTone.Attention), (spike.Kind, spike.Tone));
        Assert.Equal(1.6m, spike.Figures["ratio"]);
        Assert.Equal(600m, spike.Figures["acute"]);
        Assert.Equal(375m, spike.Figures["chronic"]);

        var drop = Assert.Single(InsightRules.Evaluate(Input(loads: Loads(100, 400))));
        Assert.Equal((InsightKind.LoadDrop, InsightTone.Info), (drop.Kind, drop.Tone));

        Assert.Empty(InsightRules.Evaluate(Input(loads: Loads(500, 400)))); // 500 / 425 = 1.18
    }

    [Fact]
    [Trait("AC", "AC-97")]
    public void Falling_sleep_and_rising_soreness_compare_7_days_with_the_21_before()
    {
        var wellness = DailyChecks(7, sleep: 6.5m, soreness: 4).Concat(DailyChecks(21, sleep: 7.5m, soreness: 2, from: 7)).ToList();

        var insights = InsightRules.Evaluate(Input(loads: Loads(400, 400), wellness: wellness));

        var sleep = Assert.Single(insights, i => i.Kind == InsightKind.SleepFalling);
        Assert.Equal((6.5m, 7.5m), (sleep.Figures["recent"], sleep.Figures["before"]));
        var soreness = Assert.Single(insights, i => i.Kind == InsightKind.SorenessRising);
        Assert.Equal((4.0m, 2.0m), (soreness.Figures["recent"], soreness.Figures["before"]));
        Assert.All(insights, i => Assert.Equal(InsightTone.Attention, i.Tone));
    }

    [Fact]
    [Trait("AC", "AC-97")]
    public void A_small_change_or_too_few_checks_is_not_an_insight()
    {
        var small = DailyChecks(7, sleep: 7.1m, soreness: 2).Concat(DailyChecks(21, sleep: 7.5m, soreness: 2, from: 7)).ToList();
        Assert.Empty(InsightRules.Evaluate(Input(loads: Loads(400, 400), wellness: small)));

        // Only two checks before: no comparison, but too few recent checks is its own insight.
        var few = DailyChecks(2, sleep: 5.0m, soreness: 5).Concat(DailyChecks(2, sleep: 8.0m, soreness: 1, from: 10)).ToList();
        var insight = Assert.Single(InsightRules.Evaluate(Input(loads: Loads(400, 400), wellness: few)));
        Assert.Equal(InsightKind.MissingCheckIns, insight.Kind);
        Assert.Equal(2m, insight.Figures["checkIns"]);
    }

    [Fact]
    [Trait("AC", "AC-97")]
    public void A_recent_result_better_than_all_earlier_ones_is_a_personal_best()
    {
        var tests = new List<DeviceResult>
        {
            new("CMJ", Today.AddDays(-60), 40.0m), new("CMJ", Today.AddDays(-30), 41.5m), new("CMJ", Today.AddDays(-3), 43.0m),
            new("SPRINT_30M", Today.AddDays(-50), 4.30m), new("SPRINT_30M", Today.AddDays(-20), 4.20m), new("SPRINT_30M", Today.AddDays(-2), 4.10m),
            new("SJ", Today.AddDays(-40), 35.0m), new("SJ", Today.AddDays(-1), 36.0m), // only one earlier result
            new("GPS_HSR", Today.AddDays(-40), 800m), new("GPS_HSR", Today.AddDays(-30), 900m), new("GPS_HSR", Today.AddDays(-20), 950m), // not recent
        };

        var bests = InsightRules.Evaluate(Input(loads: Loads(400, 400), tests: tests));

        Assert.Equal(["CMJ", "SPRINT_30M"], bests.Select(b => b.TestType));
        Assert.All(bests, b => Assert.Equal((InsightKind.PersonalBest, InsightTone.Positive), (b.Kind, b.Tone)));
        Assert.Equal((43.0m, 41.5m), (bests[0].Figures["value"], bests[0].Figures["previous"]));
        Assert.Equal((4.10m, 4.20m), (bests[1].Figures["value"], bests[1].Figures["previous"]));
    }

    [Fact]
    [Trait("AC", "AC-97")]
    public void An_open_goal_due_within_21_days_is_at_risk()
    {
        var tests = new List<DeviceResult> { new("CMJ", Today.AddDays(-5), 41.0m), new("SJ", Today.AddDays(-5), 38.0m) };
        var goals = new List<GoalDto>
        {
            new("CMJ", 45.0m, Today.AddDays(10), null, "lena"),           // at risk
            new("SJ", 37.0m, Today.AddDays(10), null, "lena"),            // already met
            new("SPRINT_30M", 4.0m, Today.AddDays(30), null, "lena"),     // not due yet
            new("GPS_HSR", 1000m, Today.AddDays(5), null, "lena"),        // due, no result yet
        };

        var risks = InsightRules.Evaluate(Input(loads: Loads(400, 400), tests: tests, goals: goals));

        Assert.Equal(["CMJ", "GPS_HSR"], risks.Select(r => r.TestType));
        Assert.All(risks, r => Assert.Equal((InsightKind.GoalAtRisk, InsightTone.Attention), (r.Kind, r.Tone)));
        Assert.Equal((45.0m, 41.0m, 10m), (risks[0].Figures["target"], risks[0].Figures["latest"], risks[0].Figures["daysLeft"]));
        Assert.False(risks[1].Figures.ContainsKey("latest"));
    }

    [Theory]
    [Trait("AC", "AC-97")]
    [InlineData(2000, 2600, true, 30)]
    [InlineData(2000, 1400, true, -30)]
    [InlineData(2000, 2400, false, 0)]
    [InlineData(0, 900, false, 0)]
    public void The_last_complete_week_is_checked_against_the_plan(int planned, int logged, bool expected, int difference)
    {
        var insights = InsightRules.Evaluate(Input(loads: Loads(400, 400), lastWeek: new PlanWeekLoad(planned, logged)));

        if (!expected)
        {
            Assert.Empty(insights);
            return;
        }
        var offTrack = Assert.Single(insights);
        Assert.Equal((InsightKind.PlanOffTrack, InsightTone.Info), (offTrack.Kind, offTrack.Tone));
        Assert.Equal((planned, logged, difference), ((int)offTrack.Figures["planned"], (int)offTrack.Figures["logged"], (int)offTrack.Figures["difference"]));
    }

    [Fact]
    [Trait("AC", "AC-97")]
    [Trait("AC", "AC-98")]
    public void Attention_comes_first_then_positive_then_info()
    {
        var tests = new List<DeviceResult> { new("CMJ", Today.AddDays(-60), 40m), new("CMJ", Today.AddDays(-30), 41m), new("CMJ", Today.AddDays(-1), 42m) };

        var kinds = InsightRules.Evaluate(Input(loads: Loads(600, 300), wellness: DailyChecks(2, 8.0m, 2), tests: tests,
            lastWeek: new PlanWeekLoad(1000, 2000))).Select(i => i.Kind);

        Assert.Equal([InsightKind.LoadSpike, InsightKind.PersonalBest, InsightKind.MissingCheckIns, InsightKind.PlanOffTrack], kinds);
    }
}
