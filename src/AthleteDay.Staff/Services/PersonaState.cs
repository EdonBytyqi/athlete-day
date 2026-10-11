using AthleteDay.Contracts;

namespace AthleteDay.Staff.Services;

public sealed record Persona(string User, string Role, string Label);

/// <summary>The demo persona switcher. A real deployment signs people in with Entra ID instead.</summary>
public sealed class PersonaState
{
    /// <summary>
    /// The athlete entry of the switcher. Who the athlete is gets chosen in a second list
    /// (<see cref="SignInAsAthlete"/>); until then this placeholder is the identity.
    /// </summary>
    public const string AthleteChooser = "athlete";

    public static readonly IReadOnlyList<Persona> All =
    [
        new("lena", Roles.Coach, "Lena · Athletic coach"),
        new(AthleteChooser, Roles.Athlete, "Athlete"),
        new("mia", Roles.Nutrition, "Mia · Nutrition scientist"),
        new("jonas", Roles.Physio, "Jonas · Physio"),
        new("luca", Roles.Kitchen, "Luca · Kitchen")
    ];

    public Persona Current { get; private set; } = All[0];

    /// <summary>The athlete signed in through the second list; null for every other role.</summary>
    public AthleteDto? SignedInAthlete { get; private set; }

    public event Action? Changed;

    public void Switch(string user)
    {
        Current = All.Single(p => p.User == user);
        SignedInAthlete = null;
        Changed?.Invoke();
    }

    /// <summary>Signs in as one athlete: their code is the user name the API checks (AC-09).</summary>
    public void SignInAsAthlete(AthleteDto athlete)
    {
        Current = new Persona(athlete.Code, Roles.Athlete, $"{athlete.DisplayName} ({athlete.Code}) · Athlete");
        SignedInAthlete = athlete;
        Changed?.Invoke();
    }

    public bool Is(string role) => Current.Role == role;
}

/// <summary>Adds the demo identity headers to every API call.</summary>
public sealed class DemoHeadersHandler(PersonaState persona) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove(DemoHeaders.User);
        request.Headers.Remove(DemoHeaders.Role);
        request.Headers.Add(DemoHeaders.User, persona.Current.User);
        request.Headers.Add(DemoHeaders.Role, persona.Current.Role);
        return base.SendAsync(request, cancellationToken);
    }
}
