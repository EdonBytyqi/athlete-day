using AthleteDay.Contracts;
using static System.FormattableString;

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

    public static ReadinessDto Evaluate(WellnessSummaryDto? wellness, double? ratio, TrainingRestriction restriction)
    {
        var reasons = new List<string>();

        if (wellness is null)
        {
            reasons.Add("No wellness check today");
        }
        else
        {
            if (wellness.SleepHours < MinSleepHours)
                reasons.Add(Invariant($"Sleep under {MinSleepHours:0} h ({wellness.SleepHours:0.#} h)"));
            if (wellness.Soreness >= HighSoreness)
                reasons.Add(Invariant($"High soreness ({wellness.Soreness}/5)"));
            if (wellness.Mood <= LowMood)
                reasons.Add(Invariant($"Low mood ({wellness.Mood}/5)"));
        }

        if (ratio > HighRatio)
            reasons.Add(Invariant($"7-day load well above 28-day average (ratio {ratio:0.00})"));
        else if (ratio < LowRatio)
            reasons.Add(Invariant($"7-day load well below 28-day average (ratio {ratio:0.00})"));

        if (restriction != TrainingRestriction.None)
            reasons.Add(Invariant($"Physio restriction: {restriction}"));

        var status = reasons.Count switch
        {
            0 => ReadinessStatus.Ok,
            1 when wellness is null => ReadinessStatus.Missing,
            _ => ReadinessStatus.CheckIn
        };

        return new ReadinessDto(status, reasons);
    }
}
