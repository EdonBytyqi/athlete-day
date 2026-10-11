using AthleteDay.Contracts;
using AthleteDay.Staff.Components;
using AthleteDay.Staff.Pages;
using AthleteDay.Staff.Services;

namespace AthleteDay.Staff.Tests;

public class AthleteSignInTests : StaffTestContext
{
    private static string[] Options(IRenderedComponent<AthleteSignIn> cut) =>
        cut.FindAll("select option").Select(o => o.TextContent.Trim()).ToArray();

    [Fact]
    [Trait("AC", "AC-50")]
    public void Choosing_the_athlete_role_lists_the_current_squad()
    {
        var cut = Render<AthleteSignIn>();
        Assert.Empty(cut.FindAll("select")); // the coach has nobody to choose

        cut.InvokeAsync(() => Persona.Switch(PersonaState.AthleteChooser));

        cut.WaitForAssertion(() =>
            Assert.Equal(["Choose your name…", "Nora (ATH-001)", "Elias (ATH-002)", "Ada (ATH-007)"], Options(cut)));
    }

    [Fact]
    [Trait("AC", "AC-50")]
    public async Task Picking_an_athlete_signs_in_with_their_code()
    {
        Persona.Switch(PersonaState.AthleteChooser);
        var cut = Render<AthleteSignIn>();
        cut.WaitForAssertion(() => Assert.Equal(4, Options(cut).Length));

        cut.Find("select").Change("ATH-007");

        Assert.Equal(("ATH-007", Roles.Athlete), (Persona.Current.User, Persona.Current.Role));
        Assert.Equal("Ada", Persona.SignedInAthlete?.DisplayName);

        await Services.GetService<StaffApi>()!.GetAthletesAsync();
        Assert.Equal(("ATH-007", Roles.Athlete), (Api.Requests[^1].User, Api.Requests[^1].Role));
    }

    [Fact]
    [Trait("AC", "AC-51")]
    public void Morning_check_greets_and_submits_for_the_signed_in_athlete()
    {
        Persona.SignInAsAthlete(Api.Squad[0]);
        var cut = Render<Wellness>();

        Assert.Contains("Good morning, Nora", cut.Find("h1").TextContent);
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains(Api.Requests, r =>
            r.Path == "/api/wellness" && r.User == "ATH-001" && r.Body!.Contains("\"athleteId\":1")));
    }

    [Fact]
    [Trait("AC", "AC-51")]
    public void Picking_another_athlete_switches_the_form()
    {
        Persona.SignInAsAthlete(Api.Squad[0]);
        var cut = Render<Wellness>();

        cut.InvokeAsync(() => Persona.SignInAsAthlete(Api.Squad[1]));
        cut.WaitForAssertion(() => Assert.Contains("Good morning, Elias", cut.Find("h1").TextContent));
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains(Api.Requests, r =>
            r.Path == "/api/wellness" && r.User == "ATH-002" && r.Body!.Contains("\"athleteId\":2")));
    }

    [Fact]
    [Trait("AC", "AC-51")]
    public void Without_a_chosen_athlete_the_form_asks_to_choose_and_cannot_be_sent()
    {
        Persona.Switch(PersonaState.AthleteChooser);
        var cut = Render<Wellness>();

        Assert.Contains("Choose your name", cut.Markup);
        Assert.True(cut.Find("button[type=submit]").HasAttribute("disabled"));
    }
}
