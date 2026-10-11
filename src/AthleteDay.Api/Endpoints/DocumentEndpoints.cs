using System.Security.Claims;
using System.Security.Cryptography;
using AthleteDay.Api.Audit;
using AthleteDay.Api.Auth;
using AthleteDay.Api.Data;
using AthleteDay.Api.Documents;
using AthleteDay.Api.Domain;
using AthleteDay.Api.Infrastructure;
using AthleteDay.Api.Resources;
using AthleteDay.Contracts;
using AthleteDay.Core;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AthleteDay.Api.Endpoints;

/// <summary>
/// Athlete documents (spec v1.9, ADR-15): files in Blob Storage, metadata in SQL, need-to-know per type. A document of
/// a type the role may not see answers 404, so its existence does not leak. Every successful call is audited.
/// </summary>
public static class DocumentEndpoints
{
    public static RouteGroupBuilder MapDocumentEndpoints(this RouteGroupBuilder api)
    {
        var documents = api.MapGroup("/athletes/{athleteId:int}/documents")
            .WithTags("Documents")
            .RequireAuthorization(Policies.Documents)
            .Audited("Document");

        documents.MapGet("/", List)
            .WithSummary("The athlete's documents of the types the caller may see, newest first");

        // Header-based auth (no cookies) means no CSRF risk, so the antiforgery check is not needed here.
        documents.MapPost("/", Upload)
            .DisableAntiforgery()
            .WithSummary("Upload a PDF, JPEG or PNG (up to 10 MB) of a type the caller may see");

        documents.MapGet("/{documentId:int}/content", Download)
            .WithSummary("Download the file as an attachment");

        documents.MapDelete("/{documentId:int}", Delete)
            .WithSummary("Delete the document and its file");

        return api;
    }

    private static async Task<Results<Ok<List<DocumentDto>>, NotFound>> List(
        int athleteId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (!await db.Athletes.AnyAsync(a => a.Id == athleteId, ct)) return TypedResults.NotFound();

        var visible = DocumentAccess.VisibleTypes(user.RoleName());
        var documents = await db.AthleteDocuments.AsNoTracking()
            .Where(d => d.AthleteId == athleteId && visible.Contains(d.Type))
            .OrderByDescending(d => d.UploadedAt).ThenByDescending(d => d.Id)
            .ToListAsync(ct);
        return TypedResults.Ok(documents.Select(ToDto).ToList());
    }

    private static async Task<Results<Created<DocumentDto>, ValidationProblem, ForbidHttpResult, NotFound>> Upload(
        int athleteId, IFormFile? file, [FromForm] string? type, [FromForm] string? title, ClaimsPrincipal user,
        AppDbContext db, IDocumentStore store, AppClock clock, CancellationToken ct)
    {
        if (!Enum.TryParse<DocumentType>(type, ignoreCase: false, out var documentType) || !Enum.IsDefined(documentType))
            return Invalid("type", ApiText.DocumentType);
        if (!DocumentAccess.CanSee(user.RoleName(), documentType))
            return TypedResults.Forbid();
        if (!await db.Athletes.AnyAsync(a => a.Id == athleteId, ct))
            return TypedResults.NotFound();

        var cleanTitle = title?.Trim() ?? "";
        if (cleanTitle.Length is 0 or > DocumentFiles.MaxTitleLength)
            return Invalid("title", ApiText.DocumentTitle);
        if (file is null || !DocumentFiles.IsValidSize(file.Length))
            return Invalid("file", ApiText.DocumentFile);

        // At most 10 MB, so reading it into memory is fine; the bytes decide the type, not the name.
        using var content = new MemoryStream((int)file.Length);
        await file.CopyToAsync(content, ct);
        var bytes = content.GetBuffer().AsSpan(0, (int)content.Length);
        if (DocumentFiles.DetectContentType(bytes[..Math.Min(bytes.Length, DocumentFiles.SignatureLength)]) is not { } contentType)
            return Invalid("file", ApiText.DocumentFile);

        var document = new AthleteDocument
        {
            AthleteId = athleteId,
            Type = documentType,
            Title = cleanTitle,
            FileName = FileName(file.FileName),
            ContentType = contentType,
            SizeBytes = content.Length,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
            BlobName = Guid.NewGuid().ToString("N"),
            UploadedBy = user.UserName(),
            UploadedAt = clock.Now
        };

        content.Position = 0;
        await store.SaveAsync(document.BlobName, content, contentType, ct);
        try
        {
            db.AthleteDocuments.Add(document);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // No metadata, no file: never leave an orphan behind.
            await store.DeleteAsync(document.BlobName, CancellationToken.None);
            throw;
        }

        return TypedResults.Created($"/api/athletes/{athleteId}/documents/{document.Id}/content", ToDto(document));
    }

    private static async Task<Results<FileStreamHttpResult, NotFound>> Download(
        int athleteId, int documentId, ClaimsPrincipal user, HttpContext http, AppDbContext db, IDocumentStore store,
        CancellationToken ct)
    {
        var document = await FindVisibleAsync(db, athleteId, documentId, user, ct);
        if (document is null || await store.OpenReadAsync(document.BlobName, ct) is not { } content)
            return TypedResults.NotFound();

        // Served as an attachment of its recognised type; the browser must not guess another one.
        http.Response.Headers.XContentTypeOptions = "nosniff";
        return TypedResults.File(content, document.ContentType, document.FileName);
    }

    private static async Task<Results<NoContent, NotFound>> Delete(
        int athleteId, int documentId, ClaimsPrincipal user, AppDbContext db, IDocumentStore store, CancellationToken ct)
    {
        var document = await FindVisibleAsync(db, athleteId, documentId, user, ct);
        if (document is null) return TypedResults.NotFound();

        db.AthleteDocuments.Remove(document);
        await db.SaveChangesAsync(ct);
        await store.DeleteAsync(document.BlobName, ct);
        return TypedResults.NoContent();
    }

    /// <summary>The document, if it belongs to this athlete and the caller may see its type; otherwise null (404).</summary>
    private static async Task<AthleteDocument?> FindVisibleAsync(
        AppDbContext db, int athleteId, int documentId, ClaimsPrincipal user, CancellationToken ct)
    {
        var document = await db.AthleteDocuments.SingleOrDefaultAsync(d => d.Id == documentId && d.AthleteId == athleteId, ct);
        return document is not null && DocumentAccess.CanSee(user.RoleName(), document.Type) ? document : null;
    }

    private static string FileName(string raw)
    {
        var name = Path.GetFileName(raw.Replace('\\', '/')).Trim();
        if (name.Length == 0) return "document";
        return name.Length <= 255 ? name : name[^255..];
    }

    private static DocumentDto ToDto(AthleteDocument d) =>
        new(d.Id, d.AthleteId, d.Type, d.Title, d.FileName, d.ContentType, d.SizeBytes, d.Sha256, d.UploadedBy, d.UploadedAt);

    private static ValidationProblem Invalid(string field, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}
