using AthleteDay.Api.Auth;
using AthleteDay.Api.Data;
using AthleteDay.Api.Domain;
using AthleteDay.Api.Infrastructure;
using AthleteDay.Contracts;

namespace AthleteDay.Api.Audit;

/// <summary>
/// Records who accessed which health-related resource. Only successful requests are logged,
/// so the log answers "who has seen this athlete's data?".
/// </summary>
public sealed class AuditFilter(string resource) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var result = await next(context);

        // Handlers that can answer in several ways return Results<…>; look at the result inside it.
        var outcome = result is INestedHttpResult nested ? nested.Result : result;

        var http = context.HttpContext;
        var status = outcome is IStatusCodeHttpResult { StatusCode: { } code } ? code : http.Response.StatusCode;
        if (status is < 200 or >= 300)
            return result;

        // The route names the athlete (…/athletes/{athleteId}); a create has no id in the route yet, so take it
        // from the response instead. A response about several athletes (a comparison) gets one entry per athlete.
        IReadOnlyList<int?> athleteIds =
            http.Request.RouteValues.TryGetValue("athleteId", out var raw) && int.TryParse(raw?.ToString(), out var id) ? [id]
            : outcome is IValueHttpResult { Value: IAthleteScoped scoped } ? [scoped.AthleteId]
            : outcome is IValueHttpResult { Value: IAthletesScoped several } ? several.AthleteIds.Select(i => (int?)i).ToList()
            : [null];

        var db = http.RequestServices.GetRequiredService<AppDbContext>();
        var clock = http.RequestServices.GetRequiredService<AppClock>();
        db.AuditEntries.AddRange(athleteIds.Select(athleteId => new AuditEntry
        {
            At = clock.Now,
            User = http.User.UserName(),
            Role = http.User.RoleName(),
            Action = http.Request.Method == HttpMethods.Get ? "Viewed" : "Changed",
            Resource = resource,
            AthleteId = athleteId
        }));
        await db.SaveChangesAsync(http.RequestAborted);

        return result;
    }
}

public static class AuditExtensions
{
    public static TBuilder Audited<TBuilder>(this TBuilder builder, string resource) where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(new AuditFilter(resource));
}
