using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReadyToGoTravel.Booking.Idempotency;
using ReadyToGoTravel.Booking.Persistence;

namespace ReadyToGoTravel.Booking;

public static class BookingModule
{
    public static IServiceCollection AddBookingModule(
        this IServiceCollection services,
        Action<IServiceProvider, DbContextOptionsBuilder> configureDatabase)
    {
        ArgumentNullException.ThrowIfNull(configureDatabase);

        services.TryAddSingleton(TimeProvider.System);
        services.AddDbContext<BookingDbContext>(configureDatabase);
        services.AddScoped<IIdempotencyService, IdempotencyService>();
        services.AddHealthChecks()
            .AddDbContextCheck<BookingDbContext>("booking_database", tags: ["ready"]);

        return services;
    }
}
