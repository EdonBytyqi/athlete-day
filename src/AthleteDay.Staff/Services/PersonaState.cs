using AthleteDay.Contracts;

namespace AthleteDay.Staff.Services;

public sealed record Persona(string User, string Role, string Label);

/// <summary>The demo persona switcher. A real deployment signs people in with Entra ID instead.</summary>
public sealed class PersonaState
{
    public static readonly IReadOnlyList<Persona> All =
    [
        new("lena", Roles.Coach, "Lena · Athletic coach"),
        new("ATH-003", Roles.Athlete, "Lea (ATH-003) · Athlete"),
        new("mia", Roles.Nutrition, "Mia · Nutrition scientist"),
        new("jonas", Roles.Physio, "Jonas · Physio"),
        new("luca", Roles.Kitchen, "Luca · Kitchen")
    ];

    public Persona Current { get; private set; } = All[0];

    public event Action? Changed;

    public void Switch(string user)
    {
        Current = All.Single(p => p.User == user);
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
