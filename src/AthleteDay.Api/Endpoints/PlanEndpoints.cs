using System.Security.Claims;
using AthleteDay.Api.Auth;
using AthleteDay.Api.Data;
using AthleteDay.Api.Domain;
using AthleteDay.Api.Infrastructure;
using AthleteDay.Api.Realtime;
using AthleteDay.Contracts;
using AthleteDay.Core;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AthleteDay.Api.Endpoints;

/// <summary>
/// Weekly training plans (spec v1.8, ADR-14): a standard week per athlete plus whole-day changes. The coach writes;
/// coach, physio and nutrition read; an athlete reads only their own plan.
/// </summary>
public static class PlanEndpoints
{
    public static RouteGroupBuilder MapPlanEndpoints(this RouteGroupBuilder api)
    {
        var plan = api.MapGroup("/athletes/{athleteId:int}/plan").WithTags("Training plan");

        plan.MapGet("/week", GetWeek)
            .RequireAuthorization(Policies.ReadPlan)
            .WithSummary("One week (Monday to Sunday) with standard and changed days, planned against logged load");

        plan.MapGet("/template", GetTemplate)
            .RequireAuthorization(Policies.ReadPlan)
            .WithSummary("The athlete's standard week");

        plan.MapPut("/template", SetTemplate)
            .RequireAuthorization(Policies.WriteCoaching)
            .WithSummary("Replace the standard week (coach); dated changes stay as they are");

        plan.MapPut("/days/{date}", ChangeDay)
            .RequireAuthorization(Policies.WriteCoaching)
            .WithSummary("Replace the sessions of one date (coach); no sessions cancels the day");

        plan.MapDelete("/days/{date}", async Task<Results<NoContent, NotFound>> (
                    int athleteId, DateOnly date, AppDbContext db, IDashboardNotifier notifier, CancellationToken ct) =>
                {
                    var removed = await db.PlanDayChanges.Where(c => c.AthleteId == athleteId && c.Date == date).ExecuteDeleteAsync(ct);
                    if (removed == 0) return TypedResults.NotFound();
                    await notifier.DataChangedAsync("plan", ct);
                    return TypedResults.NoContent();
                })
            .RequireAuthorization(Policies.WriteCoaching)
            .WithSummary("Undo the change of one date (coach): the standard week applies again");

        return api;
    }

    private static async Task<Results<Ok<WeekPlanDto>, NotFound, ForbidHttpResult>> GetWeek(
        int athleteId, DateOnly? start, ClaimsPrincipal user, AppDbContext db, AppClock clock, CancellationToken ct)
    {
        switch (await AccessAsync(db, athleteId, user, ct))
        {
            case Access.Unknown: return TypedResults.NotFound();
            case Access.Denied: return TypedResults.Forbid();
        }

        var monday = ProgressCalculator.WeekStart(start ?? clock.Today);
        var sunday = monday.AddDays(6);
        var changes = await db.PlanDayChanges.AsNoTracking()
            .Where(c => c.AthleteId == athleteId && c.Date >= monday && c.Date <= sunday)
            .ToListAsync(ct);
        var logged = await db.TrainingSessions.AsNoTracking()
            .Where(s => s.AthleteId == athleteId && s.Date >= monday && s.Date <= sunday)
            .Select(s => new DailyLoad(s.Date, s.Rpe * s.DurationMinutes))
            .ToListAsync(ct);

        return TypedResults.Ok(PlanCalculator.BuildWeek(
            athleteId, monday,
            await TemplateAsync(db, athleteId, ct),
            changes.ToDictionary(c => c.Date, c => new DayChange(c.Reason,
                c.Sessions.OrderBy(s => s.Position).Select(s => PlanCalculator.Planned(s.Type, s.DurationMinutes, s.Rpe, s.Note)).ToList())),
            logged));
    }

    private static async Task<Results<Ok<PlanTemplateDto>, NotFound, ForbidHttpResult>> GetTemplate(
        int athleteId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        switch (await AccessAsync(db, athleteId, user, ct))
        {
            case Access.Unknown: return TypedResults.NotFound();
            case Access.Denied: return TypedResults.Forbid();
        }
        return TypedResults.Ok(ToDto(athleteId, await TemplateAsync(db, athleteId, ct)));
    }

