using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AthleteDay.Contracts;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;

namespace AthleteDay.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class SquadTests(ApiFactory api) : IAsyncLifetime
{
    private int _rowing;
    private int _sculling;

    public async Task InitializeAsync()
    {
        _rowing = await api.SportIdAsync("Rowing");
        _sculling = await api.SportIdAsync("Sculling");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich")).DateTime);

    private AthleteRequest NewAthlete(string name = "Ada", int age = 24) => new()
    {
        DisplayName = name, SportId = _rowing, BirthYear = Today.Year - age, Sex = Sex.Female, BodyMassKg = 68.5m
    };

    private async Task<AthleteDetailsDto> AddAsync(AthleteRequest? request = null)
    {
        var response = await api.ClientAs(Roles.Coach).PostAsJsonAsync("/api/athletes", request ?? NewAthlete());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<AthleteDetailsDto>();
    }

    [Fact]
    [Trait("AC", "AC-32")]
    public async Task Coach_adds_an_athlete_and_the_system_assigns_the_next_free_code()
    {
        var first = await AddAsync(NewAthlete("Ada"));
        var second = await AddAsync(NewAthlete("Ben"));

        Assert.Matches(@"^ATH-\d{3}$", first.Code);
        Assert.Equal(int.Parse(first.Code[4..]) + 1, int.Parse(second.Code[4..]));
        Assert.Equal(("Ada", "Rowing", Today.Year - 24, Sex.Female, 68.5m),
            (first.DisplayName, first.Sport, first.BirthYear, first.Sex, first.BodyMassKg));

        var list = await api.ClientAs(Roles.Coach).GetFromJsonAsync<List<AthleteDto>>("/api/athletes");
        Assert.Contains(list!, a => a.Id == first.Id && a.Code == first.Code);
    }

    [Fact]
    [Trait("AC", "AC-32")]
    public async Task Athletes_added_at_the_same_time_get_different_codes()
    {
        var responses = await Task.WhenAll(Enumerable.Range(1, 8).Select(i =>
            api.ClientAs(Roles.Coach).PostAsJsonAsync("/api/athletes", NewAthlete($"Parallel {i}"))));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var codes = await Task.WhenAll(responses.Select(async r => (await r.ReadAsync<AthleteDetailsDto>()).Code));
        Assert.Equal(codes.Length, codes.Distinct().Count());
    }

    [Fact]
    [Trait("AC", "AC-33")]
    public async Task Invalid_athlete_returns_400_with_an_error_per_field()
    {
        var response = await api.ClientAs(Roles.Coach).PostAsJsonAsync("/api/athletes",
            new AthleteRequest { DisplayName = "", SportId = 0, BirthYear = Today.Year - 5, BodyMassKg = 10 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadAsync<ValidationProblemDetails>();
        Assert.Equivalent(new[] { "DisplayName", "SportId", "BirthYear", "BodyMassKg" }, problem.Errors.Keys.ToArray());
    }

    [Fact]
    [Trait("AC", "AC-34")]
    public async Task Coach_edits_an_athlete_and_the_code_stays_the_same()
    {
        var athlete = await AddAsync();
        var coach = api.ClientAs(Roles.Coach);

        var response = await coach.PutAsJsonAsync($"/api/athletes/{athlete.Id}", new AthleteRequest
        {
            DisplayName = "Ada L.", SportId = _sculling, BirthYear = Today.Year - 25, Sex = Sex.NotStated, BodyMassKg = 70m
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var edited = await coach.GetFromJsonAsync<AthleteDetailsDto>($"/api/athletes/{athlete.Id}");
        Assert.Equal((athlete.Code, "Ada L.", "Sculling", Today.Year - 25, Sex.NotStated, 70m),
            (edited!.Code, edited.DisplayName, edited.Sport, edited.BirthYear, edited.Sex, edited.BodyMassKg));
    }

    [Fact]
    [Trait("AC", "AC-34")]
    public async Task Editing_an_unknown_or_archived_athlete_returns_404()
    {
        var archived = await AddAsync();
        var coach = api.ClientAs(Roles.Coach);
        (await coach.DeleteAsync($"/api/athletes/{archived.Id}")).EnsureSuccessStatusCode();

        var unknown = await coach.PutAsJsonAsync("/api/athletes/999999", NewAthlete());
        var gone = await coach.PutAsJsonAsync($"/api/athletes/{archived.Id}", NewAthlete());

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    [Trait("AC", "AC-35")]
    public async Task Archiving_hides_the_athlete_keeps_its_history_and_can_be_undone()
    {
        var athlete = await AddAsync();
        var coach = api.ClientAs(Roles.Coach);
        (await coach.PostAsJsonAsync("/api/sessions", new LogSessionRequest
        {
            AthleteId = athlete.Id, Date = Today, DurationMinutes = 60, Rpe = 6
        })).EnsureSuccessStatusCode();
        (await api.ClientAs(Roles.Nutrition).PutAsJsonAsync("/api/nutrition-targets", new NutritionTargetRequest
        {
            AthleteId = athlete.Id, Date = Today, EnergyKcal = 3000, CarbsGPerKg = 6
        })).EnsureSuccessStatusCode();

        var archive = await coach.DeleteAsync($"/api/athletes/{athlete.Id}");

        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);
        Assert.DoesNotContain((await coach.GetFromJsonAsync<List<DashboardRowDto>>("/api/dashboard"))!, r => r.AthleteId == athlete.Id);
        Assert.DoesNotContain((await coach.GetFromJsonAsync<List<AthleteDto>>("/api/athletes"))!, a => a.Id == athlete.Id);
        Assert.DoesNotContain(athlete.Code, await api.ClientAs(Roles.Kitchen).GetStringAsync("/api/kitchen/today"));

        var lateSession = await coach.PostAsJsonAsync("/api/sessions", new LogSessionRequest
        {
            AthleteId = athlete.Id, Date = Today, DurationMinutes = 30, Rpe = 3
        });
        Assert.Equal(HttpStatusCode.BadRequest, lateSession.StatusCode);

        await api.WithDbAsync(async db =>
            Assert.Equal(1, await db.TrainingSessions.CountAsync(s => s.AthleteId == athlete.Id)));

        var archived = await coach.GetFromJsonAsync<List<AthleteDetailsDto>>("/api/athletes/archived");
        Assert.Contains(archived!, a => a.Id == athlete.Id && a.ArchivedAt is not null);

        var restore = await coach.PostAsync($"/api/athletes/{athlete.Id}/restore", null);

        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
        var row = (await coach.GetFromJsonAsync<List<DashboardRowDto>>("/api/dashboard"))!.Single(r => r.AthleteId == athlete.Id);
        Assert.Equal(360, row.TodayLoad);
    }

    [Theory]
    [InlineData(Roles.Physio)]
    [InlineData(Roles.Nutrition)]
    [InlineData(Roles.Kitchen)]
    [InlineData(Roles.Athlete)]
    [InlineData(Roles.Admin)]
    [Trait("AC", "AC-36")]
    public async Task Only_the_coach_manages_the_squad(string role)
    {
        var athlete = await AddAsync();
        var client = api.ClientAs(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/athletes", NewAthlete())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/athletes/{athlete.Id}", NewAthlete())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/api/athletes/{athlete.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/athletes/{athlete.Id}/restore", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/athletes/archived")).StatusCode);
    }

    [Fact]
    [Trait("AC", "AC-36")]
    public async Task Squad_changes_are_audited_and_push_a_change_signal()
    {
        var athlete = await AddAsync();
        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(api.Server.BaseAddress, "/hubs/dashboard"), o =>
            {
                o.HttpMessageHandlerFactory = _ => api.Server.CreateHandler();
                o.Transports = HttpTransportType.LongPolling;
            })
            .Build();
        hub.On<JsonElement>("dataChanged", payload => received.TrySetResult(payload));
        await hub.StartAsync();

        (await api.ClientAs(Roles.Coach, "lena").DeleteAsync($"/api/athletes/{athlete.Id}")).EnsureSuccessStatusCode();

        var payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(["reason", "at"], payload.EnumerateObject().Select(p => p.Name).ToArray());

        var audit = await api.ClientAs(Roles.Admin).GetFromJsonAsync<List<AuditEntryDto>>("/api/audit?take=500");
        Assert.Contains(audit!, a => a is { User: "lena", Role: Roles.Coach, Action: "Changed", Resource: "Athlete" }
                                     && a.AthleteId == athlete.Id);
    }

    [Fact]
    [Trait("AC", "AC-36")]
    public async Task Adding_an_athlete_is_audited_with_its_id()
    {
        var response = await api.ClientAs(Roles.Coach, "lena").PostAsJsonAsync("/api/athletes", NewAthlete());
        var athlete = await response.ReadAsync<AthleteDetailsDto>();

        var audit = await api.ClientAs(Roles.Admin).GetFromJsonAsync<List<AuditEntryDto>>("/api/audit?take=500");
        Assert.Contains(audit!, a => a is { User: "lena", Action: "Changed", Resource: "Athlete" } && a.AthleteId == athlete.Id);
    }

    [Fact]
    [Trait("AC", "AC-36")]
    public async Task Failed_changes_are_not_audited()
    {
        var response = await api.ClientAs(Roles.Coach, "lena").PutAsJsonAsync("/api/athletes/999998", NewAthlete());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var audit = await api.ClientAs(Roles.Admin).GetFromJsonAsync<List<AuditEntryDto>>("/api/audit?take=500");
        Assert.DoesNotContain(audit!, a => a.AthleteId == 999998);
    }

    [Fact]
    [Trait("AC", "AC-37")]
    public async Task Dashboard_rows_contain_age_and_sex()
    {
        var athlete = await AddAsync(NewAthlete(age: 24));

        var row = (await api.ClientAs(Roles.Physio).GetFromJsonAsync<List<DashboardRowDto>>("/api/dashboard"))!
            .Single(r => r.AthleteId == athlete.Id);

        Assert.Equal(24, row.Age);
        Assert.Equal(Sex.Female, row.Sex);
    }
}
