using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;

namespace AthleteDay.Api.Imports;

/// <summary>The event published when a device file has landed in storage.</summary>
/// <param name="Culture">The uploader's UI culture (e.g. "de-CH"), so row errors are written in their language. Null in older events.</param>
public sealed record ImportFileUploaded(Guid JobId, string BlobName, string? Culture = null);

/// <summary>A received event. Call <see cref="CompleteAsync"/> once it is handled, or it is delivered again.</summary>
public sealed record ReceivedImportEvent(ImportFileUploaded Event, Func<Task> CompleteAsync);

public interface IImportFileStore
{
    Task SaveAsync(string blobName, Stream content, CancellationToken ct);
    Task<string> ReadTextAsync(string blobName, CancellationToken ct);
}

public interface IImportEventQueue
{
    Task PublishAsync(ImportFileUploaded evt, CancellationToken ct);
    Task<ReceivedImportEvent?> ReceiveAsync(CancellationToken ct);
}

public sealed class StorageOptions
{
    public const string Section = "Storage";

    /// <summary>Azure Storage connection string. Locally "UseDevelopmentStorage=true" (Azurite). Empty = in-memory.</summary>
    public string? ConnectionString { get; set; }
    public string Container { get; set; } = "device-imports";
    public string Queue { get; set; } = "device-imports";
    public string DocumentContainer { get; set; } = "athlete-documents";
}

// ---- Azure Blob Storage + Storage Queue (Azurite locally) ----------------------------------------------

public sealed class BlobImportFileStore(BlobContainerClient container) : IImportFileStore
{
    public async Task SaveAsync(string blobName, Stream content, CancellationToken ct)
    {
        await container.CreateIfNotExistsAsync(cancellationToken: ct);
        await container.GetBlobClient(blobName).UploadAsync(content, overwrite: true, ct);
    }

    public async Task<string> ReadTextAsync(string blobName, CancellationToken ct)
    {
        var download = await container.GetBlobClient(blobName).DownloadContentAsync(ct);
        return download.Value.Content.ToString();
    }
}

/// <summary>
/// In Azure, Event Grid can route the BlobCreated event into this queue. Here the API publishes the event
/// itself, because Azurite has no Event Grid. The consumer is the same either way.
/// </summary>
public sealed class StorageQueueImportEventQueue(QueueClient queue) : IImportEventQueue
{
    private bool _created;

    public async Task PublishAsync(ImportFileUploaded evt, CancellationToken ct)
    {
        await EnsureCreatedAsync(ct);
        await queue.SendMessageAsync(JsonSerializer.Serialize(evt), ct);
    }

    public async Task<ReceivedImportEvent?> ReceiveAsync(CancellationToken ct)
    {
        await EnsureCreatedAsync(ct);
        var response = await queue.ReceiveMessageAsync(TimeSpan.FromMinutes(2), ct);
        var message = response.Value;
        if (message is null)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
            return null;
        }

        var evt = JsonSerializer.Deserialize<ImportFileUploaded>(message.MessageText)!;
        return new ReceivedImportEvent(evt, () => queue.DeleteMessageAsync(message.MessageId, message.PopReceipt, ct));
    }

    private async Task EnsureCreatedAsync(CancellationToken ct)
    {
        if (_created) return;
        await queue.CreateIfNotExistsAsync(cancellationToken: ct);
        _created = true;
    }
}

// ---- In-memory adapters: for tests and for running without Azurite ------------------------------------

public sealed class InMemoryImportFileStore : IImportFileStore
{
    private readonly ConcurrentDictionary<string, string> _files = new();

    public async Task SaveAsync(string blobName, Stream content, CancellationToken ct)
    {
        using var reader = new StreamReader(content);
        _files[blobName] = await reader.ReadToEndAsync(ct);
    }

    public Task<string> ReadTextAsync(string blobName, CancellationToken ct) => Task.FromResult(_files[blobName]);
}

public sealed class InMemoryImportEventQueue : IImportEventQueue
{
    private readonly Channel<ImportFileUploaded> _channel = Channel.CreateUnbounded<ImportFileUploaded>();

    public Task PublishAsync(ImportFileUploaded evt, CancellationToken ct) => _channel.Writer.WriteAsync(evt, ct).AsTask();

    public async Task<ReceivedImportEvent?> ReceiveAsync(CancellationToken ct)
    {
        var evt = await _channel.Reader.ReadAsync(ct);
        return new ReceivedImportEvent(evt, () => Task.CompletedTask);
    }
}
