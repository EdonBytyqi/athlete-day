using AthleteDay.Api.Audit;
using AthleteDay.Api.Auth;
using AthleteDay.Api.Data;
using AthleteDay.Api.Domain;
using AthleteDay.Api.Infrastructure;
using AthleteDay.Api.Resources;
using AthleteDay.Api.Realtime;
using AthleteDay.Contracts;
using AthleteDay.Core;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AthleteDay.Api.Endpoints;

/// <summary>The writes from the people in the hall: coach, athlete, nutrition and physio. All through EF Core.</summary>
public static class StaffInputEndpoints
{
    public static RouteGroupBuilder MapStaffInputEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/sessions", LogSession)
            .RequireAuthorization(Policies.LogSessions)
            .WithTags("Coach")
            .WithSummary("Log a training session (type, duration, RPE)");

        api.MapPut("/wellness", SubmitWellness)
            .RequireAuthorization(Policies.SubmitWellness)
            .WithTags("Athlete")
            .WithSummary("Morning wellness check; athletes can only submit their own");

        api.MapPut("/nutrition-targets", SetNutritionTarget)
            .RequireAuthorization(Policies.SetNutrition)
            .WithTags("Nutrition")
            .WithSummary("Set an athlete's daily energy and carbohydrate target");

        api.MapGet("/athletes/{athleteId:int}/physio-notes", GetPhysioNotes)
            .RequireAuthorization(Policies.HealthNotes)
            .Audited("PhysioNotes")
            .WithTags("Physio")
            .WithSummary("Free-text health notes (physio only)");

        api.MapPost("/physio-notes", AddPhysioNote)
            .RequireAuthorization(Policies.HealthNotes)
            .Audited("PhysioNotes")
            .WithTags("Physio")
            .WithSummary("Add a health note and training restriction");

        return api;
    }

    private static async Task<Results<Created<TrainingSessionDto>, ValidationProblem>> LogSession(
        LogSessionRequest request, ClaimsPrincipal user, AppDbContext db, AppClock clock,
        IDashboardNotifier notifier, CancellationToken ct)
    {
        var athlete = await db.Athletes.FindAsync([request.AthleteId], ct);
        if (athlete is null) return UnknownAthlete();

        var session = new TrainingSession
        {
            AthleteId = athlete.Id,
            Date = request.Date,
            Type = request.Type,
            DurationMinutes = request.DurationMinutes,
            Rpe = request.Rpe,
            Notes = request.Notes,
            LoggedBy = user.UserName(),
            LoggedAt = clock.Now
        };
        db.TrainingSessions.Add(session);
        await db.SaveChangesAsync(ct);
        await notifier.DataChangedAsync("session", ct);

        var dto = new TrainingSessionDto(session.Id, athlete.Id, athlete.Code, session.Date, session.Type,
            session.DurationMinutes, session.Rpe, LoadCalculator.SessionLoad(session.Rpe, session.DurationMinutes),
            session.LoggedBy);
        return TypedResults.Created($"/api/athletes/{athlete.Id}/sessions", dto);
    }

    private static async Task<Results<Ok<WellnessSummaryDto>, ValidationProblem, ForbidHttpResult>> SubmitWellness(
        WellnessCheckRequest request, ClaimsPrincipal user, AppDbContext db, IDashboardNotifier notifier,
        CancellationToken ct)
    {
        var athlete = await db.Athletes.FindAsync([request.AthleteId], ct);
        if (athlete is null) return UnknownAthlete();

        // The athlete persona's user name is their code: nobody can file a wellness check for someone else.
        if (!string.Equals(athlete.Code, user.UserName(), StringComparison.OrdinalIgnoreCase))
            return TypedResults.Forbid();

        var check = await db.WellnessChecks.SingleOrDefaultAsync(w => w.AthleteId == athlete.Id && w.Date == request.Date, ct);
        if (check is null)
        {
            check = new WellnessCheck { AthleteId = athlete.Id, Date = request.Date };
            db.WellnessChecks.Add(check);
        }
        check.SleepHours = request.SleepHours;
        check.Soreness = request.Soreness;
        check.Mood = request.Mood;

        await db.SaveChangesAsync(ct);
        await notifier.DataChangedAsync("wellness", ct);
        return TypedResults.Ok(new WellnessSummaryDto(check.Date, check.SleepHours, check.Soreness, check.Mood));
    }

    private static async Task<Results<Ok<NutritionTodayDto>, ValidationProblem>> SetNutritionTarget(
        NutritionTargetRequest request, ClaimsPrincipal user, AppDbContext db, IDashboardNotifier notifier,
        CancellationToken ct)
    {
        var athlete = await db.Athletes.FindAsync([request.AthleteId], ct);
        if (athlete is null) return UnknownAthlete();

        var target = await db.NutritionTargets.SingleOrDefaultAsync(n => n.AthleteId == athlete.Id && n.Date == request.Date, ct);
        if (target is null)
        {
            target = new NutritionTarget { AthleteId = athlete.Id, Date = request.Date, SetBy = user.UserName() };
            db.NutritionTargets.Add(target);
        }
        target.EnergyKcal = request.EnergyKcal;
        target.CarbsGPerKg = request.CarbsGPerKg;
        target.KitchenNote = string.IsNullOrWhiteSpace(request.KitchenNote) ? null : request.KitchenNote.Trim();
        target.SetBy = user.UserName();

        await db.SaveChangesAsync(ct);
        await notifier.DataChangedAsync("nutrition", ct);
        return TypedResults.Ok(new NutritionTodayDto(target.EnergyKcal, NutritionCalculator.CarbsGrams(target.CarbsGPerKg, athlete.BodyMassKg)));
    }

    private static async Task<List<PhysioNoteDto>> GetPhysioNotes(int athleteId, AppDbContext db, CancellationToken ct) =>
        await db.PhysioNotes.AsNoTracking()
            .Where(p => p.AthleteId == athleteId)
            .OrderByDescending(p => p.Date).ThenByDescending(p => p.Id)
            .Select(p => new PhysioNoteDto(p.Id, p.AthleteId, p.Date, p.Restriction, p.Note, p.Author))
            .ToListAsync(ct);

    private static async Task<Results<Created<PhysioNoteDto>, ValidationProblem>> AddPhysioNote(
        PhysioNoteRequest request, ClaimsPrincipal user, AppDbContext db, IDashboardNotifier notifier,
        CancellationToken ct)
    {
        if (!await db.Athletes.AnyAsync(a => a.Id == request.AthleteId, ct)) return UnknownAthlete();

        var note = new PhysioNote
        {
            AthleteId = request.AthleteId,
            Date = request.Date,
            Restriction = request.Restriction,
            Note = request.Note.Trim(),
            Author = user.UserName()
        };
        db.PhysioNotes.Add(note);
        await db.SaveChangesAsync(ct);
        await notifier.DataChangedAsync("physio", ct);

        return TypedResults.Created($"/api/athletes/{note.AthleteId}/physio-notes",
            new PhysioNoteDto(note.Id, note.AthleteId, note.Date, note.Restriction, note.Note, note.Author));
    }

    private static ValidationProblem UnknownAthlete() =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["AthleteId"] = [ApiText.UnknownAthlete] });
}
