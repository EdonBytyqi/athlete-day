namespace AthleteDay.Core;

/// <summary>A device test: unit, plausible range, and whether a higher result is better (spec section 4).</summary>
public sealed record DeviceTest(string Type, string Unit, decimal Min, decimal Max, bool HigherIsBetter)
{
    public bool IsPlausible(decimal value) => value >= Min && value <= Max;

    /// <summary>True when <paramref name="value"/> is at least as good as <paramref name="target"/>.</summary>
    public bool Meets(decimal value, decimal target) => HigherIsBetter ? value >= target : value <= target;
}

/// <summary>The device tests the app understands, shared by the CSV import, benchmarks and goals.</summary>
public static class DeviceTests
{
    public static readonly IReadOnlyDictionary<string, DeviceTest> All = new Dictionary<string, DeviceTest>(StringComparer.OrdinalIgnoreCase)
    {
        ["CMJ"] = new("CMJ", "cm", 10, 90, HigherIsBetter: true),                   // counter-movement jump height
        ["SJ"] = new("SJ", "cm", 10, 90, HigherIsBetter: true),                     // squat jump height
        ["SPRINT_30M"] = new("SPRINT_30M", "s", 3.0m, 8.0m, HigherIsBetter: false),
        ["GPS_DISTANCE"] = new("GPS_DISTANCE", "m", 0, 30_000, HigherIsBetter: true),
        ["GPS_HSR"] = new("GPS_HSR", "m", 0, 5_000, HigherIsBetter: true)           // high-speed running distance
    };

    public static DeviceTest Get(string type) => All[type];

    public static bool TryGet(string type, out DeviceTest test) => All.TryGetValue(type, out test!);
}
