using Microsoft.AspNetCore.SignalR;

namespace AthleteDay.Api.Realtime;

/// <summary>
/// Pushes "something changed" to open dashboards. The message carries no athlete data: each client
/// reloads through the API with its own role, so the push channel can never leak health data.
/// </summary>
public sealed class DashboardHub : Hub;

public interface IDashboardNotifier
{
    Task DataChangedAsync(string reason, CancellationToken ct = default);
}

public sealed class DashboardNotifier(IHubContext<DashboardHub> hub) : IDashboardNotifier
{
    public const string EventName = "dataChanged";

    public Task DataChangedAsync(string reason, CancellationToken ct = default) =>
        hub.Clients.All.SendAsync(EventName, new { reason, at = DateTimeOffset.UtcNow }, ct);
}
