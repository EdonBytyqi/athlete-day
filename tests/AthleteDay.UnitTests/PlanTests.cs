using System.ComponentModel.DataAnnotations;
using AthleteDay.Contracts;
using AthleteDay.Core;

namespace AthleteDay.UnitTests;

public class PlanTests
{
    private static readonly DateOnly Monday = new(2026, 10, 12);

    private static readonly Dictionary<int, IReadOnlyList<PlannedSessionDto>> Template = new()
    {
        [1] = [PlanCalculator.Planned(SessionType.Strength, 60, 6, null)],
        [3] = [PlanCalculator.Planned(SessionType.Speed, 45, 7, "Short sprints"), PlanCalculator.Planned(SessionType.Recovery, 30, 2, null)],
        [5] = [PlanCalculator.Planned(SessionType.Endurance, 90, 5, null)],
    };

    [Fact]
    [Trait("AC", "AC-79")]
    public void Days_follow_the_standard_week_unless_the_date_was_changed()
    {
        var changes = new Dictionary<DateOnly, DayChange>
        {
            [Monday.AddDays(2)] = new("Match moved", [PlanCalculator.Planned(SessionType.Technique, 40, 4, null)]),
            [Monday.AddDays(4)] = new("Travel", []),
        };

        var week = PlanCalculator.BuildWeek(7, Monday.AddDays(2), Template, changes, logged: []);

        Assert.Equal(Monday, week.WeekStart);
        Assert.Equal(Enumerable.Range(1, 7), week.Days.Select(d => d.DayOfWeek));
        Assert.Equal((PlanDaySource.Standard, 360), (week.Days[0].Source, week.Days[0].PlannedLoad));
        Assert.Equal(PlanDaySource.Standard, week.Days[1].Source);
        Assert.Empty(week.Days[1].Sessions);

        var wednesday = week.Days[2];
        Assert.Equal((PlanDaySource.Changed, "Match moved", 160), (wednesday.Source, wednesday.Reason, wednesday.PlannedLoad));
        Assert.Equal([SessionType.Technique], wednesday.Sessions.Select(s => s.Type));

        var friday = week.Days[4];
        Assert.Equal((PlanDaySource.Changed, "Travel", 0), (friday.Source, friday.Reason, friday.PlannedLoad));
        Assert.Empty(friday.Sessions);
    }

    [Fact]
    [Trait("AC", "AC-81")]
    public void Planned_and_logged_load_are_compared_per_day_and_for_the_week()
    {
        var week = PlanCalculator.BuildWeek(7, Monday, Template, new Dictionary<DateOnly, DayChange>(),
            logged: [new(Monday, 300), new(Monday, 100), new(Monday.AddDays(2), 250), new(Monday.AddDays(7), 999)]);

        Assert.Equal((360, 400, 2), (week.Days[0].PlannedLoad, week.Days[0].LoggedLoad, week.Days[0].LoggedSessions));
        Assert.Equal((375, 250), (week.Days[2].PlannedLoad, week.Days[2].LoggedLoad)); // 315 + 60
        Assert.Equal((360 + 375 + 450, 650), (week.PlannedLoad, week.LoggedLoad));  // next Monday is outside the week
    }

    [Theory]
    [InlineData(2, 5, null, "DurationMinutes")]
    [InlineData(60, 11, null, "Rpe")]
    [InlineData(60, 5, 201, "Note")]
    [Trait("AC", "AC-78")]
    public void Planned_sessions_are_validated(int minutes, int rpe, int? noteLength, string field)
    {
        var session = new PlannedSessionRequest
        {
            Type = SessionType.Strength, DurationMinutes = minutes, Rpe = rpe, Note = noteLength is { } n ? new string('x', n) : null
        };

        Assert.Contains(Validate(session), e => e.MemberNames.Contains(field));
    }

    [Fact]
    [Trait("AC", "AC-78")]
    public void A_day_has_at_most_three_sessions()
    {
        var day = new PlanDayChangeRequest { Sessions = [.. Enumerable.Range(0, 4).Select(_ => new PlannedSessionRequest())] };

        Assert.Contains(Validate(day), e => e.MemberNames.Contains("Sessions"));
    }

    private static List<ValidationResult> Validate(object request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }
}
