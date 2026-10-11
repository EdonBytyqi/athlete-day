using System.Net;
using System.Net.Http.Json;
using AthleteDay.Contracts;
using System.Globalization;
using AthleteDay.Staff.Resources;
using AthleteDay.Staff.Services;
using Microsoft.Extensions.Localization;
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

    /// <summary>A week with one standard and one changed day, for the athlete's own plan view.</summary>
    public static WeekPlanDto WeekPlan(int athleteId)
    {
        var monday = new DateOnly(2026, 10, 12);
        var days = Enumerable.Range(0, 7).Select(i => new PlanDayDto(monday.AddDays(i), i + 1, PlanDaySource.Standard, null, [], 0, 0, 0)).ToList();
        days[0] = days[0] with { Sessions = [new(SessionType.Strength, 60, 6, null, 360)], PlannedLoad = 360 };
        days[2] = days[2] with { Source = PlanDaySource.Changed, Reason = "Match moved", Sessions = [new(SessionType.Technique, 40, 4, null, 160)], PlannedLoad = 160 };
        return new WeekPlanDto(athleteId, monday, days, 520, 0);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        string? Header(string name) => request.Headers.TryGetValues(name, out var values) ? values.Single() : null;
        var path = request.RequestUri!.AbsolutePath;
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
        Requests.Add(new(request.Method, path, Header(DemoHeaders.User), Header(DemoHeaders.Role), body));

        if (path.StartsWith("/api/athletes/") && path.EndsWith("/plan/week"))
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(WeekPlan(int.Parse(path.Split('/')[3]))) };

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
        // English unless a test switches; independent of the machine's language.
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

        var http = new HttpClient(new DemoHeadersHandler(Persona) { InnerHandler = Api })
        {
            BaseAddress = new Uri("http://api.test/")
        };
        Services.AddLocalization();
        Services.AddSingleton(Persona);
        Services.AddSingleton(sp => new StaffApi(http, Persona, sp.GetRequiredService<IStringLocalizer<Strings>>()));
    }
}
