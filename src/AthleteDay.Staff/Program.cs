using System.Globalization;
using AthleteDay.Staff;
using AthleteDay.Staff.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Empty ApiBaseUrl = same origin (behind the nginx proxy in Docker); locally the API runs on :5080.
var apiBase = builder.Configuration["ApiBaseUrl"];
var baseAddress = new Uri(string.IsNullOrWhiteSpace(apiBase) ? builder.HostEnvironment.BaseAddress : apiBase);

builder.Services.AddLocalization();
builder.Services.AddSingleton<PersonaState>();
builder.Services.AddTransient<DemoHeadersHandler>();
builder.Services.AddHttpClient<StaffApi>(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<DemoHeadersHandler>();

var host = builder.Build();

// English or German (ADR-09): ?lang= wins and is remembered, otherwise the last choice (see index.html).
// Blazor loads culture data at start-up, so the language switch reloads the page.
var language = await host.Services.GetRequiredService<IJSRuntime>().InvokeAsync<string>("athleteDay.language");
var culture = CultureInfo.GetCultureInfo(language == "de" ? "de-CH" : "en-GB");
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = culture;

await host.RunAsync();
