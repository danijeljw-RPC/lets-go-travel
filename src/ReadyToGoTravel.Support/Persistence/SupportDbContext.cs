using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Support.Domain;
using ReadyToGoTravel.Support.Notifications;

namespace ReadyToGoTravel.Support.Persistence;

internal sealed class SupportDbContext(DbContextOptions<SupportDbContext> options) : DbContext(options)
{
    public DbSet<SupportTicket> Tickets => Set<SupportTicket>();

    public DbSet<SupportTicketMessage> TicketMessages => Set<SupportTicketMessage>();

    public DbSet<SupportAuditEvent> AuditEvents => Set<SupportAuditEvent>();

    public DbSet<SupportGuestAccessToken> GuestAccessTokens => Set<SupportGuestAccessToken>();

    public DbSet<SupportAttachment> Attachments => Set<SupportAttachment>();

    public DbSet<AttachmentScanWork> AttachmentScanWork => Set<AttachmentScanWork>();

    public DbSet<SupportNotificationOutboxItem> SupportNotificationOutbox => Set<SupportNotificationOutboxItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("support");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SupportDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RejectTicketMessageMutation();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        RejectTicketMessageMutation();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void RejectTicketMessageMutation()
    {
        if (ChangeTracker.Entries<SupportTicketMessage>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Support ticket messages are append-only.");
        }
    }
}
