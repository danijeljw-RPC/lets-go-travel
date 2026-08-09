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
