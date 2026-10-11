using AthleteDay.Api.Auth;
using AthleteDay.Api.Data;
using AthleteDay.Api.Domain;
using AthleteDay.Api.Imports;
using AthleteDay.Api.Infrastructure;
using AthleteDay.Api.Resources;
using AthleteDay.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;

namespace AthleteDay.Api.Endpoints;

public static class ImportEndpoints
{
    public const long MaxFileBytes = 2 * 1024 * 1024;

    public static RouteGroupBuilder MapImportEndpoints(this RouteGroupBuilder api)
    {
        var imports = api.MapGroup("/imports").WithTags("Device import").RequireAuthorization(Policies.ImportDeviceData);

        // Header-based auth (no cookies) means no CSRF risk, so the antiforgery check is not needed here.
        imports.MapPost("/", Upload)
            .DisableAntiforgery()
            .WithSummary("Upload a device CSV export. It is stored in Blob Storage and processed asynchronously.");

        imports.MapGet("/", async (AppDbContext db, CancellationToken ct) =>
                await db.ImportJobs.AsNoTracking()
                    .OrderByDescending(j => j.UploadedAt)
                    .Take(20)
                    .Select(j => ToDto(j, Array.Empty<ImportRowErrorDto>()))
                    .ToListAsync(ct))
            .WithSummary("Recent imports");

        imports.MapGet("/{id:guid}", async Task<Results<Ok<ImportJobDto>, NotFound>> (Guid id, AppDbContext db, CancellationToken ct) =>
            {
                var job = await db.ImportJobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == id, ct);
                if (job is null) return TypedResults.NotFound();

                var errors = await db.ImportRowErrors.AsNoTracking()
                    .Where(e => e.ImportJobId == id)
                    .OrderBy(e => e.RowNumber)
                    .Select(e => new ImportRowErrorDto(e.RowNumber, e.Column, e.Reason, e.RawLine))
                    .ToListAsync(ct);
                return TypedResults.Ok(ToDto(job, errors));
            })
            .WithSummary("Validation report of one import: rejected rows and why");

        return api;
    }

    private static async Task<Results<Accepted<ImportJobDto>, ValidationProblem>> Upload(
        IFormFile file, ClaimsPrincipal user, AppDbContext db, IImportFileStore files, IImportEventQueue queue,
        AppClock clock, CancellationToken ct)
    {
        if (file.Length == 0 || file.Length > MaxFileBytes)
            return Invalid(ApiText.FileSize);
        if (!Path.GetExtension(file.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            return Invalid(ApiText.CsvOnly);

        var job = new ImportJob
        {
            Id = Guid.NewGuid(),
            FileName = Path.GetFileName(file.FileName),
            Status = ImportStatus.Queued,
            UploadedBy = user.UserName(),
            UploadedAt = clock.Now,
            BlobName = ""
        };
        job.BlobName = $"{clock.Today:yyyy/MM/dd}/{job.Id}.csv";

        // 1) Store the raw file, 2) record the job, 3) publish the event. The worker does the rest.
        await using (var stream = file.OpenReadStream())
        {
            await files.SaveAsync(job.BlobName, stream, ct);
        }
        db.ImportJobs.Add(job);
        await db.SaveChangesAsync(ct);
        // The worker runs outside this request, so the event carries the uploader's language for the row errors.
        await queue.PublishAsync(new ImportFileUploaded(job.Id, job.BlobName, CultureInfo.CurrentUICulture.Name), ct);

        return TypedResults.Accepted($"/api/imports/{job.Id}", ToDto(job, []));
    }

    private static ImportJobDto ToDto(ImportJob j, IReadOnlyList<ImportRowErrorDto> errors) =>
        new(j.Id, j.FileName, j.Status, j.UploadedBy, j.UploadedAt, j.CompletedAt,
            j.TotalRows, j.ImportedRows, j.RejectedRows, j.FailureReason, errors);

    private static ValidationProblem Invalid(string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [message] });
}
