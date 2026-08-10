using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Booking.Bookings;
using ReadyToGoTravel.Booking.Checkout;
using ReadyToGoTravel.Booking.Idempotency;
using ReadyToGoTravel.Booking.Notifications;
using ReadyToGoTravel.Booking.Payments;
using ReadyToGoTravel.Booking.Reconciliation;
using ReadyToGoTravel.Booking.Webhooks;

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

    public DbSet<BookingVersion> BookingVersions => Set<BookingVersion>();

    public DbSet<ReconciliationWork> ReconciliationWork => Set<ReconciliationWork>();

    public DbSet<ReconciliationAttempt> ReconciliationAttempts => Set<ReconciliationAttempt>();

    public DbSet<OperationalCase> OperationalCases => Set<OperationalCase>();

    public DbSet<WebhookInboxItem> WebhookInbox => Set<WebhookInboxItem>();

    public DbSet<NotificationOutboxItem> NotificationOutbox => Set<NotificationOutboxItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("booking");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BookingDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RejectBookingVersionMutation();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        RejectBookingVersionMutation();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void RejectBookingVersionMutation()
    {
        if (ChangeTracker.Entries<BookingVersion>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Canonical booking versions are append-only.");
        }
    }
}
