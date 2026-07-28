using ReadyToGoTravel.BuildingBlocks.Hosting;

namespace ReadyToGoTravel.Worker;

public sealed partial class Worker(ILogger<Worker> logger)
    : CooperativeWorker(TimeProvider.System, TimeSpan.FromMinutes(1))
{
    protected override Task ExecuteCycleAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LogReady(logger);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "General worker is ready for durable work.")]
    private static partial void LogReady(ILogger logger);
}
