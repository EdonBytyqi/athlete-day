using System.Globalization;
using AthleteDay.Api.Data;
using AthleteDay.Api.Domain;
using AthleteDay.Api.Infrastructure;
using AthleteDay.Api.Realtime;
using AthleteDay.Contracts;
using AthleteDay.Core;
using Microsoft.EntityFrameworkCore;

namespace AthleteDay.Api.Imports;

/// <summary>Validates one uploaded device file and imports the good rows.</summary>
public sealed class ImportProcessor(
    AppDbContext db,
    IImportFileStore files,
    IDashboardNotifier notifier,
    AppClock clock,
    ILogger<ImportProcessor> logger)
{
    public async Task ProcessAsync(ImportFileUploaded evt, CancellationToken ct)
    {
        var job = await db.ImportJobs.SingleOrDefaultAsync(j => j.Id == evt.JobId, ct);

        // Queues deliver at least once. A job that is no longer queued has been handled already.
        if (job is null || job.Status != ImportStatus.Queued)
        {
            logger.LogInformation("Skipping import event for job {JobId}: already handled or unknown", evt.JobId);
            return;
        }

        job.Status = ImportStatus.Processing;
        await db.SaveChangesAsync(ct);

        try
        {
            // Row errors are stored as text, in the uploader's language (ADR-09). The change only lasts for this call.
            CultureInfo.CurrentUICulture = UploaderCulture(evt.Culture);
            var csv = await files.ReadTextAsync(evt.BlobName, ct);
            var athletes = await db.Athletes.AsNoTracking().ToDictionaryAsync(a => a.Code, a => a.Id, ct);
            var result = DeviceCsvValidator.Validate(csv, athletes.Keys.ToHashSet(), clock.Today);

            db.DeviceMeasurements.AddRange(result.ValidRows.Select(r => new DeviceMeasurement
            {
                AthleteId = athletes[r.AthleteCode],
                Date = r.Date,
                TestType = r.TestType,
                Value = r.Value,
                Unit = r.Unit,
                ImportJobId = job.Id
            }));

            job.Errors.AddRange(result.Errors.Select(e => new ImportRowError
            {
                RowNumber = e.RowNumber,
                Column = e.Column,
                Reason = Truncate(e.Reason, 500),
                RawLine = Truncate(e.RawLine, 1000)
            }));

            job.TotalRows = result.TotalRows;
            job.ImportedRows = result.ValidRows.Count;
            job.RejectedRows = result.Errors.Count(e => e.RowNumber > 1);
            job.Status = result.HasFileError ? ImportStatus.Failed : ImportStatus.Completed;
            job.FailureReason = result.HasFileError ? result.Errors.First(e => e.RowNumber == 1).Reason : null;
            job.CompletedAt = clock.Now;
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Import {JobId}: {Imported} imported, {Rejected} rejected",
                job.Id, job.ImportedRows, job.RejectedRows);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Import {JobId} failed", job.Id);
            db.ChangeTracker.Clear();
            await db.ImportJobs.Where(j => j.Id == job.Id).ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, ImportStatus.Failed)
                .SetProperty(j => j.FailureReason, "Unexpected error while processing the file.")
                .SetProperty(j => j.CompletedAt, clock.Now), ct);
        }

        await notifier.DataChangedAsync("import", ct);
    }

    private static CultureInfo UploaderCulture(string? name)
    {
        try { return name is { Length: > 0 } ? CultureInfo.GetCultureInfo(name) : CultureInfo.InvariantCulture; }
        catch (CultureNotFoundException) { return CultureInfo.InvariantCulture; }
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
