using System.Security.Claims;
using System.Text.Json;
using AthleteDay.Api.Audit;
using AthleteDay.Api.Auth;
using AthleteDay.Api.Data;
using AthleteDay.Api.Domain;
using AthleteDay.Api.Infrastructure;
using AthleteDay.Api.Reports;
using AthleteDay.Api.Resources;
using AthleteDay.Contracts;
using AthleteDay.Core;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AthleteDay.Api.Endpoints;

/// <summary>
/// The athlete space (spec v1.7): progress, benchmarks and goals, session notes and progress reviews.
/// Notes and reviews are coaching documents: the coach writes them, coach and physio read them, reads are audited (ADR-12).
/// </summary>
public static class AthleteSpaceEndpoints
{
    public const int MinWeeks = 4;
    public const int MaxWeeks = 52;
    public const int DefaultWeeks = 12;

    private static readonly JsonSerializerOptions SnapshotJson = new(JsonSerializerDefaults.Web);

    public static RouteGroupBuilder MapAthleteSpaceEndpoints(this RouteGroupBuilder api)
    {
        var athlete = api.MapGroup("/athletes/{athleteId:int}").WithTags("Athlete space");

        athlete.MapGet("/progress", async Task<Results<Ok<ProgressDto>, NotFound, ValidationProblem>> (
                    int athleteId, int? weeks, AppDbContext db, AthleteFigures figures, CancellationToken ct) =>
                WeeksProblem(weeks) is { } problem ? problem
                : !await ExistsAsync(db, athleteId, ct) ? TypedResults.NotFound()
                : TypedResults.Ok(await figures.ProgressAsync(athleteId, weeks ?? DefaultWeeks, ct)))
            .RequireAuthorization(Policies.ViewDashboard)
            .Audited("Progress")
            .WithSummary("Weekly load, sessions and wellness, with the last 4 weeks compared to the 4 before");

        athlete.MapGet("/benchmarks", async Task<Results<Ok<IReadOnlyList<BenchmarkDto>>, NotFound, ValidationProblem>> (
                    int athleteId, int? weeks, AppDbContext db, AthleteFigures figures, CancellationToken ct) =>
                WeeksProblem(weeks) is { } problem ? problem
                : !await ExistsAsync(db, athleteId, ct) ? TypedResults.NotFound()
                : TypedResults.Ok(await figures.BenchmarksAsync(athleteId, weeks ?? DefaultWeeks, ct)))
            .RequireAuthorization(Policies.ViewDashboard)
            .Audited("Benchmarks")
            .WithSummary("Device tests against the athlete's history, the squad of the same sport and the goal");

        athlete.MapPut("/goals/{testType}", SetGoal)
            .RequireAuthorization(Policies.WriteCoaching)
            .Audited("Goal")
            .WithSummary("Set the goal for one device test (coach)");

        athlete.MapDelete("/goals/{testType}", async Task<Results<NoContent, NotFound>> (
                    int athleteId, string testType, AppDbContext db, CancellationToken ct) =>
                await db.Goals.Where(g => g.AthleteId == athleteId && g.TestType == testType).ExecuteDeleteAsync(ct) > 0
                    ? TypedResults.NoContent()
                    : TypedResults.NotFound())
            .RequireAuthorization(Policies.WriteCoaching)
            .Audited("Goal")
            .WithSummary("Remove the goal for one device test (coach)");

        athlete.MapGet("/session-notes", async (int athleteId, AppDbContext db, CancellationToken ct) =>
                await db.SessionNotes.AsNoTracking()
                    .Where(n => n.AthleteId == athleteId)
                    .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
                    .Select(n => new SessionNoteDto(n.Id, n.SessionId, n.AthleteId, n.Session.Date, n.Session.Type,
                        n.Session.Rpe * n.Session.DurationMinutes, n.Text, n.Author, n.CreatedAt))
                    .ToListAsync(ct))
            .RequireAuthorization(Policies.CoachingNotes)
            .Audited("SessionNotes")
            .WithSummary("Notes on the athlete's sessions, newest first (coach and physio)");

        athlete.MapGet("/reviews", async (int athleteId, AppDbContext db, CancellationToken ct) =>
                (await db.ProgressReviews.AsNoTracking()
                    .Where(r => r.AthleteId == athleteId)
                    .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
                    .ToListAsync(ct))
                .Select(ToDto)
                .ToList())
            .RequireAuthorization(Policies.CoachingNotes)
            .Audited("Reviews")
            .WithSummary("Progress reviews of the athlete, newest first, with their snapshots (coach and physio)");

        athlete.MapPost("/reviews", AddReview)
            .RequireAuthorization(Policies.WriteCoaching)
            .Audited("Reviews")
            .WithSummary("Write a progress review for the last weeks; its figures are stored with it (coach)");

        api.MapPost("/sessions/{sessionId:int}/notes", AddSessionNote)
            .RequireAuthorization(Policies.WriteCoaching)
            .Audited("SessionNotes")
            .WithTags("Athlete space")
            .WithSummary("Add a note to a training session (coach)");

        return api;
    }

