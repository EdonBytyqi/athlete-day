using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AthleteDay.Api.Domain;
using AthleteDay.Api.Imports;
using AthleteDay.Contracts;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AthleteDay.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class ContractAndEventTests(ApiFactory api)
{
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich")).DateTime);

    [Fact]
    [Trait("AC", "AC-29")]
    public async Task Published_api_matches_the_committed_openapi_contract()
    {
        var contractPath = Path.Combine(RepoRoot(), "docs", "spec", "openapi.json");
        var actual = Normalise(await api.CreateClient().GetStringAsync("/openapi/v1.json"));

        if (Environment.GetEnvironmentVariable("UPDATE_OPENAPI_CONTRACT") == "1")
        {
            await File.WriteAllTextAsync(contractPath,
                actual.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
            return;
        }

        var expected = Normalise(await File.ReadAllTextAsync(contractPath));
        Assert.True(JsonNode.DeepEquals(expected, actual),
            "The API no longer matches docs/spec/openapi.json. Change the spec first; " +
            "after implementing, refresh it with UPDATE_OPENAPI_CONTRACT=1 and review the diff.");
    }

    [Fact]
    [Trait("AC", "AC-27")]
    public async Task Writes_push_a_change_signal_without_athlete_data()
    {
        var athlete = await api.CreateAthleteAsync();
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

        await api.ClientAs(Roles.Coach).PostAsJsonAsync("/api/sessions", new LogSessionRequest
        {
            AthleteId = athlete.Id, Date = Today, DurationMinutes = 45, Rpe = 6
        });

        var payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(["reason", "at"], payload.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.DoesNotContain(athlete.Code, payload.GetRawText());
    }

    [Fact]
    [Trait("AC", "AC-30")]
    public async Task An_import_event_delivered_twice_is_processed_once()
    {
        var athlete = await api.CreateAthleteAsync();
        var job = new ImportJob
        {
            Id = Guid.NewGuid(), FileName = "twice.csv", BlobName = $"tests/{Guid.NewGuid()}.csv",
            Status = ImportStatus.Queued, UploadedBy = "test", UploadedAt = DateTimeOffset.UtcNow
        };
        var csv = $"athlete_code,date,test_type,value,unit\n{athlete.Code},{Today.AddDays(-1):yyyy-MM-dd},CMJ,40,cm";

        await using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv)))
            await api.Services.GetRequiredService<IImportFileStore>().SaveAsync(job.BlobName, stream, CancellationToken.None);
        await api.WithDbAsync(async db => { db.ImportJobs.Add(job); await db.SaveChangesAsync(); });

        var evt = new ImportFileUploaded(job.Id, job.BlobName);
        for (var delivery = 0; delivery < 2; delivery++)
        {
            await using var scope = api.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ImportProcessor>().ProcessAsync(evt, CancellationToken.None);
        }

        await api.WithDbAsync(async db =>
        {
            Assert.Equal(1, await db.DeviceMeasurements.CountAsync(m => m.AthleteId == athlete.Id));
            Assert.Equal(ImportStatus.Completed, (await db.ImportJobs.SingleAsync(j => j.Id == job.Id)).Status);
        });
    }

    /// <summary>Removes parts that depend on where the API runs, so only the contract itself is compared.</summary>
    private static JsonNode Normalise(string json)
    {
        var node = JsonNode.Parse(json)!;
        node.AsObject().Remove("servers");
        return node;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AthleteDay.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root (AthleteDay.slnx) not found.");
    }
}
