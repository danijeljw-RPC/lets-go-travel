using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ReadyToGoTravel.BuildingBlocks.Hosting;

public static partial class WorkerHealthPublishing
{
    public static IServiceCollection AddWorkerHealthPublishing(this IServiceCollection services)
    {
        services.AddHealthChecks();
        services.AddSingleton<IHealthCheckPublisher, LoggingHealthCheckPublisher>();
        services.Configure<HealthCheckPublisherOptions>(options =>
        {
            options.Delay = TimeSpan.Zero;
            options.Period = TimeSpan.FromSeconds(30);
        });

        return services;
    }

    private sealed partial class LoggingHealthCheckPublisher(
        ILogger<LoggingHealthCheckPublisher> logger) : IHealthCheckPublisher
    {
        public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LogHealth(logger, report.Status);
            return Task.CompletedTask;
        }

        [LoggerMessage(Level = LogLevel.Information, Message = "Worker health status: {Status}.")]
        private static partial void LogHealth(ILogger logger, HealthStatus status);
    }
}
