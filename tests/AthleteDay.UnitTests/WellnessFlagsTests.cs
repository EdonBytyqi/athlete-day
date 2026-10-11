using AthleteDay.Contracts;
using AthleteDay.Core;

namespace AthleteDay.UnitTests;

public class WellnessFlagsTests
{
    private static readonly WellnessSummaryDto Good = new(new DateOnly(2026, 10, 10), SleepHours: 8, Soreness: 2, Mood: 4);

    [Fact]
    [Trait("AC", "AC-44")]
    public void A_day_within_all_thresholds_has_no_flags()
    {
        Assert.Empty(ReadinessEvaluator.WellnessFlags(Good));
    }

    [Theory]
    [Trait("AC", "AC-44")]
    [InlineData(5.5, 2, 4, "Sleep under 6 h (5.5 h)")]
    [InlineData(8.0, 4, 4, "High soreness (4/5)")]
    [InlineData(8.0, 2, 2, "Low mood (2/5)")]
    public void Each_threshold_produces_its_flag(decimal sleep, int soreness, int mood, string flag)
    {
        var flags = ReadinessEvaluator.WellnessFlags(Good with { SleepHours = sleep, Soreness = soreness, Mood = mood });

        Assert.Equal([flag], flags);
    }

    [Fact]
    [Trait("AC", "AC-44")]
    public void Flags_use_the_same_wording_as_readiness_reasons()
    {
        var poor = Good with { SleepHours = 5, Soreness = 5, Mood = 1 };

        var flags = ReadinessEvaluator.WellnessFlags(poor);
        var reasons = ReadinessEvaluator.Evaluate(poor, ratio: 1.0, TrainingRestriction.None).Reasons;

        Assert.Equal(3, flags.Count);
        Assert.Equal(reasons, flags);
    }
}
