using AthleteDay.Staff;
using AthleteDay.Staff.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Empty ApiBaseUrl = same origin (behind the nginx proxy in Docker); locally the API runs on :5080.
var apiBase = builder.Configuration["ApiBaseUrl"];
var baseAddress = new Uri(string.IsNullOrWhiteSpace(apiBase) ? builder.HostEnvironment.BaseAddress : apiBase);

builder.Services.AddSingleton<PersonaState>();
builder.Services.AddTransient<DemoHeadersHandler>();
builder.Services.AddHttpClient<StaffApi>(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<DemoHeadersHandler>();

await builder.Build().RunAsync();
