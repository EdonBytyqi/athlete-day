using AthleteDay.Contracts;
using System.Globalization;
using Text = AthleteDay.Core.Resources.CoreText;

namespace AthleteDay.Core;

/// <summary>
/// Turns today's wellness check, load ratio and physio restriction into flags for a conversation.
/// The flags start a check-in between coach and athlete. They are not a diagnosis.
/// </summary>
public static class ReadinessEvaluator
{
    public const decimal MinSleepHours = 6m;
    public const int HighSoreness = 4;
    public const int LowMood = 2;
    public const double HighRatio = 1.3;
    public const double LowRatio = 0.8;

    /// <summary>The wellness part of the readiness rules, for one day (spec AC-44).</summary>
    public static IReadOnlyList<string> WellnessFlags(WellnessSummaryDto wellness)
    {
        var flags = new List<string>();
        if (wellness.SleepHours < MinSleepHours)
            flags.Add(Format(Text.SleepUnder, MinSleepHours.ToString("0", CultureInfo.InvariantCulture), wellness.SleepHours.ToString("0.#", CultureInfo.InvariantCulture)));
        if (wellness.Soreness >= HighSoreness)
            flags.Add(Format(Text.HighSoreness, wellness.Soreness));
        if (wellness.Mood <= LowMood)
            flags.Add(Format(Text.LowMood, wellness.Mood));
        return flags;
    }

    public static ReadinessDto Evaluate(WellnessSummaryDto? wellness, double? ratio, TrainingRestriction restriction)
    {
        var reasons = new List<string>();

        if (wellness is null)
            reasons.Add(Text.NoWellnessCheck);
        else
            reasons.AddRange(WellnessFlags(wellness));

        if (ratio > HighRatio)
            reasons.Add(Format(Text.LoadWellAbove, ratio.Value.ToString("0.00", CultureInfo.InvariantCulture)));
        else if (ratio < LowRatio)
            reasons.Add(Format(Text.LoadWellBelow, ratio.Value.ToString("0.00", CultureInfo.InvariantCulture)));

        if (restriction != TrainingRestriction.None)
            reasons.Add(Format(Text.PhysioRestriction, RestrictionName(restriction)));

        var status = reasons.Count switch
        {
            0 => ReadinessStatus.Ok,
            1 when wellness is null => ReadinessStatus.Missing,
            _ => ReadinessStatus.CheckIn
        };

        return new ReadinessDto(status, reasons);
    }

    // Texts come from Resources/CoreText(.de).resx in the current UI culture; numbers stay invariant ("5.5 h").
    private static string Format(string template, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, template, args);

    private static string RestrictionName(TrainingRestriction restriction) => restriction switch
    {
        TrainingRestriction.Modified => Text.RestrictionModified,
        TrainingRestriction.NoTraining => Text.RestrictionNoTraining,
        _ => restriction.ToString()
    };
}
