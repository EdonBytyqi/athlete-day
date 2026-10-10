using AthleteDay.Api.Auth;
using AthleteDay.Api.Data;
using AthleteDay.Api.Domain;
using AthleteDay.Api.Infrastructure;

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

        var http = context.HttpContext;
        var status = result is IStatusCodeHttpResult { StatusCode: { } code } ? code : http.Response.StatusCode;
        if (status is < 200 or >= 300)
            return result;

        int? athleteId = http.Request.RouteValues.TryGetValue("athleteId", out var raw) && int.TryParse(raw?.ToString(), out var id)
            ? id
            : null;

        var db = http.RequestServices.GetRequiredService<AppDbContext>();
        var clock = http.RequestServices.GetRequiredService<AppClock>();
        db.AuditEntries.Add(new AuditEntry
        {
            At = clock.Now,
            User = http.User.UserName(),
            Role = http.User.RoleName(),
            Action = http.Request.Method == HttpMethods.Get ? "Viewed" : "Changed",
            Resource = resource,
            AthleteId = athleteId
        });
        await db.SaveChangesAsync(http.RequestAborted);

        return result;
    }
}

public static class AuditExtensions
{
    public static TBuilder Audited<TBuilder>(this TBuilder builder, string resource) where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(new AuditFilter(resource));
}
