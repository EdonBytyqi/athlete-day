using AthleteDay.Contracts;

namespace AthleteDay.Core;

/// <summary>Planned and logged load of one complete plan week.</summary>
public sealed record PlanWeekLoad(int Planned, int Logged);

/// <summary>
/// What the rules look at: sessions (one load each) of at least the last 28 days, wellness checks of at least the last
/// 28 days, all device results, the goals, and the last complete plan week (null when there is no plan).
/// </summary>
public sealed record InsightInput(
    DateOnly Today,
    IReadOnlyList<DailyLoad> Loads,
    IReadOnlyList<WellnessSummaryDto> Wellness,
    IReadOnlyList<DeviceResult> Tests,
    IReadOnlyList<GoalDto> Goals,
    PlanWeekLoad? LastWeek);

/// <summary>The insight rules of spec section 4 (AC-97, ADR-16). Each insight carries the figures that triggered it.</summary>
public static class InsightRules
{
    public const double SpikeRatio = 1.3;
    public const double DropRatio = 0.8;
    public const decimal SleepDrop = 0.5m;
    public const decimal SorenessRise = 0.5m;
    public const int MinChecksPerWindow = 3;
    public const int MinCheckInsPerWeek = 4;
    public const int PersonalBestDays = 14;
    public const int MinEarlierResults = 2;
    public const int GoalHorizonDays = 21;
    public const decimal PlanTolerance = 0.25m;

    public static IReadOnlyList<InsightDto> Evaluate(InsightInput input)
    {
        var insights = new List<InsightDto>();
        var load = LoadInsight(input);

        // Attention first, then positive, then info (spec section 4).
        if (load?.Kind == InsightKind.LoadSpike) insights.Add(load);
        insights.AddRange(WellnessInsights(input));
        insights.AddRange(GoalsAtRisk(input));
        insights.AddRange(PersonalBests(input));
        if (load?.Kind == InsightKind.LoadDrop) insights.Add(load);
        if (MissingCheckIns(input) is { } missing) insights.Add(missing);
        if (PlanOffTrack(input.LastWeek) is { } plan) insights.Add(plan);
        return insights;
    }

    private static InsightDto? LoadInsight(InsightInput input)
    {
        var byDay = input.Loads.GroupBy(l => l.Date).ToDictionary(g => g.Key, g => g.Sum(l => l.Load));
        var acute = Math.Round(LoadCalculator.RollingAverage(byDay, input.Today, LoadCalculator.AcuteDays), 1);
        var chronic = Math.Round(LoadCalculator.RollingAverage(byDay, input.Today, LoadCalculator.ChronicDays), 1);
        if (LoadCalculator.Ratio(acute, chronic) is not { } ratio) return null;

        var kind = ratio > SpikeRatio ? InsightKind.LoadSpike : ratio < DropRatio ? InsightKind.LoadDrop : (InsightKind?)null;
        return kind is null
            ? null
            : new InsightDto(kind.Value, kind == InsightKind.LoadSpike ? InsightTone.Attention : InsightTone.Info, null,
                Figures(("ratio", (decimal)ratio), ("acute", (decimal)acute), ("chronic", (decimal)chronic)));
    }

    private static IEnumerable<InsightDto> WellnessInsights(InsightInput input)
    {
        var recent = Window(input, 0, 7);
        var before = Window(input, 7, 21);
        if (recent.Count < MinChecksPerWindow || before.Count < MinChecksPerWindow) yield break;

        var (sleepNow, sleepBefore) = (Average(recent, c => c.SleepHours), Average(before, c => c.SleepHours));
        if (sleepNow <= sleepBefore - SleepDrop)
            yield return new InsightDto(InsightKind.SleepFalling, InsightTone.Attention, null, Figures(("recent", sleepNow), ("before", sleepBefore)));

        var (soreNow, soreBefore) = (Average(recent, c => c.Soreness), Average(before, c => c.Soreness));
        if (soreNow >= soreBefore + SorenessRise)
            yield return new InsightDto(InsightKind.SorenessRising, InsightTone.Attention, null, Figures(("recent", soreNow), ("before", soreBefore)));
    }

