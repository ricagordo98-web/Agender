using Agender.Infrastructure.Services;

namespace Agender.Web.Meals;

public sealed class RecipeCatalogWorker(RecipeCatalogSync sync, ILogger<RecipeCatalogWorker> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan SyncCheckInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan PoliteDelay = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        DateTime nextSyncCheck = DateTime.MinValue;

        try
        {
            await Task.Delay(StartupDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                TimeSpan delay;

                try
                {
                    if (DateTime.UtcNow >= nextSyncCheck)
                    {
                        await sync.SyncDueSourcesAsync(stoppingToken);
                        nextSyncCheck = DateTime.UtcNow + SyncCheckInterval;
                    }

                    delay = await sync.EnrichNextAsync(stoppingToken) ? PoliteDelay : IdleDelay;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Recipe catalog sync failed.");
                    delay = ErrorDelay;
                }

                await Task.Delay(delay, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}

public sealed class MealMessageWorker(MealMessageDispatcher dispatcher, ILogger<MealMessageWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(Interval);

        try
        {
            do
            {
                try
                {
                    await dispatcher.DispatchDueAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Meal message dispatch failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
        }
    }
}
