namespace AthleteDay.Contracts;

/// <summary>
/// The disciplines that work on an athlete. Each one sees only what it needs.
/// </summary>
public static class Roles
{
    public const string Coach = "Coach";
    public const string Physio = "Physio";
    public const string Nutrition = "Nutrition";
    public const string Kitchen = "Kitchen";
    public const string Athlete = "Athlete";
    public const string Admin = "Admin";

    public static readonly IReadOnlyList<string> All = [Coach, Physio, Nutrition, Kitchen, Athlete, Admin];
}

/// <summary>Demo-only identity headers. Production would use Entra ID tokens instead.</summary>
public static class DemoHeaders
{
    public const string User = "X-Demo-User";
    public const string Role = "X-Demo-Role";
}