    private static IEnumerable<InsightDto> GoalsAtRisk(InsightInput input)
    {
        foreach (var test in DeviceTests.All.Values)
        {
            var goal = input.Goals.FirstOrDefault(g => Same(g.TestType, test.Type));
            if (goal?.TargetDate is not { } due || due < input.Today || due.DayNumber - input.Today.DayNumber > GoalHorizonDays) continue;

            var latest = input.Tests.Where(t => Same(t.TestType, test.Type)).MaxBy(t => t.Date);
            if (latest is not null && test.Meets(latest.Value, goal.Target)) continue;

            var figures = new Dictionary<string, decimal> { ["target"] = goal.Target, ["daysLeft"] = due.DayNumber - input.Today.DayNumber };
            if (latest is not null) figures["latest"] = latest.Value;
            yield return new InsightDto(InsightKind.GoalAtRisk, InsightTone.Attention, test.Type, figures);
        }
    }

    private static IEnumerable<InsightDto> PersonalBests(InsightInput input)
    {
        var windowStart = input.Today.AddDays(-(PersonalBestDays - 1));
        foreach (var test in DeviceTests.All.Values)
        {
            var results = input.Tests.Where(t => Same(t.TestType, test.Type)).ToList();
            var recent = results.Where(t => t.Date >= windowStart && t.Date <= input.Today).ToList();
            if (recent.Count == 0) continue;

            var best = test.HigherIsBetter ? recent.MaxBy(t => t.Value)! : recent.MinBy(t => t.Value)!;
            var earlier = results.Where(t => t.Date < best.Date).Select(t => t.Value).ToList();
            if (earlier.Count < MinEarlierResults) continue;

            var previous = test.HigherIsBetter ? earlier.Max() : earlier.Min();
            var better = test.HigherIsBetter ? best.Value > previous : best.Value < previous;
            if (better)
                yield return new InsightDto(InsightKind.PersonalBest, InsightTone.Positive, test.Type, Figures(("value", best.Value), ("previous", previous)));
        }
    }

    private static InsightDto? MissingCheckIns(InsightInput input)
    {
        var days = Window(input, 0, 7).Select(c => c.Date).Distinct().Count();
        return days < MinCheckInsPerWeek
            ? new InsightDto(InsightKind.MissingCheckIns, InsightTone.Info, null, Figures(("checkIns", days)))
            : null;
    }

    private static InsightDto? PlanOffTrack(PlanWeekLoad? week)
    {
        if (week is not { Planned: > 0 } w) return null;
        var difference = (decimal)(w.Logged - w.Planned) / w.Planned;
        return Math.Abs(difference) > PlanTolerance
            ? new InsightDto(InsightKind.PlanOffTrack, InsightTone.Info, null,
                Figures(("planned", w.Planned), ("logged", w.Logged), ("difference", Math.Round(difference * 100, MidpointRounding.AwayFromZero))))
            : null;
    }

    /// <summary>Checks from <paramref name="skipDays"/> days ago going back <paramref name="days"/> days.</summary>
    private static List<WellnessSummaryDto> Window(InsightInput input, int skipDays, int days)
    {
        var newest = input.Today.AddDays(-skipDays);
        var oldest = newest.AddDays(-(days - 1));
        return input.Wellness.Where(c => c.Date >= oldest && c.Date <= newest).ToList();
    }

    private static decimal Average(List<WellnessSummaryDto> checks, Func<WellnessSummaryDto, decimal> pick) =>
        Math.Round(checks.Average(pick), 1);

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, decimal> Figures(params (string Name, decimal Value)[] figures) =>
        figures.ToDictionary(f => f.Name, f => f.Value);
}
