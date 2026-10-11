using System.Text.Json;
using AthleteDay.Api.Domain;
using AthleteDay.Api.Infrastructure;
using AthleteDay.Api.Reports;
using AthleteDay.Contracts;
using AthleteDay.Core;
using Microsoft.EntityFrameworkCore;

namespace AthleteDay.Api.Data;

/// <summary>
/// Synthetic demo data, generated relative to today so the dashboard always looks current.
/// No real athletes: names are invented, codes are pseudonymous. The scenario is built so the
/// dashboard shows a mix: one load spike, one sore athlete, one physio restriction, one missing check-in.
/// </summary>
public sealed class DemoDataSeeder(AppDbContext db, AppClock clock, ILogger<DemoDataSeeder> logger)
{
    private sealed record Profile(string Code, string Name, string Sport, int Age, Sex Sex, decimal Mass, int Kcal, decimal Carbs, string? KitchenNote);

    private static readonly Profile[] Profiles =
    [
        new("ATH-001", "Nora", "Alpine skiing", 24, Sex.Female, 66.0m, 3200, 6.0m, null),
        new("ATH-002", "Elias", "Ice hockey", 21, Sex.Male, 86.0m, 3900, 6.5m, "No pork"),
        new("ATH-003", "Lea", "Sprint", 19, Sex.Female, 61.0m, 2600, 5.0m, "Vegetarian"),
        new("ATH-004", "Noah", "Rowing", 27, Sex.Male, 84.0m, 4200, 8.0m, null),
        new("ATH-005", "Mila", "Football", 22, Sex.Female, 63.0m, 2700, 5.5m, "Lactose-free options"),
        new("ATH-006", "Jan", "Biathlon", 30, Sex.Male, 72.0m, 3600, 7.0m, null)
    ];

    private static readonly string[] ExtraSports = ["Cycling", "Swimming"];

    private static readonly SessionType[] Rotation =
        [SessionType.Strength, SessionType.Endurance, SessionType.Speed, SessionType.Technique, SessionType.OnIce, SessionType.Recovery];

    public async Task ResetAsync(CancellationToken ct = default)
    {
        await db.AuditEntries.ExecuteDeleteAsync(ct);
        await db.ProgressReviews.ExecuteDeleteAsync(ct);
        await db.PlanDayChanges.ExecuteDeleteAsync(ct);
        await db.PlanTemplateSessions.ExecuteDeleteAsync(ct);
        await db.Goals.ExecuteDeleteAsync(ct);
        await db.SessionNotes.ExecuteDeleteAsync(ct);
        await db.ImportRowErrors.ExecuteDeleteAsync(ct);
        await db.DeviceMeasurements.ExecuteDeleteAsync(ct);
        await db.ImportJobs.ExecuteDeleteAsync(ct);
        await db.PhysioNotes.ExecuteDeleteAsync(ct);
        await db.NutritionTargets.ExecuteDeleteAsync(ct);
        await db.WellnessChecks.ExecuteDeleteAsync(ct);
        await db.TrainingSessions.ExecuteDeleteAsync(ct);
        await db.Athletes.IgnoreQueryFilters().ExecuteDeleteAsync(ct);
        await db.Sports.ExecuteDeleteAsync(ct);
        // Keep demo athletes at ids 1–6 so links like /athletes/2 survive a reset.
        // (Only when rows were ever inserted; on a brand-new table RESEED 0 would make the first id 0.)
        await db.Database.ExecuteSqlRawAsync("""
            IF EXISTS (SELECT 1 FROM sys.identity_columns
                       WHERE object_id = OBJECT_ID('dbo.Athletes') AND last_value IS NOT NULL)
                DBCC CHECKIDENT ('dbo.Athletes', RESEED, 0);
            IF EXISTS (SELECT 1 FROM sys.identity_columns
                       WHERE object_id = OBJECT_ID('dbo.Sports') AND last_value IS NOT NULL)
                DBCC CHECKIDENT ('dbo.Sports', RESEED, 0);
            """, ct);
        await SeedAsync(ct);
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Athletes.IgnoreQueryFilters().AnyAsync(ct)) return;

        var today = clock.Today;

        // The managed sports list (ADR-10): the squad's sports plus two nobody does yet, to choose from.
        var sports = Profiles.Select(p => p.Sport).Concat(ExtraSports).Distinct().Order()
            .Select(name => new Sport { Name = name })
            .ToDictionary(sport => sport.Name);
        db.Sports.AddRange(sports.Values);

        var athletes = Profiles.Select(p => new Athlete
        {
            Code = p.Code, DisplayName = p.Name, Sport = sports[p.Sport], BodyMassKg = p.Mass, BirthYear = today.Year - p.Age, Sex = p.Sex
        }).ToList();
        db.Athletes.AddRange(athletes);
        await db.SaveChangesAsync(ct);

