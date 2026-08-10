using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReadyToGoTravel.Consumer.Application;
using ReadyToGoTravel.Consumer.Persistence;
using ReadyToGoTravel.Consumer.Retention;
using ReadyToGoTravel.Retention;

namespace ReadyToGoTravel.Consumer;

public static class ConsumerModule
{
    public static IServiceCollection AddConsumerModule(
        this IServiceCollection services,
        Action<IServiceProvider, DbContextOptionsBuilder> configureDatabase)
    {
        ArgumentNullException.ThrowIfNull(configureDatabase);

        services.TryAddSingleton(TimeProvider.System);
        services.AddDbContext<ConsumerDbContext>(configureDatabase);
        services.AddScoped<IConsumerBookingContext, ConsumerBookingContextResolver>();
        services.AddOptions<RetentionSweepOptions>().BindConfiguration(RetentionSweepOptions.SectionName);
        services.AddScoped<IConsumerRetentionSweepProcessor, ConsumerRetentionSweepProcessor>();
        services.AddHealthChecks()
            .AddDbContextCheck<ConsumerDbContext>("consumer_database", tags: ["ready"]);

        return services;
    }
}
