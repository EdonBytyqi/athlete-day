namespace AthleteDay.Api.Imports;

/// <summary>
/// Background consumer for <see cref="ImportFileUploaded"/> events. It runs inside the API for the demo;
/// because it only depends on the queue, it can move to an Azure Function or a separate container unchanged.
/// </summary>
public sealed class ImportWorker(IImportEventQueue queue, IServiceScopeFactory scopes, ILogger<ImportWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var received = await queue.ReceiveAsync(stoppingToken);
                if (received is null) continue;

                await using (var scope = scopes.CreateAsyncScope())
                {
                    var processor = scope.ServiceProvider.GetRequiredService<ImportProcessor>();
                    await processor.ProcessAsync(received.Event, stoppingToken);
                }

                await received.CompleteAsync();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // The message stays on the queue and becomes visible again after the visibility timeout.
                logger.LogError(ex, "Import worker failed to handle an event; it will be retried");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
