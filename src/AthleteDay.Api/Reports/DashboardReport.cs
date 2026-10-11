using AthleteDay.Contracts;
using AthleteDay.Core;
using Dapper;
using Microsoft.Data.SqlClient;

namespace AthleteDay.Api.Reports;

/// <summary>
/// Read side of the dashboard: hand-written T-SQL through Dapper, so one round trip returns every athlete
/// with load windows, today's wellness, nutrition and current restriction. Writes go through EF Core.
/// </summary>
public sealed class DashboardReport(SqlConnectionFactory connections)
{
    private const string DashboardSql = """
        WITH daily AS (
            SELECT s.AthleteId, s.[Date], SUM(s.Rpe * s.DurationMinutes) AS DailyLoad
            FROM dbo.TrainingSessions AS s
            WHERE s.[Date] > DATEADD(day, -28, @Today) AND s.[Date] <= @Today
            GROUP BY s.AthleteId, s.[Date]
        ),
        windows AS (
            SELECT d.AthleteId,
                   SUM(CASE WHEN d.[Date] = @Today THEN d.DailyLoad ELSE 0 END)                    AS TodayLoad,
                   SUM(CASE WHEN d.[Date] > DATEADD(day, -7, @Today) THEN d.DailyLoad ELSE 0 END)  AS AcuteSum,
                   SUM(d.DailyLoad)                                                                AS ChronicSum
            FROM daily AS d
            GROUP BY d.AthleteId
        )
        SELECT a.Id AS AthleteId, a.Code, a.DisplayName, a.Sport, a.BodyMassKg, a.BirthYear, a.Sex,
               COALESCE(w.TodayLoad, 0)  AS TodayLoad,
               COALESCE(w.AcuteSum, 0)   AS AcuteSum,
               COALESCE(w.ChronicSum, 0) AS ChronicSum,
               wc.[Date] AS WellnessDate, wc.SleepHours, wc.Soreness, wc.Mood,
               n.EnergyKcal, n.CarbsGPerKg,
               COALESCE(p.Restriction, 'None') AS Restriction
        FROM dbo.Athletes AS a
        LEFT JOIN windows AS w ON w.AthleteId = a.Id
        LEFT JOIN dbo.WellnessChecks AS wc ON wc.AthleteId = a.Id AND wc.[Date] = @Today
        LEFT JOIN dbo.NutritionTargets AS n ON n.AthleteId = a.Id AND n.[Date] = @Today
        OUTER APPLY (
            SELECT TOP (1) pn.Restriction
            FROM dbo.PhysioNotes AS pn
            WHERE pn.AthleteId = a.Id AND pn.[Date] <= @Today
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
        string Restriction);

    public async Task<IReadOnlyList<DashboardRowDto>> GetAsync(DateOnly today, CancellationToken ct)
    {
        await using var connection = connections.Create();
        var rows = await connection.QueryAsync<Row>(
            new CommandDefinition(DashboardSql, new { Today = today.ToDateTime(TimeOnly.MinValue) }, cancellationToken: ct));

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
                nutrition);
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
