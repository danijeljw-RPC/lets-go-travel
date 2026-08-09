using Microsoft.Extensions.Options;
using ReadyToGoTravel.Booking.Checkout;
using ReadyToGoTravel.Booking.Notifications;
using ReadyToGoTravel.Booking.Providers;
using ReadyToGoTravel.Booking.Reconciliation;
using ReadyToGoTravel.Booking.Webhooks;
using ReadyToGoTravel.BuildingBlocks.Hosting;

namespace ReadyToGoTravel.Worker;

public sealed partial class Worker(
    ILogger<Worker> logger,
    IOptions<CooperativeWorkerOptions> options,
    IServiceScopeFactory scopeFactory)
    : CooperativeWorker(TimeProvider.System, options.Value.IdleDelay)
{
    protected override async Task ExecuteCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var scope = scopeFactory.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var didWork = await services.GetRequiredService<IWebhookInboxProcessor>()
                .ProcessNextAsync(Environment.MachineName, cancellationToken);
            if (services.GetService<IBookingProvider>() is not null)
            {
                didWork |= await services.GetRequiredService<IReconciliationWorkProcessor>()
                    .ProcessNextAsync(CheckoutProduct.Hotel, Environment.MachineName, cancellationToken);
            }

            didWork |= await services.GetRequiredService<INotificationOutboxProcessor>()
                .ProcessNextAsync(Environment.MachineName, cancellationToken);
            if (!didWork)
            {
                LogReady(logger);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogCycleFailure(logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "General worker is ready for durable work.")]
    private static partial void LogReady(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "General durable-work cycle failed; the worker will retry after its idle delay.")]
    private static partial void LogCycleFailure(ILogger logger, Exception exception);
}
