using AthleteDay.Contracts;

namespace AthleteDay.Core;

/// <summary>The sessions that replace one date's standard sessions; none = a cancelled day (ADR-14).</summary>
public sealed record DayChange(string? Reason, IReadOnlyList<PlannedSessionDto> Sessions);

/// <summary>
/// Builds a training week from the standard week and the dated changes (AC-79), and compares planned with logged
/// load (AC-81). Weekdays are numbered 1 (Monday) to 7 (Sunday).
/// </summary>
public static class PlanCalculator
{
    public static PlannedSessionDto Planned(SessionType type, int minutes, int rpe, string? note) =>
        new(type, minutes, rpe, note, LoadCalculator.SessionLoad(rpe, minutes));

    public static int IsoDayOfWeek(DateOnly date) => date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;

    /// <param name="anyDay">Any day of the week to build; the week starts on its Monday.</param>
    /// <param name="template">Standard sessions by weekday (1–7); missing weekdays have none.</param>
    /// <param name="changes">Changed dates; each replaces that day's standard sessions completely.</param>
    /// <param name="logged">Logged sessions as (date, load); those outside the week are ignored.</param>
    public static WeekPlanDto BuildWeek(
        int athleteId,
        DateOnly anyDay,
        IReadOnlyDictionary<int, IReadOnlyList<PlannedSessionDto>> template,
        IReadOnlyDictionary<DateOnly, DayChange> changes,
        IEnumerable<DailyLoad> logged)
    {
        var monday = ProgressCalculator.WeekStart(anyDay);
        var loggedByDay = logged.ToLookup(l => l.Date);

        var days = Enumerable.Range(0, 7).Select(offset =>
        {
            var date = monday.AddDays(offset);
            var weekday = offset + 1;
            var (source, reason, sessions) = changes.TryGetValue(date, out var change)
                ? (PlanDaySource.Changed, change.Reason, change.Sessions)
                : (PlanDaySource.Standard, (string?)null, template.GetValueOrDefault(weekday) ?? []);
            var done = loggedByDay[date].ToList();
            return new PlanDayDto(date, weekday, source, reason, sessions,
                sessions.Sum(s => s.PlannedLoad), done.Sum(l => l.Load), done.Count);
        }).ToList();

        return new WeekPlanDto(athleteId, monday, days, days.Sum(d => d.PlannedLoad), days.Sum(d => d.LoggedLoad));
    }
}
