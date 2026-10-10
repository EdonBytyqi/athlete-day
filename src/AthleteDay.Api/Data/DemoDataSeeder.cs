using AthleteDay.Api.Domain;
using AthleteDay.Api.Infrastructure;
using AthleteDay.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AthleteDay.Api.Data;

/// <summary>
/// Synthetic demo data, generated relative to today so the dashboard always looks current.
/// No real athletes: names are invented, codes are pseudonymous. The scenario is built so the
/// dashboard shows a mix: one load spike, one sore athlete, one physio restriction, one missing check-in.
/// </summary>
public sealed class DemoDataSeeder(AppDbContext db, AppClock clock, ILogger<DemoDataSeeder> logger)
{
    private sealed record Profile(string Code, string Name, string Sport, decimal Mass, int Kcal, decimal Carbs, string? KitchenNote);

    private static readonly Profile[] Profiles =
    [
        new("ATH-001", "Nora", "Alpine skiing", 66.0m, 3200, 6.0m, null),
        new("ATH-002", "Elias", "Ice hockey", 86.0m, 3900, 6.5m, "No pork"),
        new("ATH-003", "Lea", "Sprint", 61.0m, 2600, 5.0m, "Vegetarian"),
        new("ATH-004", "Noah", "Rowing", 84.0m, 4200, 8.0m, null),
        new("ATH-005", "Mila", "Football", 63.0m, 2700, 5.5m, "Lactose-free options"),
        new("ATH-006", "Jan", "Biathlon", 72.0m, 3600, 7.0m, null)
    ];

    private static readonly SessionType[] Rotation =
        [SessionType.Strength, SessionType.Endurance, SessionType.Speed, SessionType.Technique, SessionType.OnIce, SessionType.Recovery];

    public async Task ResetAsync(CancellationToken ct = default)
    {
        await db.AuditEntries.ExecuteDeleteAsync(ct);
        await db.ImportRowErrors.ExecuteDeleteAsync(ct);
        await db.DeviceMeasurements.ExecuteDeleteAsync(ct);
        await db.ImportJobs.ExecuteDeleteAsync(ct);
        await db.PhysioNotes.ExecuteDeleteAsync(ct);
        await db.NutritionTargets.ExecuteDeleteAsync(ct);
        await db.WellnessChecks.ExecuteDeleteAsync(ct);
        await db.TrainingSessions.ExecuteDeleteAsync(ct);
        await db.Athletes.ExecuteDeleteAsync(ct);
        // Keep demo athletes at ids 1–6 so links like /athletes/2 survive a reset.
        // (Only when rows were ever inserted; on a brand-new table RESEED 0 would make the first id 0.)
        await db.Database.ExecuteSqlRawAsync("""
            IF EXISTS (SELECT 1 FROM sys.identity_columns
                       WHERE object_id = OBJECT_ID('dbo.Athletes') AND last_value IS NOT NULL)
                DBCC CHECKIDENT ('dbo.Athletes', RESEED, 0);
            """, ct);
        await SeedAsync(ct);
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Athletes.AnyAsync(ct)) return;

        var today = clock.Today;
        var athletes = Profiles.Select(p => new Athlete { Code = p.Code, DisplayName = p.Name, Sport = p.Sport, BodyMassKg = p.Mass }).ToList();
        db.Athletes.AddRange(athletes);
        await db.SaveChangesAsync(ct);

        foreach (var (athlete, index) in athletes.Select((a, i) => (a, i)))
        {
            var profile = Profiles[index];
            var random = new Random(42 + index);

            AddSessionHistory(athlete, today, random);
            AddWellnessHistory(athlete, today, random);

            db.NutritionTargets.Add(new NutritionTarget
            {
                AthleteId = athlete.Id, Date = today, EnergyKcal = profile.Kcal, CarbsGPerKg = profile.Carbs,
                KitchenNote = profile.KitchenNote, SetBy = "seed"
            });
        }

