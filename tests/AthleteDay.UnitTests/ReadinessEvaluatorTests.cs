using AthleteDay.Contracts;
using AthleteDay.Core;

namespace AthleteDay.UnitTests;

public class ReadinessEvaluatorTests
{
    private static readonly WellnessSummaryDto Good = new(new DateOnly(2026, 10, 10), SleepHours: 8, Soreness: 2, Mood: 4);

    [Fact]
    public void Good_wellness_and_normal_ratio_is_ok()
    {
        var result = ReadinessEvaluator.Evaluate(Good, ratio: 1.0, TrainingRestriction.None);

        Assert.Equal(ReadinessStatus.Ok, result.Status);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void Missing_wellness_alone_is_reported_as_missing_not_as_a_concern()
    {
        var result = ReadinessEvaluator.Evaluate(null, ratio: 1.0, TrainingRestriction.None);

        Assert.Equal(ReadinessStatus.Missing, result.Status);
        Assert.Equal(["No wellness check today"], result.Reasons);
    }

    [Theory]
    [InlineData(5.5, 2, 4, "Sleep under 6 h (5.5 h)")]
    [InlineData(8.0, 4, 4, "High soreness (4/5)")]
    [InlineData(8.0, 2, 2, "Low mood (2/5)")]
    public void Each_wellness_threshold_raises_a_check_in(decimal sleep, int soreness, int mood, string reason)
    {
        var result = ReadinessEvaluator.Evaluate(Good with { SleepHours = sleep, Soreness = soreness, Mood = mood }, 1.0, TrainingRestriction.None);

        Assert.Equal(ReadinessStatus.CheckIn, result.Status);
        Assert.Contains(reason, result.Reasons);
    }

    [Theory]
    [InlineData(1.31, "above")]
    [InlineData(0.79, "below")]
    public void Ratio_outside_the_band_raises_a_check_in(double ratio, string direction)
    {
        var result = ReadinessEvaluator.Evaluate(Good, ratio, TrainingRestriction.None);

        Assert.Equal(ReadinessStatus.CheckIn, result.Status);
        Assert.Contains(result.Reasons, r => r.Contains(direction));
    }

    [Theory]
    [InlineData(1.3)]
    [InlineData(0.8)]
    [InlineData(null)]
    public void Ratio_at_the_band_edges_or_unknown_is_not_flagged(double? ratio)
    {
        Assert.Equal(ReadinessStatus.Ok, ReadinessEvaluator.Evaluate(Good, ratio, TrainingRestriction.None).Status);
    }

    [Fact]
    public void Physio_restriction_is_always_surfaced()
    {
        var result = ReadinessEvaluator.Evaluate(Good, 1.0, TrainingRestriction.Modified);

        Assert.Equal(ReadinessStatus.CheckIn, result.Status);
        Assert.Contains("Physio restriction: Modified", result.Reasons);
    }

    [Fact]
    public void Missing_wellness_plus_another_reason_is_a_check_in()
    {
        var result = ReadinessEvaluator.Evaluate(null, ratio: 1.6, TrainingRestriction.None);

        Assert.Equal(ReadinessStatus.CheckIn, result.Status);
        Assert.Equal(2, result.Reasons.Count);
    }
}
