using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Booking.Bookings;
using ReadyToGoTravel.Booking.Checkout;
using ReadyToGoTravel.Booking.Idempotency;
using ReadyToGoTravel.Booking.Payments;

namespace ReadyToGoTravel.Booking.Persistence;

internal sealed class BookingDbContext(DbContextOptions<BookingDbContext> options) : DbContext(options)
{
    public DbSet<CheckoutSession> Checkouts => Set<CheckoutSession>();

    public DbSet<CheckoutRevision> CheckoutRevisions => Set<CheckoutRevision>();

    public DbSet<TravellerSnapshot> TravellerSnapshots => Set<TravellerSnapshot>();

    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();

    public DbSet<ComponentBooking> ComponentBookings => Set<ComponentBooking>();

    public DbSet<BookingRecoveryCase> RecoveryCases => Set<BookingRecoveryCase>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("booking");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BookingDbContext).Assembly);
    }
}
