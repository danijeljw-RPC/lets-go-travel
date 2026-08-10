using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReadyToGoTravel.Retention.Application;
using ReadyToGoTravel.Retention.Persistence;

namespace ReadyToGoTravel.Retention;

public static class RetentionModule
{
    public static IServiceCollection AddRetentionModule(
        this IServiceCollection services,
        Action<IServiceProvider, DbContextOptionsBuilder> configureDatabase)
    {
        ArgumentNullException.ThrowIfNull(configureDatabase);

        services.TryAddSingleton(TimeProvider.System);
        services.AddDbContext<RetentionDbContext>(configureDatabase);
        services.AddScoped<LegalHoldService>();
        services.AddScoped<ILegalHoldGuard>(provider => provider.GetRequiredService<LegalHoldService>());
        services.AddScoped<IRetentionReceiptRecorder>(provider => provider.GetRequiredService<LegalHoldService>());

        services.AddHealthChecks()
            .AddDbContextCheck<RetentionDbContext>("retention_database", tags: ["ready"]);

        return services;
    }
}
