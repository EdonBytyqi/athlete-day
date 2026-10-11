using System.Collections.Concurrent;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace AthleteDay.Api.Documents;

/// <summary>Where athlete document files live (ADR-15). Names are random; the metadata is in SQL.</summary>
public interface IDocumentStore
{
    Task SaveAsync(string name, Stream content, string contentType, CancellationToken ct);

    /// <summary>The file's content, or null when there is no such file.</summary>
    Task<Stream?> OpenReadAsync(string name, CancellationToken ct);

    Task DeleteAsync(string name, CancellationToken ct);
}

/// <summary>A private Blob container (Azurite locally). Files are only ever served through the API, never by link.</summary>
public sealed class BlobDocumentStore(BlobContainerClient container) : IDocumentStore
{
    private bool _created;

    public async Task SaveAsync(string name, Stream content, string contentType, CancellationToken ct)
    {
        if (!_created)
        {
            await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
            _created = true;
        }
        await container.GetBlobClient(name).UploadAsync(content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } }, ct);
    }

    public async Task<Stream?> OpenReadAsync(string name, CancellationToken ct)
    {
        try
        {
            return await container.GetBlobClient(name).OpenReadAsync(cancellationToken: ct);
        }
        catch (RequestFailedException e) when (e.Status == StatusCodes.Status404NotFound)
        {
            return null;
        }
    }

    public Task DeleteAsync(string name, CancellationToken ct) =>
        container.GetBlobClient(name).DeleteIfExistsAsync(cancellationToken: ct);
}

/// <summary>For tests and for running without Azurite, like the device import's in-memory store.</summary>
public sealed class InMemoryDocumentStore : IDocumentStore
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new();

    public async Task SaveAsync(string name, Stream content, string contentType, CancellationToken ct)
    {
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, ct);
        _files[name] = copy.ToArray();
    }

    public Task<Stream?> OpenReadAsync(string name, CancellationToken ct) =>
        Task.FromResult<Stream?>(_files.TryGetValue(name, out var bytes) ? new MemoryStream(bytes, writable: false) : null);

    public Task DeleteAsync(string name, CancellationToken ct)
    {
        _files.TryRemove(name, out _);
        return Task.CompletedTask;
    }
}
