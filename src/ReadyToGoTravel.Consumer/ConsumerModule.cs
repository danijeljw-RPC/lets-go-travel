using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReadyToGoTravel.Consumer.Persistence;

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
        services.AddHealthChecks()
            .AddDbContextCheck<ConsumerDbContext>("consumer_database", tags: ["ready"]);

        return services;
    }
}
