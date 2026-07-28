using Microsoft.Extensions.Hosting;

namespace ReadyToGoTravel.BuildingBlocks.Hosting;

public abstract class CooperativeWorker : BackgroundService
{
    private readonly TimeSpan idleDelay;
    private readonly TimeProvider timeProvider;

    protected CooperativeWorker(TimeProvider timeProvider, TimeSpan idleDelay)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(idleDelay, TimeSpan.Zero);

        this.timeProvider = timeProvider;
        this.idleDelay = idleDelay;
    }

    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ExecuteCycleAsync(stoppingToken);

            try
            {
                await Task.Delay(idleDelay, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    protected abstract Task ExecuteCycleAsync(CancellationToken cancellationToken);
}
