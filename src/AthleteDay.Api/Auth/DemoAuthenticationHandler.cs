using System.Security.Claims;
using System.Text.Encodings.Web;
using AthleteDay.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AthleteDay.Api.Auth;

/// <summary>
/// Demo-only authentication: the persona switcher in the UI sends a user name and a role as headers.
/// It lets the demo switch roles in one click. In production this handler is replaced by
/// Entra ID (JWT bearer); the policies and endpoints stay exactly the same.
/// </summary>
public sealed class DemoAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Demo";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var user = Request.Headers[DemoHeaders.User].ToString();
        var role = Request.Headers[DemoHeaders.Role].ToString();

        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(role))
            return Task.FromResult(AuthenticateResult.NoResult());

        if (!Roles.All.Contains(role))
            return Task.FromResult(AuthenticateResult.Fail($"Unknown role '{role}'."));

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, user.Trim()), new Claim(ClaimTypes.Role, role)],
            SchemeName);

        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

public static class ClaimsPrincipalExtensions
{
    public static string UserName(this ClaimsPrincipal user) => user.Identity?.Name ?? "unknown";

    public static string RoleName(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Role) ?? "unknown";
}