    private static async Task<Results<Ok<PlanTemplateDto>, NotFound>> SetTemplate(
        int athleteId, PlanTemplateRequest request, AppDbContext db, IDashboardNotifier notifier, CancellationToken ct)
    {
        if (!await db.Athletes.AnyAsync(a => a.Id == athleteId, ct)) return TypedResults.NotFound();

        // Only this athlete's standard week is replaced; other athletes and dated changes are untouched (AC-78).
        await db.PlanTemplateSessions.Where(t => t.AthleteId == athleteId).ExecuteDeleteAsync(ct);
        db.PlanTemplateSessions.AddRange(request.Days.SelectMany(d => d.Sessions.Select((s, i) => new PlanTemplateSession
        {
            AthleteId = athleteId, DayOfWeek = d.DayOfWeek, Position = i,
            Type = s.Type, DurationMinutes = s.DurationMinutes, Rpe = s.Rpe, Note = Clean(s.Note)
        })));
        await db.SaveChangesAsync(ct);
        await notifier.DataChangedAsync("plan", ct);
        return TypedResults.Ok(ToDto(athleteId, await TemplateAsync(db, athleteId, ct)));
    }

    private static async Task<Results<NoContent, NotFound>> ChangeDay(
        int athleteId, DateOnly date, PlanDayChangeRequest request, ClaimsPrincipal user, AppDbContext db, AppClock clock,
        IDashboardNotifier notifier, CancellationToken ct)
    {
        if (!await db.Athletes.AnyAsync(a => a.Id == athleteId, ct)) return TypedResults.NotFound();

        var change = await db.PlanDayChanges.SingleOrDefaultAsync(c => c.AthleteId == athleteId && c.Date == date, ct);
        if (change is null)
        {
            change = new PlanDayChange { AthleteId = athleteId, Date = date, ChangedBy = user.UserName() };
            db.PlanDayChanges.Add(change);
        }
        change.Reason = Clean(request.Reason);
        change.Sessions = request.Sessions.Select((s, i) => new PlanDayChangeSession
        {
            Position = i, Type = s.Type, DurationMinutes = s.DurationMinutes, Rpe = s.Rpe, Note = Clean(s.Note)
        }).ToList();
        change.ChangedBy = user.UserName();
        change.ChangedAt = clock.Now;
        await db.SaveChangesAsync(ct);
        await notifier.DataChangedAsync("plan", ct);
        return TypedResults.NoContent();
    }

    /// <summary>Unknown athletes are 404; an athlete may only read their own plan (like the morning check, AC-82).</summary>
    private enum Access { Allowed, Unknown, Denied }

    private static async Task<Access> AccessAsync(AppDbContext db, int athleteId, ClaimsPrincipal user, CancellationToken ct)
    {
        var code = await db.Athletes.Where(a => a.Id == athleteId).Select(a => a.Code).SingleOrDefaultAsync(ct);
        if (code is null) return Access.Unknown;
        return user.IsInRole(Roles.Athlete) && !string.Equals(code, user.UserName(), StringComparison.OrdinalIgnoreCase)
            ? Access.Denied
            : Access.Allowed;
    }

    private static async Task<IReadOnlyDictionary<int, IReadOnlyList<PlannedSessionDto>>> TemplateAsync(
        AppDbContext db, int athleteId, CancellationToken ct) =>
        (await db.PlanTemplateSessions.AsNoTracking()
            .Where(t => t.AthleteId == athleteId)
            .OrderBy(t => t.DayOfWeek).ThenBy(t => t.Position)
            .ToListAsync(ct))
        .GroupBy(t => t.DayOfWeek)
        .ToDictionary(g => g.Key, g => (IReadOnlyList<PlannedSessionDto>)g
            .Select(t => PlanCalculator.Planned(t.Type, t.DurationMinutes, t.Rpe, t.Note)).ToList());

    private static PlanTemplateDto ToDto(int athleteId, IReadOnlyDictionary<int, IReadOnlyList<PlannedSessionDto>> template) =>
        new(athleteId, Enumerable.Range(1, 7).Select(d => new PlanTemplateDayDto(d, template.GetValueOrDefault(d) ?? [])).ToList());

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