    private static async Task<Results<Ok<GoalDto>, NotFound, ValidationProblem>> SetGoal(
        int athleteId, string testType, GoalRequest request, ClaimsPrincipal user, AppDbContext db, AppClock clock,
        CancellationToken ct)
    {
        if (!DeviceTests.TryGet(testType, out var test))
            return Problem("testType", string.Format(ApiText.UnknownTest, testType));
        if (!test.IsPlausible(request.Target))
            return Problem(nameof(GoalRequest.Target), string.Format(ApiText.GoalOutOfRange, test.Min, test.Max, test.Unit, test.Type));
        if (!await ExistsAsync(db, athleteId, ct)) return TypedResults.NotFound();

        var goal = await db.Goals.SingleOrDefaultAsync(g => g.AthleteId == athleteId && g.TestType == test.Type, ct);
        if (goal is null)
        {
            goal = new Goal { AthleteId = athleteId, TestType = test.Type, SetBy = user.UserName() };
            db.Goals.Add(goal);
        }
        goal.Target = request.Target;
        goal.TargetDate = request.TargetDate;
        goal.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        goal.SetBy = user.UserName();
        goal.SetAt = clock.Now;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new GoalDto(goal.TestType, goal.Target, goal.TargetDate, goal.Note, goal.SetBy));
    }

    private static async Task<Results<Created<SessionNoteDto>, NotFound>> AddSessionNote(
        int sessionId, SessionNoteRequest request, ClaimsPrincipal user, AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var session = await db.TrainingSessions.FindAsync([sessionId], ct);
        if (session is null || !await ExistsAsync(db, session.AthleteId, ct)) return TypedResults.NotFound();

        var note = new SessionNote
        {
            SessionId = session.Id, AthleteId = session.AthleteId, Text = request.Text.Trim(), Author = user.UserName(), CreatedAt = clock.Now
        };
        db.SessionNotes.Add(note);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/athletes/{session.AthleteId}/session-notes", new SessionNoteDto(
            note.Id, note.SessionId, note.AthleteId, session.Date, session.Type,
            LoadCalculator.SessionLoad(session.Rpe, session.DurationMinutes), note.Text, note.Author, note.CreatedAt));
    }

    private static async Task<Results<Created<ProgressReviewDto>, NotFound>> AddReview(
        int athleteId, ProgressReviewRequest request, ClaimsPrincipal user, AppDbContext db, AthleteFigures figures,
        AppClock clock, CancellationToken ct)
    {
        if (!await ExistsAsync(db, athleteId, ct)) return TypedResults.NotFound();

        var (from, to, snapshot) = await figures.SnapshotAsync(athleteId, request.Weeks, ct);
        var review = new ProgressReview
        {
            AthleteId = athleteId, From = from, To = to,
            Observations = Clean(request.Observations), Achievements = Clean(request.Achievements),
            Challenges = Clean(request.Challenges), Recommendations = Clean(request.Recommendations),
            Author = user.UserName(), CreatedAt = clock.Now,
            SnapshotJson = JsonSerializer.Serialize(snapshot, SnapshotJson)
        };
        db.ProgressReviews.Add(review);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/athletes/{athleteId}/reviews", ToDto(review));
    }

    private static ProgressReviewDto ToDto(ProgressReview r) => new(
        r.Id, r.AthleteId, r.From, r.To, r.Observations, r.Achievements, r.Challenges, r.Recommendations, r.Author, r.CreatedAt,
        JsonSerializer.Deserialize<ReviewSnapshotDto>(r.SnapshotJson, SnapshotJson)!);

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    // The archive filter applies: an archived athlete has no space.
    private static Task<bool> ExistsAsync(AppDbContext db, int athleteId, CancellationToken ct) =>
        db.Athletes.AnyAsync(a => a.Id == athleteId, ct);

    private static ValidationProblem? WeeksProblem(int? weeks) =>
        weeks is < MinWeeks or > MaxWeeks ? Problem("weeks", ApiText.ProgressWeeksRange) : null;

    private static ValidationProblem Problem(string field, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}
