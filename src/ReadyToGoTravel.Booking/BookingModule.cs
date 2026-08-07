using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReadyToGoTravel.Booking.Application;
using ReadyToGoTravel.Booking.Idempotency;
using ReadyToGoTravel.Booking.Payments;
using ReadyToGoTravel.Booking.Persistence;
using ReadyToGoTravel.Booking.Providers;
using ReadyToGoTravel.Booking.SupplierIntegrations.LiteApi;
using ReadyToGoTravel.Search.Capabilities;

namespace ReadyToGoTravel.Booking;

public static class BookingModule
{
    public static IServiceCollection AddBookingModule(
        this IServiceCollection services,
        Action<IServiceProvider, DbContextOptionsBuilder> configureDatabase,
        SearchEnvironment environment = SearchEnvironment.Production,
        bool enableFixtures = false)
    {
        ArgumentNullException.ThrowIfNull(configureDatabase);
        if (environment == SearchEnvironment.Production && enableFixtures)
        {
            throw new InvalidOperationException("Sanitized booking fixtures cannot be enabled in Production.");
        }

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(new BookingRuntime(environment));
        services.AddDbContext<BookingDbContext>(configureDatabase);
        services.AddScoped<CheckoutService>();
        services.AddScoped<IIdempotencyService, IdempotencyService>();
        services.AddHealthChecks()
            .AddDbContextCheck<BookingDbContext>("booking_database", tags: ["ready"]);

        if (environment == SearchEnvironment.Sandbox && enableFixtures)
        {
            services.AddSingleton<LiteApiFixturePaymentProvider>();
            services.AddSingleton<ICustomerPaymentProvider>(provider =>
                provider.GetRequiredService<LiteApiFixturePaymentProvider>());
            services.AddSingleton<LiteApiFixtureBookingProvider>();
            services.AddSingleton<IBookingProvider>(provider =>
                provider.GetRequiredService<LiteApiFixtureBookingProvider>());
            services.AddSingleton<ISupplierSettlementProvider, LiteApiFixtureSettlementProvider>();
            services.AddScoped<IPaymentService, PaymentService>();
        }

        return services;
    }
}

internal sealed record BookingRuntime(SearchEnvironment Environment);
