namespace AthleteDay.Api.Infrastructure;

/// <summary>"Today" means today in Cham, regardless of where the server runs.</summary>
public sealed class AppClock(TimeProvider time)
{
    private static readonly TimeZoneInfo Zurich = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");

    public DateTimeOffset Now => time.GetUtcNow();

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), Zurich).DateTime);
}
