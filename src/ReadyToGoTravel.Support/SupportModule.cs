using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReadyToGoTravel.Support.Application;
using ReadyToGoTravel.Support.Guest;
using ReadyToGoTravel.Support.Persistence;

namespace ReadyToGoTravel.Support;

public static class SupportModule
{
    public static IServiceCollection AddSupportModule(
        this IServiceCollection services,
        Action<IServiceProvider, DbContextOptionsBuilder> configureDatabase)
    {
        ArgumentNullException.ThrowIfNull(configureDatabase);

        services.TryAddSingleton(TimeProvider.System);
        services.AddDbContext<SupportDbContext>(configureDatabase);
        services.AddScoped<SupportTicketService>();
        services.AddScoped<IGuestAccessTokenService, GuestAccessTokenService>();
        services.AddHealthChecks()
            .AddDbContextCheck<SupportDbContext>("support_database", tags: ["ready"]);

        return services;
    }
}
