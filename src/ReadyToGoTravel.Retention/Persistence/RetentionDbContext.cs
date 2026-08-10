using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Retention.Domain;

namespace ReadyToGoTravel.Retention.Persistence;

public sealed class RetentionDbContext(DbContextOptions<RetentionDbContext> options) : DbContext(options)
{
    public DbSet<LegalHold> LegalHolds => Set<LegalHold>();

    public DbSet<LegalHoldScope> LegalHoldScopes => Set<LegalHoldScope>();

    public DbSet<LegalHoldAuditEvent> LegalHoldAuditEvents => Set<LegalHoldAuditEvent>();

    public DbSet<RetentionDeletionReceipt> RetentionDeletionReceipts => Set<RetentionDeletionReceipt>();

    public DbSet<RetentionOperationalCase> RetentionOperationalCases => Set<RetentionOperationalCase>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("retention");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RetentionDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RejectLegalHoldAuditEventMutation();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        RejectLegalHoldAuditEventMutation();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void RejectLegalHoldAuditEventMutation()
    {
        if (ChangeTracker.Entries<LegalHoldAuditEvent>().Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Legal hold audit events are append-only.");
        }

        if (ChangeTracker.Entries<RetentionDeletionReceipt>().Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Retention deletion receipts are append-only.");
        }
    }
}
