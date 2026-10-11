using AthleteDay.Contracts;
using AthleteDay.Core;
using Dapper;
using Microsoft.Data.SqlClient;

namespace AthleteDay.Api.Reports;

/// <summary>
/// Read side of the dashboard: hand-written T-SQL through Dapper, so one round trip returns every athlete
/// with load windows, wellness, nutrition and restriction as of the end date (@To), plus totals over the
/// chosen period (@From – @To, ADR-11). Writes go through EF Core.
/// </summary>
public sealed class DashboardReport(SqlConnectionFactory connections)
{
    private const string DashboardSql = """
        WITH daily AS (
            SELECT s.AthleteId, s.[Date], SUM(s.Rpe * s.DurationMinutes) AS DailyLoad
            FROM dbo.TrainingSessions AS s
            WHERE s.[Date] > DATEADD(day, -28, @To) AND s.[Date] <= @To
            GROUP BY s.AthleteId, s.[Date]
        ),
        windows AS (
            SELECT d.AthleteId,
                   SUM(CASE WHEN d.[Date] = @To THEN d.DailyLoad ELSE 0 END)                    AS TodayLoad,
                   SUM(CASE WHEN d.[Date] > DATEADD(day, -7, @To) THEN d.DailyLoad ELSE 0 END)  AS AcuteSum,
                   SUM(d.DailyLoad)                                                             AS ChronicSum
            FROM daily AS d
            GROUP BY d.AthleteId
        ),
        range_sessions AS (
            SELECT s.AthleteId, SUM(s.Rpe * s.DurationMinutes) AS RangeLoad, COUNT(*) AS RangeSessions
            FROM dbo.TrainingSessions AS s
            WHERE s.[Date] >= @From AND s.[Date] <= @To
            GROUP BY s.AthleteId
        ),
        range_wellness AS (
            SELECT wr.AthleteId, COUNT(*) AS WellnessDays,
                   AVG(CAST(wr.SleepHours AS decimal(6, 2))) AS AvgSleep,
                   AVG(CAST(wr.Soreness AS float))           AS AvgSoreness,
                   AVG(CAST(wr.Mood AS float))               AS AvgMood
            FROM dbo.WellnessChecks AS wr
            WHERE wr.[Date] >= @From AND wr.[Date] <= @To
            GROUP BY wr.AthleteId
        )
        SELECT a.Id AS AthleteId, a.Code, a.DisplayName, sp.Name AS Sport, a.BodyMassKg, a.BirthYear, a.Sex,
               COALESCE(w.TodayLoad, 0)  AS TodayLoad,
               COALESCE(w.AcuteSum, 0)   AS AcuteSum,
               COALESCE(w.ChronicSum, 0) AS ChronicSum,
               wc.[Date] AS WellnessDate, wc.SleepHours, wc.Soreness, wc.Mood,
               n.EnergyKcal, n.CarbsGPerKg,
               COALESCE(p.Restriction, 'None') AS Restriction,
               COALESCE(rs.RangeLoad, 0) AS RangeLoad, COALESCE(rs.RangeSessions, 0) AS RangeSessions,
               COALESCE(rw.WellnessDays, 0) AS WellnessDays, rw.AvgSleep, rw.AvgSoreness, rw.AvgMood
        FROM dbo.Athletes AS a
        JOIN dbo.Sports AS sp ON sp.Id = a.SportId
        LEFT JOIN windows AS w ON w.AthleteId = a.Id
        LEFT JOIN range_sessions AS rs ON rs.AthleteId = a.Id
        LEFT JOIN range_wellness AS rw ON rw.AthleteId = a.Id
        LEFT JOIN dbo.WellnessChecks AS wc ON wc.AthleteId = a.Id AND wc.[Date] = @To
        LEFT JOIN dbo.NutritionTargets AS n ON n.AthleteId = a.Id AND n.[Date] = @To
        OUTER APPLY (
            SELECT TOP (1) pn.Restriction
            FROM dbo.PhysioNotes AS pn
            WHERE pn.AthleteId = a.Id AND pn.[Date] <= @To
            ORDER BY pn.[Date] DESC, pn.Id DESC
        ) AS p
        WHERE a.ArchivedAt IS NULL  -- archived athletes stay hidden (ADR-07); Dapper does not see EF Core query filters
        ORDER BY a.Code;
        """;