        db.PhysioNotes.AddRange(
            new PhysioNote
            {
                AthleteId = athletes[0].Id, Date = today.AddDays(-20), Restriction = TrainingRestriction.None,
                Note = "Routine screening, no findings.", Author = "seed"
            },
            new PhysioNote
            {
                AthleteId = athletes[4].Id, Date = today.AddDays(-2), Restriction = TrainingRestriction.Modified,
                Note = "Synthetic example: mild ankle sprain. No cutting or jumping; bike and upper body fine.", Author = "seed"
            });

        AddSeedMeasurements(athletes, today);

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded synthetic demo data for {Count} athletes", athletes.Count);
    }

    private void AddSessionHistory(Athlete athlete, DateOnly today, Random random)
    {
        for (var daysAgo = 55; daysAgo >= 0; daysAgo--)
        {
            var date = today.AddDays(-daysAgo);
            var isRestDay = date.DayOfWeek == DayOfWeek.Sunday || (date.DayOfWeek == DayOfWeek.Wednesday && random.Next(2) == 0);
            if (isRestDay) continue;

            // Only some athletes have trained already this morning.
            if (daysAgo == 0 && athlete.Code is not ("ATH-001" or "ATH-002" or "ATH-004")) continue;

            var duration = random.Next(45, 100);
            var rpe = random.Next(4, 8);

            // ATH-002: a hard training camp this week, so the 7-day load jumps above the 28-day average.
            if (athlete.Code == "ATH-002" && daysAgo < 7)
            {
                duration = random.Next(100, 130);
                rpe = random.Next(7, 10);
            }
            // ATH-005: training reduced since the physio restriction two days ago.
            if (athlete.Code == "ATH-005" && daysAgo < 2)
            {
                duration = 40;
                rpe = 3;
            }

            db.TrainingSessions.Add(new TrainingSession
            {
                AthleteId = athlete.Id,
                Date = date,
                Type = athlete.Code == "ATH-002" && random.Next(2) == 0 ? SessionType.OnIce : Rotation[random.Next(Rotation.Length - 1)],
                DurationMinutes = duration,
                Rpe = rpe,
                LoggedBy = "seed",
                LoggedAt = clock.Now
            });
        }
    }

    private void AddWellnessHistory(Athlete athlete, DateOnly today, Random random)
    {
        for (var daysAgo = 13; daysAgo >= 0; daysAgo--)
        {
            // ATH-003 has not done today's check-in yet: the athlete persona fills it in during the demo.
            if (daysAgo == 0 && athlete.Code == "ATH-003") continue;

            var check = new WellnessCheck
            {
                AthleteId = athlete.Id,
                Date = today.AddDays(-daysAgo),
                SleepHours = 6.5m + random.Next(0, 4) * 0.5m,
                Soreness = random.Next(1, 4),
                Mood = random.Next(3, 6)
            };

            if (daysAgo == 0 && athlete.Code == "ATH-004") check.Soreness = 4;
            if (daysAgo == 0 && athlete.Code == "ATH-002") check.SleepHours = 5.5m;

            db.WellnessChecks.Add(check);
        }
    }

    private void AddSeedMeasurements(List<Athlete> athletes, DateOnly today)
    {
        var job = new ImportJob
        {
            Id = Guid.NewGuid(), FileName = "seed-jump-tests.csv", BlobName = "seed", Status = ImportStatus.Completed,
            UploadedBy = "seed", UploadedAt = clock.Now, CompletedAt = clock.Now
        };

        foreach (var (athlete, index) in athletes.Select((a, i) => (a, i)))
        {
            for (var week = 3; week >= 1; week--)
            {
                db.DeviceMeasurements.Add(new DeviceMeasurement
                {
                    AthleteId = athlete.Id, Date = today.AddDays(-7 * week), TestType = "CMJ",
                    Value = 34 + index * 2 + (3 - week) * 0.8m, Unit = "cm", ImportJobId = job.Id
                });
            }
        }

        job.TotalRows = job.ImportedRows = athletes.Count * 3;
        db.ImportJobs.Add(job);
    }
}
