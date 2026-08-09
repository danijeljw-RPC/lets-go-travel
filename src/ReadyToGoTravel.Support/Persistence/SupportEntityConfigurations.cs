using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReadyToGoTravel.Support.Domain;

namespace ReadyToGoTravel.Support.Persistence;

internal sealed class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
{
    public void Configure(EntityTypeBuilder<SupportTicket> builder)
    {
        builder.ToTable("support_tickets");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.CustomerSubject).HasColumnName("customer_subject").HasMaxLength(128);
        builder.HasIndex(value => value.CustomerSubject);
        builder.Property(value => value.ContactName).HasColumnName("contact_name").HasMaxLength(200).IsRequired();
        builder.Property(value => value.ContactEmail).HasColumnName("contact_email").HasMaxLength(320).IsRequired();
        builder.Property(value => value.Category).HasColumnName("category").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Ignore(value => value.IsUrgent);
        builder.Property(value => value.BookingReference).HasColumnName("booking_reference").HasMaxLength(120);
        builder.Property(value => value.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(value => value.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(value => value.UpdatedAt).HasColumnName("updated_at").IsRequired().IsConcurrencyToken();
        builder.Property(value => value.ClosedAt).HasColumnName("closed_at");
        builder.HasIndex(value => new { value.Status, value.CreatedAt });

        builder.Navigation(value => value.Messages).HasField("messages").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(value => value.Messages).WithOne().HasForeignKey(value => value.TicketId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SupportTicketMessageConfiguration : IEntityTypeConfiguration<SupportTicketMessage>
{
    public void Configure(EntityTypeBuilder<SupportTicketMessage> builder)
    {
        builder.ToTable("support_ticket_messages");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.TicketId).HasColumnName("ticket_id");
        builder.Property(value => value.SequenceNumber).HasColumnName("sequence_number").IsRequired();
        builder.HasIndex(value => new { value.TicketId, value.SequenceNumber }).IsUnique();
        builder.Property(value => value.AuthorType).HasColumnName("author_type").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(value => value.AuthorSubject).HasColumnName("author_subject").HasMaxLength(128);
        builder.Property(value => value.Body).HasColumnName("body").HasMaxLength(4000).IsRequired();
        builder.Property(value => value.CreatedAt).HasColumnName("created_at").IsRequired();
    }
}

internal sealed class SupportAuditEventConfiguration : IEntityTypeConfiguration<SupportAuditEvent>
{
    public void Configure(EntityTypeBuilder<SupportAuditEvent> builder)
    {
        builder.ToTable("support_audit_events");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.TicketId).HasColumnName("ticket_id");
        builder.HasIndex(value => value.TicketId);
        builder.Property(value => value.EventType).HasColumnName("event_type").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(value => value.Detail).HasColumnName("detail").HasMaxLength(400).IsRequired();
        builder.Property(value => value.CreatedAt).HasColumnName("created_at").IsRequired();
    }
}

internal sealed class SupportGuestAccessTokenConfiguration : IEntityTypeConfiguration<SupportGuestAccessToken>
{
    public void Configure(EntityTypeBuilder<SupportGuestAccessToken> builder)
    {
        builder.ToTable("support_guest_access_tokens");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.TicketId).HasColumnName("ticket_id");
        builder.HasIndex(value => value.TicketId);
        builder.Property(value => value.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        builder.HasIndex(value => value.TokenHash).IsUnique();
        builder.Property(value => value.IssuedAt).HasColumnName("issued_at").IsRequired();
        builder.Property(value => value.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(value => value.RevokedAt).HasColumnName("revoked_at");
        builder.Property(value => value.LastUsedAt).HasColumnName("last_used_at");
        builder.Property(value => value.RotatedFromTokenId).HasColumnName("rotated_from_token_id");
        builder.HasOne<SupportGuestAccessToken>().WithMany().HasForeignKey(value => value.RotatedFromTokenId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SupportAttachmentConfiguration : IEntityTypeConfiguration<SupportAttachment>
{
    public void Configure(EntityTypeBuilder<SupportAttachment> builder)
    {
        builder.ToTable("support_attachments");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.TicketId).HasColumnName("ticket_id");
        builder.HasIndex(value => value.TicketId);
        builder.Property(value => value.MessageId).HasColumnName("message_id");
        builder.HasIndex(value => value.MessageId);
        builder.Property(value => value.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(200).IsRequired();
        builder.Property(value => value.ContentType).HasColumnName("content_type").HasMaxLength(80).IsRequired();
        builder.Property(value => value.SizeBytes).HasColumnName("size_bytes").IsRequired();
        builder.Property(value => value.StorageKey).HasColumnName("storage_key").HasMaxLength(300).IsRequired();
        builder.HasIndex(value => value.StorageKey).IsUnique();
        builder.Property(value => value.Sha256Checksum).HasColumnName("sha256_checksum").HasMaxLength(64).IsRequired();
        builder.Property(value => value.UploaderCustomerSubject).HasColumnName("uploader_customer_subject").HasMaxLength(128);
        builder.Property(value => value.UploaderGuestTokenId).HasColumnName("uploader_guest_token_id");
        builder.Property(value => value.ScanStatus).HasColumnName("scan_status").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(value => value.CreatedAt).HasColumnName("created_at").IsRequired();
    }
}

internal sealed class AttachmentScanWorkConfiguration : IEntityTypeConfiguration<AttachmentScanWork>
{
    public void Configure(EntityTypeBuilder<AttachmentScanWork> builder)
    {
        builder.ToTable("attachment_scan_work");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.AttachmentId).HasColumnName("attachment_id");
        builder.HasOne<SupportAttachment>().WithMany().HasForeignKey(value => value.AttachmentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(value => value.AttachmentId).IsUnique();
        builder.Property(value => value.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16).IsRequired().IsConcurrencyToken();
        builder.Property(value => value.Attempts).HasColumnName("attempts").IsRequired();
        builder.Property(value => value.NextAttemptAtUtc).HasColumnName("next_attempt_at").IsRequired();
        builder.Property(value => value.LeaseOwner).HasColumnName("lease_owner").HasMaxLength(120);
        builder.Property(value => value.LeaseExpiresAtUtc).HasColumnName("lease_expires_at");
        builder.Property(value => value.ErrorCode).HasColumnName("error_code").HasMaxLength(120);
        builder.Property(value => value.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(value => value.CompletedAt).HasColumnName("completed_at");
        builder.HasIndex(value => new { value.Status, value.NextAttemptAtUtc });
    }
}
