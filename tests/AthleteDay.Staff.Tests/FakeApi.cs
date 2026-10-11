using System.Net;
using System.Net.Http.Json;
using AthleteDay.Contracts;
using AthleteDay.Staff.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AthleteDay.Staff.Tests;

/// <summary>Stands in for the API: serves a small squad and records what the staff app sends, with its identity.</summary>
public sealed class FakeApi : HttpMessageHandler
{
    public sealed record SentRequest(HttpMethod Method, string Path, string? User, string? Role, string? Body);

    public List<AthleteDto> Squad { get; } =
    [
        new(1, "ATH-001", "Nora", "Alpine skiing"),
        new(2, "ATH-002", "Elias", "Ice hockey"),
        new(7, "ATH-007", "Ada", "Rowing")
    ];

    public List<SentRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        string? Header(string name) => request.Headers.TryGetValues(name, out var values) ? values.Single() : null;
        var path = request.RequestUri!.AbsolutePath;
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
        Requests.Add(new(request.Method, path, Header(DemoHeaders.User), Header(DemoHeaders.Role), body));

        return path switch
        {
            "/api/athletes" => new(HttpStatusCode.OK) { Content = JsonContent.Create(Squad) },
            "/api/wellness" => new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new WellnessSummaryDto(DateOnly.FromDateTime(DateTime.Today), 8, 2, 4))
            },
            _ => new(HttpStatusCode.NotFound)
        };
    }
}

/// <summary>The staff app's real services (persona, headers handler, API client) on top of <see cref="FakeApi"/>.</summary>
public abstract class StaffTestContext : BunitContext
{
    protected FakeApi Api { get; } = new();
    protected PersonaState Persona { get; } = new();

    protected StaffTestContext()
    {
        var http = new HttpClient(new DemoHeadersHandler(Persona) { InnerHandler = Api })
        {
            BaseAddress = new Uri("http://api.test/")
        };
        Services.AddSingleton(Persona);
        Services.AddSingleton(new StaffApi(http, Persona));
    }
}
