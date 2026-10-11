using System.Net.Http.Headers;
using System.Net.Http.Json;
using AthleteDay.Api.Data;
using AthleteDay.Api.Domain;
using AthleteDay.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace AthleteDay.IntegrationTests;

/// <summary>
/// Starts the real API against a real SQL Server. In CI that is a throwaway container (Testcontainers);
/// on a machine without Docker, set ATHLETEDAY_TEST_SQL to a SQL Server connection string (e.g. LocalDB).
/// Storage runs in memory, so the full upload → event → worker → database path is exercised without Azure.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private MsSqlContainer? _container;
    private string _connectionString = "";

    public async Task InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable("ATHLETEDAY_TEST_SQL");
        if (!string.IsNullOrWhiteSpace(external))
        {
            _connectionString = new SqlConnectionStringBuilder(external)
            {
                InitialCatalog = $"AthleteDayTests_{Guid.NewGuid():N}"
            }.ConnectionString;
        }
        else
        {
            _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
            await _container.StartAsync();
            _connectionString = _container.GetConnectionString();
        }

        // Creating the client boots the host, which applies the EF Core migrations.
        _ = CreateClient();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Sql", _connectionString);
        builder.UseSetting("Storage:ConnectionString", "");
        builder.UseSetting("Demo:Seed", "false");
        builder.UseSetting("Demo:ResetOnStartup", "false");
    }

    public HttpClient ClientAs(string role, string? user = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(DemoHeaders.User, user ?? role.ToLowerInvariant());
        client.DefaultRequestHeaders.Add(DemoHeaders.Role, role);
        return client;
    }

    /// <summary>The id of a sport in the managed list, created on first use.</summary>
    public async Task<int> SportIdAsync(string name = "Testing")
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sport = await db.Sports.FirstOrDefaultAsync(s => s.Name == name);
        if (sport is null)
        {
            sport = new Sport { Name = name };
            db.Sports.Add(sport);
            await db.SaveChangesAsync();
        }
        return sport.Id;
    }

    /// <summary>Each test creates its own athlete so tests never depend on each other's data.</summary>
    public async Task<Athlete> CreateAthleteAsync(decimal bodyMassKg = 70m, string sport = "Testing")
    {
        var sportId = await SportIdAsync(sport);
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var athlete = new Athlete
        {
            Code = $"T-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            DisplayName = "Test athlete",
            SportId = sportId,
            BodyMassKg = bodyMassKg
        };
        db.Athletes.Add(athlete);
        await db.SaveChangesAsync();
        return athlete;
    }

    public async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
        else
        {
            await WithDbAsync(db => db.Database.EnsureDeletedAsync());
        }
        await base.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}

public static class HttpExtensions
{
    public static Task<HttpResponseMessage> PostCsvAsync(this HttpClient client, string fileName, string csv)
    {
        var content = new MultipartFormDataContent();
        var file = new StringContent(csv);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(file, "file", fileName);
        return client.PostAsync("/api/imports", content);
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>())!;
}
