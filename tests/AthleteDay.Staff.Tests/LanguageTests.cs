using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using AthleteDay.Staff.Components;
using AthleteDay.Staff.Pages;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;

namespace AthleteDay.Staff.Tests;

/// <summary>The staff app in English and German (AC-56).</summary>
public sealed class LanguageTests : StaffTestContext
{
    [Fact]
    [Trait("AC", "AC-56")]
    public void The_language_switch_reloads_the_app_in_the_chosen_language()
    {
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("sessions/new?athlete=2");
        var cut = Render<LanguageSwitch>();

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "DE").Click();

        Assert.EndsWith("sessions/new?athlete=2&lang=de", navigation.Uri);
        Assert.True(((BunitNavigationManager)navigation).History.First().Options.ForceLoad);
    }

    [Fact]
    [Trait("AC", "AC-56")]
    public void Labels_and_validation_messages_appear_in_German()
    {
        using var _ = UseCulture("de-CH");
        var cut = Render<LogSession>();
        cut.WaitForAssertion(() => Assert.Contains("ATH-007", cut.Markup)); // the athlete list has loaded

        Assert.Contains("Trainingseinheit erfassen", cut.Find("h1").TextContent);
        Assert.Contains("Dauer (Minuten)", cut.Markup);

        cut.Find("input[type=number]").Change("2");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Die Dauer muss zwischen 5 und 300 Minuten liegen.", cut.Markup);
            Assert.Contains("Bitte eine Athletin oder einen Athleten wählen.", cut.Markup);
        });
    }

    [Fact]
    [Trait("AC", "AC-56")]
    public void The_morning_check_greets_in_German()
    {
        using var _ = UseCulture("de-CH");
        Persona.SignInAsAthlete(Api.Squad[0]);

        var cut = Render<Wellness>();

        Assert.Contains("Guten Morgen, Nora", cut.Find("h1").TextContent);
        Assert.Contains("Muskelkater", cut.Markup);
    }

    private static IDisposable UseCulture(string name)
    {
        var (culture, uiCulture) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo(name);
        return new Restore(() => (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (culture, uiCulture));
    }

    private sealed class Restore(Action action) : IDisposable
    {
        public void Dispose() => action();
    }
}
