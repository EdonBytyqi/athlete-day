using AthleteDay.Api.Auth;
using AthleteDay.Api.Data;
using AthleteDay.Api.Domain;
using AthleteDay.Api.Infrastructure;
using AthleteDay.Api.Realtime;
using AthleteDay.Api.Resources;
using AthleteDay.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AthleteDay.Api.Endpoints;

/// <summary>The managed sports list (ADR-10): read by everyone who sees athletes, changed by the admin only.</summary>
public static class SportEndpoints
{
    public static RouteGroupBuilder MapSportEndpoints(this RouteGroupBuilder api)
    {
        var sports = api.MapGroup("/sports").WithTags("Sports");

        sports.MapGet("/", async (AppDbContext db, CancellationToken ct) =>
                await db.Sports.AsNoTracking()
                    .OrderBy(s => s.Name)
                    .Select(s => new SportDto(s.Id, s.Name, s.RetiredAt != null))
                    .ToListAsync(ct))
            .RequireAuthorization(Policies.ReadSports)
            .WithSummary("The sports list, active and retired");

        sports.MapPost("/", AddSport)
            .RequireAuthorization(Policies.ManageSports)
            .WithSummary("Add a sport (admin)");

        sports.MapPut("/{sportId:int}", ChangeSport)
            .RequireAuthorization(Policies.ManageSports)
            .WithSummary("Rename, retire or reactivate a sport (admin); a rename shows everywhere at once");

        return api;
    }

    private static async Task<Results<Created<SportDto>, ValidationProblem>> AddSport(
        SportRequest request, AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var name = request.Name.Trim();
        if (await NameTakenAsync(db, name, exceptId: null, ct)) return NameTaken();

        var sport = new Sport { Name = name, RetiredAt = request.Retired ? clock.Now : null };
        db.Sports.Add(sport);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/sports/{sport.Id}", ToDto(sport));
    }

    private static async Task<Results<Ok<SportDto>, NotFound, ValidationProblem>> ChangeSport(
        int sportId, SportRequest request, AppDbContext db, AppClock clock, IDashboardNotifier notifier, CancellationToken ct)
    {
        var sport = await db.Sports.FindAsync([sportId], ct);
        if (sport is null) return TypedResults.NotFound();

        var name = request.Name.Trim();
        if (await NameTakenAsync(db, name, exceptId: sportId, ct)) return NameTaken();

        sport.Name = name;
        sport.RetiredAt = request.Retired ? sport.RetiredAt ?? clock.Now : null;
        await db.SaveChangesAsync(ct);
        await notifier.DataChangedAsync("sport", ct); // a rename changes what dashboards show
        return TypedResults.Ok(ToDto(sport));
    }

    // The column's case-insensitive collation makes "ice hockey" and "Ice Hockey" the same name.
    private static Task<bool> NameTakenAsync(AppDbContext db, string name, int? exceptId, CancellationToken ct) =>
        db.Sports.AnyAsync(s => s.Name == name && s.Id != exceptId, ct);

    private static ValidationProblem NameTaken() =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Name"] = [ApiText.SportNameTaken] });

    private static SportDto ToDto(Sport s) => new(s.Id, s.Name, s.RetiredAt != null);
}
