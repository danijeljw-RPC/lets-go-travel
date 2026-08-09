using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReadyToGoTravel.Booking.Application;
using ReadyToGoTravel.Booking.Idempotency;
using ReadyToGoTravel.Booking.Notifications;
using ReadyToGoTravel.Booking.Payments;
using ReadyToGoTravel.Booking.Persistence;
using ReadyToGoTravel.Booking.Providers;
using ReadyToGoTravel.Booking.Reconciliation;
using ReadyToGoTravel.Booking.SupplierIntegrations.LiteApi;
using ReadyToGoTravel.Booking.Webhooks;
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
        services.AddScoped<BookingHistoryService>();
        services.AddScoped<IIdempotencyService, IdempotencyService>();
        services.AddScoped<ReconciliationScheduler>();
        services.AddScoped<IReconciliationScheduler>(provider =>
            provider.GetRequiredService<ReconciliationScheduler>());
        services.AddScoped<IReconciliationWorkProcessor, BookingReconciliationProcessor>();
        services.AddScoped<IWebhookInboxWriter, WebhookInboxService>();
        services.AddScoped<IWebhookInboxProcessor, WebhookInboxProcessor>();
        services.TryAddSingleton<ICustomerNotificationSender, DisabledCustomerNotificationSender>();
        services.AddScoped<INotificationOutboxProcessor, NotificationOutboxProcessor>();
        services.AddOptions<LiteApiWebhookOptions>()
            .Validate(options => !options.Enabled || options.CurrentSecret?.Length >= 32,
                "An enabled LiteAPI webhook requires a current secret of at least 32 characters.")
            .Validate(options => options.PreviousSecret is null || options.PreviousSecret.Length >= 32,
                "The previous LiteAPI webhook secret must be at least 32 characters when configured.")
            .Validate(options => options.Environment is "Sandbox" or "Production",
                "The LiteAPI webhook environment must be Sandbox or Production.")
            .Validate(options => options.MaximumBodyBytes is > 0 and <= 1_048_576,
                "The LiteAPI webhook body limit must be between 1 byte and 1 MiB.")
            .ValidateOnStart();
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