    private const string DailyLoadsSql = """
        SELECT s.[Date], SUM(s.Rpe * s.DurationMinutes) AS Load
        FROM dbo.TrainingSessions AS s
        WHERE s.AthleteId = @AthleteId
          AND s.[Date] > DATEADD(day, -@Days, @Today) AND s.[Date] <= @Today
        GROUP BY s.[Date]
        ORDER BY s.[Date];
        """;

    private sealed record Row(
        int AthleteId, string Code, string DisplayName, string Sport, decimal BodyMassKg, int? BirthYear, string Sex,
        int TodayLoad, int AcuteSum, int ChronicSum,
        DateTime? WellnessDate, decimal? SleepHours, int? Soreness, int? Mood,
        int? EnergyKcal, decimal? CarbsGPerKg,
        string Restriction,
        int RangeLoad, int RangeSessions, int WellnessDays, decimal? AvgSleep, double? AvgSoreness, double? AvgMood);

    /// <param name="today">Today in Zurich, for the age (age reached this year).</param>
    /// <param name="from">First day of the period.</param>
    /// <param name="to">Last day of the period; the "as of" date for windows, wellness, nutrition and readiness.</param>
    public async Task<IReadOnlyList<DashboardRowDto>> GetAsync(DateOnly today, DateOnly from, DateOnly to, CancellationToken ct)
    {
        await using var connection = connections.Create();
        var rows = await connection.QueryAsync<Row>(new CommandDefinition(DashboardSql,
            new { From = from.ToDateTime(TimeOnly.MinValue), To = to.ToDateTime(TimeOnly.MinValue) }, cancellationToken: ct));
        var days = to.DayNumber - from.DayNumber + 1;

        return rows.Select(r =>
        {
            var load = LoadCalculator.FromSums(r.TodayLoad, r.AcuteSum, r.ChronicSum);
            var wellness = r.WellnessDate is { } d
                ? new WellnessSummaryDto(DateOnly.FromDateTime(d), r.SleepHours!.Value, r.Soreness!.Value, r.Mood!.Value)
                : null;
            var restriction = Enum.Parse<TrainingRestriction>(r.Restriction);
            var nutrition = r is { EnergyKcal: { } kcal, CarbsGPerKg: { } carbs }
                ? new NutritionTodayDto(kcal, NutritionCalculator.CarbsGrams(carbs, r.BodyMassKg))
                : null;

            return new DashboardRowDto(
                r.AthleteId, r.Code, r.DisplayName, r.Sport,
                today.Year - r.BirthYear, Enum.Parse<Sex>(r.Sex),
                load.TodayLoad, load.Acute7, load.Chronic28, load.Ratio,
                wellness,
                ReadinessEvaluator.Evaluate(wellness, load.Ratio, restriction),
                restriction,
                nutrition,
                new DashboardRangeDto(from, to, days, r.RangeLoad, LoadCalculator.RangeAverage(r.RangeLoad, days), r.RangeSessions,
                    r.WellnessDays, r.AvgSleep is { } sleep ? Math.Round(sleep, 1) : null,
                    r.AvgSoreness is { } soreness ? Math.Round(soreness, 1) : null,
                    r.AvgMood is { } mood ? Math.Round(mood, 1) : null));
        }).ToList();
    }

    /// <summary>Daily loads for the trend chart. Fetches 28 extra days so the first 28-day average is complete.</summary>
    public async Task<IReadOnlyList<DailyLoad>> GetDailyLoadsAsync(int athleteId, DateOnly today, int days, CancellationToken ct)
    {
        await using var connection = connections.Create();
        var rows = await connection.QueryAsync<(DateTime Date, int Load)>(new CommandDefinition(
            DailyLoadsSql,
            new { AthleteId = athleteId, Today = today.ToDateTime(TimeOnly.MinValue), Days = days + LoadCalculator.ChronicDays },
            cancellationToken: ct));

        return rows.Select(r => new DailyLoad(DateOnly.FromDateTime(r.Date), r.Load)).ToList();
    }
}

public sealed class SqlConnectionFactory(string connectionString)
{
    public SqlConnection Create() => new(connectionString);
}
