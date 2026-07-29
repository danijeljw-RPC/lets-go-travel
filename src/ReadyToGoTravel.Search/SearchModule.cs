using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReadyToGoTravel.Search.Capabilities;
using ReadyToGoTravel.Search.Checkout;
using ReadyToGoTravel.Search.Providers;
using ReadyToGoTravel.Search.SupplierIntegrations.LiteApi;

namespace ReadyToGoTravel.Search;

public static class SearchModule
{
    public static IServiceCollection AddSearchModule(
        this IServiceCollection services,
        SearchEnvironment environment,
        bool enableFixtures)
    {
        if (environment == SearchEnvironment.Production && enableFixtures)
        {
            throw new InvalidOperationException("Sanitized search fixtures cannot be enabled in Production.");
        }

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(new SearchRuntime(environment, enableFixtures));
        services.AddSingleton<CapabilityRegistry>(_ => CapabilityRegistry.CreateDefaults());
        services.AddSingleton<ICapabilityRegistry>(provider => provider.GetRequiredService<CapabilityRegistry>());

        if (enableFixtures)
        {
            services.AddSingleton<LiteApiFixtureSearchProvider>();
            services.AddSingleton<LiteApiFixtureOfferResolver>();
            services.AddSingleton<IHotelSearchProvider>(provider =>
                provider.GetRequiredService<LiteApiFixtureSearchProvider>());
            services.AddSingleton<IFlightSearchProvider>(provider =>
                provider.GetRequiredService<LiteApiFixtureSearchProvider>());
            services.AddSingleton<ICheckoutOfferResolver>(provider =>
                provider.GetRequiredService<LiteApiFixtureOfferResolver>());
        }

        return services;
    }
}

internal sealed record SearchRuntime(SearchEnvironment Environment, bool FixturesEnabled);