        foreach (var (athlete, index) in athletes.Select((a, i) => (a, i)))
        {
            var profile = Profiles[index];
            var random = new Random(42 + index);

            AddSessionHistory(athlete, today, random);
            AddWellnessHistory(athlete, today, random);

            // Two weeks of nutrition targets, so past days and periods show them too; today's are the profile's.
            for (var daysAgo = 13; daysAgo >= 0; daysAgo--)
            {
                db.NutritionTargets.Add(new NutritionTarget
                {
                    AthleteId = athlete.Id, Date = today.AddDays(-daysAgo),
                    EnergyKcal = profile.Kcal + (daysAgo == 0 ? 0 : random.Next(-3, 4) * 100),
                    CarbsGPerKg = profile.Carbs, KitchenNote = profile.KitchenNote, SetBy = "seed"
                });
            }
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
        AddSeedGoals(athletes, today);
        AddSeedPlans(athletes, today);

        await db.SaveChangesAsync(ct);

        // Notes and a review refer to saved sessions and figures, so they come last.
        await AddSeedSessionNotesAsync(athletes, today, ct);
        await AddSeedReviewAsync(athletes[0], ct);

        logger.LogInformation("Seeded synthetic demo data for {Count} athletes", athletes.Count);
    }

    private void AddSessionHistory(Athlete athlete, DateOnly today, Random random)
    {
        // 13 weeks, so the 12-week views of the athlete space are complete.
        for (var daysAgo = 90; daysAgo >= 0; daysAgo--)
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
        // 12 weeks of morning checks: enough for the 4-weeks-against-4 trends and the weekly sleep line.
        for (var daysAgo = 83; daysAgo >= 0; daysAgo--)
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

    /// <summary>
    /// Twelve weeks of device tests, so progress and benchmarks have a history: a weekly jump test for everyone,
    /// a sprint every two weeks for the sprint and team-sport athletes, and GPS distances where the sport uses them.
    /// Values improve slowly with some noise; the same seed gives the same numbers every time.
    /// </summary>
    private void AddSeedMeasurements(List<Athlete> athletes, DateOnly today)
    {
        var job = new ImportJob
        {
            Id = Guid.NewGuid(), FileName = "seed-device-tests.csv", BlobName = "seed", Status = ImportStatus.Completed,
            UploadedBy = "seed", UploadedAt = clock.Now, CompletedAt = clock.Now
        };
        var rows = 0;

        void Add(Athlete athlete, int daysAgo, string test, decimal value, string unit)
        {
            db.DeviceMeasurements.Add(new DeviceMeasurement
            {
                AthleteId = athlete.Id, Date = today.AddDays(-daysAgo), TestType = test, Value = value, Unit = unit, ImportJobId = job.Id
            });
            rows++;
        }

        foreach (var (athlete, index) in athletes.Select((a, i) => (a, i)))
        {
            var random = new Random(100 + index);
            var code = athlete.Code;
            for (var week = 11; week >= 0; week--)
            {
                var daysAgo = 7 * week + 2; // tests on a fixed weekday, two days before "today"'s weekday
                var progress = (11 - week) / 11m;  // 0 → 1 over the twelve weeks
                Add(athlete, daysAgo, "CMJ", Math.Round(32 + index * 2 + progress * 3 + random.Next(-8, 9) / 10m, 1), "cm");

                if (week % 2 == 0 && code is "ATH-002" or "ATH-003" or "ATH-005")
                    Add(athlete, daysAgo, "SPRINT_30M", Math.Round(4.45m - index * 0.03m - progress * 0.12m + random.Next(-3, 4) / 100m, 2), "s");

                if (code is "ATH-005" or "ATH-006")
                    Add(athlete, daysAgo, "GPS_DISTANCE", 7_000 + random.Next(0, 2_500), "m");
            }
        }

        job.TotalRows = job.ImportedRows = rows;
        db.ImportJobs.Add(job);
    }

    /// <summary>
    /// A standard week for everyone, shaped by the sport, and three dated changes with a reason (ADR-14):
    /// a lighter day after the camp, a day adapted to a physio restriction, and a cancelled day next week.
    /// </summary>
    private void AddSeedPlans(List<Athlete> athletes, DateOnly today)
    {
        foreach (var (athlete, index) in athletes.Select((a, i) => (a, i)))
        {
            var sport = Profiles[index].Sport;
            var main = sport switch
            {
                "Ice hockey" => SessionType.OnIce,
                "Sprint" or "Football" => SessionType.Speed,
                "Alpine skiing" => SessionType.Technique,
                _ => SessionType.Endurance
            };
            var week = new (int Day, SessionType Type, int Minutes, int Rpe, string? Note)[]
            {
                (1, SessionType.Strength, 60, 6, null),
                (2, main, 90, 7, null),
                (3, SessionType.Recovery, 30, 2, "Mobility and easy bike"),
                (4, SessionType.Technique, 75, 5, null),
                (5, SessionType.Strength, 60, 6, null),
                (5, main, 30, 7, "Short and sharp"),
                (6, main, 90, 8, sport is "Ice hockey" or "Football" ? "Match or match simulation" : "Race simulation"),
            };
            db.PlanTemplateSessions.AddRange(week.Select((w, position) => new PlanTemplateSession
            {
                AthleteId = athlete.Id, DayOfWeek = w.Day, Position = position, Type = w.Type,
                DurationMinutes = w.Minutes, Rpe = w.Rpe, Note = w.Note
            }));
        }

        var monday = ProgressCalculator.WeekStart(today);
        PlanDayChange Change(Athlete a, DateOnly date, string reason, params PlanDayChangeSession[] sessions) => new()
        {
            AthleteId = a.Id, Date = date, Reason = reason, Sessions = [.. sessions], ChangedBy = "lena", ChangedAt = clock.Now
        };
        db.PlanDayChanges.AddRange(
            Change(athletes[1], monday.AddDays(3), "Lighter after the camp",
                new PlanDayChangeSession { Position = 0, Type = SessionType.Recovery, DurationMinutes = 40, Rpe = 3 }),
            Change(athletes[4], monday.AddDays(5), "Physio restriction: no cutting or jumping",
                new PlanDayChangeSession { Position = 0, Type = SessionType.Endurance, DurationMinutes = 45, Rpe = 4, Note = "Bike" }),
            Change(athletes[2], monday.AddDays(9), "Travel to the meeting"));
    }

    /// <summary>A few goals: one open, one reached and one whose date has passed, so every status shows.</summary>
    private void AddSeedGoals(List<Athlete> athletes, DateOnly today)
    {
        Goal G(Athlete a, string test, decimal target, int? days, string? note) => new()
        {
            AthleteId = a.Id, TestType = test, Target = target, TargetDate = days is { } d ? today.AddDays(d) : null,
            Note = note, SetBy = "lena", SetAt = clock.Now
        };
        db.Goals.AddRange(
            G(athletes[0], "CMJ", 38.0m, 42, "Before the start of the race season"),
            G(athletes[2], "SPRINT_30M", 4.30m, 30, null),
            G(athletes[1], "CMJ", 36.0m, null, "Keep it through the camp"),
            G(athletes[4], "GPS_DISTANCE", 9_800, -3, "Match fitness"));
    }

    /// <summary>Coach's notes on a few recent sessions (ADR-12).</summary>
    private async Task AddSeedSessionNotesAsync(List<Athlete> athletes, DateOnly today, CancellationToken ct)
    {
        var notes = new (int Athlete, int Nth, string Text)[]
        {
            (1, 0, "Camp day: legs heavy after the second on-ice block. Kept the last drill short."),
            (1, 2, "Strong session, good intent in the sprints."),
            (2, 1, "Acceleration looked sharp; work on the last 10 m."),
            (0, 1, "Technique focus on landings. Quality over volume today."),
            (3, 0, "Low energy, cut the second set. Ask about sleep tomorrow."),
        };
        foreach (var (athleteIndex, nth, text) in notes)
        {
            var athlete = athletes[athleteIndex];
            var session = await db.TrainingSessions.Where(s => s.AthleteId == athlete.Id)
                .OrderByDescending(s => s.Date).Skip(nth).FirstAsync(ct);
            db.SessionNotes.Add(new SessionNote
            {
                SessionId = session.Id, AthleteId = athlete.Id, Text = text, Author = "lena",
                CreatedAt = session.Date.ToDateTime(new TimeOnly(18, 0)).ToUniversalTime()
            });
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>One review with its snapshot, computed like any other (ADR-13).</summary>
    private async Task AddSeedReviewAsync(Athlete athlete, CancellationToken ct)
    {
        var (from, to, snapshot) = await new AthleteFigures(db, clock).SnapshotAsync(athlete.Id, 12, ct);
        db.ProgressReviews.Add(new ProgressReview
        {
            AthleteId = athlete.Id, From = from, To = to,
            Observations = "Consistent training block with steady weekly load. Sleep mostly above 7 h.",
            Achievements = "Counter-movement jump up about 3 cm over the block.",
            Challenges = "Load dropped in the last week; worth a conversation about the reason.",
            Recommendations = "Keep two jump-focused sessions per week; check in on recovery before the season starts.",
            Author = "lena", CreatedAt = clock.Now.AddHours(-2),
            SnapshotJson = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        });
        await db.SaveChangesAsync(ct);
    }
}
