using AthleteDay.Api.Auth;
using AthleteDay.Api.Data;
using AthleteDay.Api.Endpoints;
using AthleteDay.Api.Imports;
using AthleteDay.Api.Infrastructure;
using AthleteDay.Api.Realtime;
using AthleteDay.Api.Reports;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var sqlConnection = builder.Configuration.GetConnectionString("Sql")
    ?? throw new InvalidOperationException("Connection string 'Sql' is missing.");

// ---- Data: EF Core for writes, Dapper for the dashboard report -------------------------------------------
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(sqlConnection, sql => sql.EnableRetryOnFailure()));
builder.Services.AddSingleton(new SqlConnectionFactory(sqlConnection));
builder.Services.AddScoped<DashboardReport>();
builder.Services.AddScoped<DemoDataSeeder>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<AppClock>();

// ---- Auth: demo personas + need-to-know policies per discipline ------------------------------------------
builder.Services.AddAuthentication(DemoAuthenticationHandler.SchemeName)
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, DemoAuthenticationHandler>(
        DemoAuthenticationHandler.SchemeName, null);
builder.Services.AddAuthorizationBuilder().AddAthleteDayPolicies();

// ---- Event-driven device import: Blob Storage + queue + background worker -------------------------------
var storage = builder.Configuration.GetSection(StorageOptions.Section).Get<StorageOptions>() ?? new StorageOptions();
if (string.IsNullOrWhiteSpace(storage.ConnectionString))
{
    builder.Services.AddSingleton<IImportFileStore, InMemoryImportFileStore>();
    builder.Services.AddSingleton<IImportEventQueue, InMemoryImportEventQueue>();
}
else
{
    builder.Services.AddSingleton<IImportFileStore>(new BlobImportFileStore(
        new BlobContainerClient(storage.ConnectionString, storage.Container)));
    builder.Services.AddSingleton<IImportEventQueue>(new StorageQueueImportEventQueue(
        new QueueClient(storage.ConnectionString, storage.Queue)));
}
builder.Services.AddScoped<ImportProcessor>();
if (builder.Configuration.GetValue("Import:RunWorker", true))
{
    builder.Services.AddHostedService<ImportWorker>();
}

// ---- Web plumbing ------------------------------------------------------------------------------------------
builder.Services.AddValidation();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IDashboardNotifier, DashboardNotifier>();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
// Server texts (validation, readiness reasons, import errors) follow Accept-Language: German for de, de-CH …,
// English otherwise (ADR-09). Numbers in these texts stay invariant, so only the UI culture matters.
app.UseRequestLocalization(o => o
    .SetDefaultCulture("en")
    .AddSupportedCultures("en", "de")
    .AddSupportedUICultures("en", "de"));
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapScalarApiReference(o => o.WithTitle("Athlete Day API"));
app.MapHealthChecks("/health");
app.MapHub<DashboardHub>("/hubs/dashboard");
app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();

app.MapGroup("/api")
    .RequireAuthorization()
    .MapAthleteEndpoints()
    .MapStaffInputEndpoints()
    .MapReportEndpoints()
    .MapImportEndpoints()
    .MapSportEndpoints();

await PrepareDatabaseAsync(app);

app.Run();

static async Task PrepareDatabaseAsync(WebApplication app)
{
    var config = app.Configuration;
    await using var scope = app.Services.CreateAsyncScope();

    if (config.GetValue("Database:MigrateOnStartup", true))
    {
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    var seeder = scope.ServiceProvider.GetRequiredService<DemoDataSeeder>();
    if (config.GetValue("Demo:ResetOnStartup", false))
        await seeder.ResetAsync();
    else if (config.GetValue("Demo:Seed", true))
        await seeder.SeedAsync();
}

public partial class Program;
