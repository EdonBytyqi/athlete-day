using AthleteDay.Staff.Pages;
using AthleteDay.Staff.Services;

namespace AthleteDay.Staff.Tests;

/// <summary>The athlete's own week plan in the staff app (AC-84).</summary>
public sealed class MyWeekTests : StaffTestContext
{
    [Fact]
    [Trait("AC", "AC-84")]
    public void A_signed_in_athlete_sees_their_own_week_with_changed_days_marked()
    {
        Persona.SignInAsAthlete(Api.Squad[1]); // Elias, id 2
        var cut = Render<MyWeek>();

        cut.WaitForAssertion(() => Assert.Equal(7, cut.FindAll(".plan-day").Count));
        Assert.Contains(Api.Requests, r => r.Path == "/api/athletes/2/plan/week" && r.User == "ATH-002");
        var changed = cut.Find(".plan-day.changed");
        Assert.Contains("Match moved", changed.TextContent);
        Assert.Contains("Technique", changed.TextContent);
        Assert.Empty(cut.FindAll("button"));   // read-only
    }

    [Fact]
    [Trait("AC", "AC-84")]
    public void Without_a_chosen_athlete_the_page_asks_to_choose()
    {
        Persona.Switch(PersonaState.AthleteChooser);

        var cut = Render<MyWeek>();

        Assert.Contains("Choose your name", cut.Markup);
        Assert.DoesNotContain(Api.Requests, r => r.Path.EndsWith("/plan/week"));
    }
}
