using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReadyToGoTravel.Retention.Domain;

namespace ReadyToGoTravel.Retention.Persistence;

internal sealed class LegalHoldConfiguration : IEntityTypeConfiguration<LegalHold>
{
    public void Configure(EntityTypeBuilder<LegalHold> builder)
    {
        builder.ToTable("legal_holds");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.MatterReference).HasColumnName("matter_reference").HasMaxLength(200).IsRequired();
        builder.Property(value => value.Reason).HasColumnName("reason").HasMaxLength(2000).IsRequired();
        builder.Property(value => value.AuthorizedOwnerSubject).HasColumnName("authorized_owner_subject").HasMaxLength(200).IsRequired();
        builder.Property(value => value.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(value => value.ReviewByUtc).HasColumnName("review_by_utc").IsRequired();
        builder.Property(value => value.ReleasedAtUtc).HasColumnName("released_at_utc");
        builder.Property(value => value.ReleasedBySubject).HasColumnName("released_by_subject").HasMaxLength(200);
        builder.Property(value => value.ReleaseReason).HasColumnName("release_reason").HasMaxLength(2000);
        builder.HasIndex(value => value.ReleasedAtUtc);

        builder.Navigation(value => value.Scopes).HasField("scopes").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(value => value.Scopes).WithOne().HasForeignKey(value => value.LegalHoldId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class LegalHoldScopeConfiguration : IEntityTypeConfiguration<LegalHoldScope>
{
    public void Configure(EntityTypeBuilder<LegalHoldScope> builder)
    {
        builder.ToTable("legal_hold_scopes", table => table.HasCheckConstraint(
            "ck_legal_hold_scopes_exactly_one_subject",
            "(CASE WHEN customer_id IS NOT NULL THEN 1 ELSE 0 END) + " +
            "(CASE WHEN component_booking_id IS NOT NULL THEN 1 ELSE 0 END) + " +
            "(CASE WHEN support_ticket_id IS NOT NULL THEN 1 ELSE 0 END) = 1"));
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.LegalHoldId).HasColumnName("legal_hold_id");
        builder.Property(value => value.RecordClass).HasColumnName("record_class").HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(value => value.CustomerId).HasColumnName("customer_id");
        builder.Property(value => value.ComponentBookingId).HasColumnName("component_booking_id");
        builder.Property(value => value.SupportTicketId).HasColumnName("support_ticket_id");
        builder.HasIndex(value => new { value.RecordClass, value.CustomerId });
        builder.HasIndex(value => new { value.RecordClass, value.ComponentBookingId });
        builder.HasIndex(value => new { value.RecordClass, value.SupportTicketId });
    }
}

internal sealed class LegalHoldAuditEventConfiguration : IEntityTypeConfiguration<LegalHoldAuditEvent>
{
    public void Configure(EntityTypeBuilder<LegalHoldAuditEvent> builder)
    {
        builder.ToTable("legal_hold_audit_events");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.LegalHoldId).HasColumnName("legal_hold_id");
        builder.Property(value => value.EventType).HasColumnName("event_type").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(value => value.Detail).HasColumnName("detail").HasMaxLength(1000).IsRequired();
        builder.Property(value => value.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(value => value.ActorSubject).HasColumnName("actor_subject").HasMaxLength(200);
        builder.HasIndex(value => value.LegalHoldId);
        builder.HasOne<LegalHold>().WithMany().HasForeignKey(value => value.LegalHoldId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RetentionDeletionReceiptConfiguration : IEntityTypeConfiguration<RetentionDeletionReceipt>
{
    public void Configure(EntityTypeBuilder<RetentionDeletionReceipt> builder)
    {
        builder.ToTable("retention_deletion_receipts");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.RecordClass).HasColumnName("record_class").HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(value => value.PolicyVersion).HasColumnName("policy_version").IsRequired();
        builder.Property(value => value.Action).HasColumnName("action").HasMaxLength(40).IsRequired();
        builder.Property(value => value.SuccessCount).HasColumnName("success_count").IsRequired();
        builder.Property(value => value.FailureCount).HasColumnName("failure_count").IsRequired();
        builder.Property(value => value.CompletedAtUtc).HasColumnName("completed_at_utc").IsRequired();
        builder.Property(value => value.FailureSummary).HasColumnName("failure_summary").HasMaxLength(1000);
        builder.HasIndex(value => new { value.RecordClass, value.CompletedAtUtc });
    }
}

internal sealed class RetentionOperationalCaseConfiguration : IEntityTypeConfiguration<RetentionOperationalCase>
{
    public void Configure(EntityTypeBuilder<RetentionOperationalCase> builder)
    {
        builder.ToTable("retention_operational_cases");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.RecordClass).HasColumnName("record_class").HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(value => value.Scope).HasColumnName("scope").HasMaxLength(80).IsRequired();
        builder.Property(value => value.DedupeKey).HasColumnName("dedupe_key").HasMaxLength(160).IsRequired();
        builder.Property(value => value.Reason).HasColumnName("reason").HasMaxLength(200).IsRequired();
        builder.Property(value => value.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.HasIndex(value => value.DedupeKey).IsUnique();
    }
}
