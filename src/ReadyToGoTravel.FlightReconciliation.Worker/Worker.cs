using Microsoft.Extensions.Options;
using ReadyToGoTravel.BuildingBlocks.Hosting;

namespace ReadyToGoTravel.FlightReconciliation.Worker;

public sealed partial class Worker(
    ILogger<Worker> logger,
    IOptions<CooperativeWorkerOptions> options)
    : CooperativeWorker(TimeProvider.System, options.Value.IdleDelay)
{
    protected override Task ExecuteCycleAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LogReady(logger);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Flight reconciliation worker is ready for scheduled checks.")]
    private static partial void LogReady(ILogger logger);
}
