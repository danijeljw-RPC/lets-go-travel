using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReadyToGoTravel.Booking.Bookings;
using ReadyToGoTravel.Booking.Checkout;
using ReadyToGoTravel.Booking.Idempotency;
using ReadyToGoTravel.Booking.Payments;

namespace ReadyToGoTravel.Booking.Persistence;

internal sealed class CheckoutSessionConfiguration : IEntityTypeConfiguration<CheckoutSession>
{
    public void Configure(EntityTypeBuilder<CheckoutSession> builder)
    {
        builder.ToTable("checkout_sessions");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.CustomerId).HasColumnName("customer_id");
        builder.Property(value => value.TripId).HasColumnName("trip_id");
        builder.HasIndex(value => value.CustomerId);
        builder.HasIndex(value => value.TripId);
        builder.Property(value => value.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(value => value.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(value => value.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(value => value.ExpiresAt).HasColumnName("expires_at").IsRequired();

        builder.Ignore(value => value.CurrentRevision);
        builder.Navigation(value => value.Revisions).HasField("revisions").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(value => value.TravellerSnapshots).HasField("travellerSnapshots").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(value => value.Components).HasField("components").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(value => value.PaymentAttempts).HasField("paymentAttempts").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(value => value.RecoveryCases).HasField("recoveryCases").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(value => value.Revisions).WithOne().HasForeignKey("checkout_session_id").OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(value => value.TravellerSnapshots).WithOne().HasForeignKey("checkout_session_id").OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(value => value.Components).WithOne().HasForeignKey("checkout_session_id").OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(value => value.PaymentAttempts).WithOne().HasForeignKey("checkout_session_id").OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(value => value.RecoveryCases).WithOne().HasForeignKey("checkout_session_id").OnDelete(DeleteBehavior.Cascade);

        builder.OwnsOne(value => value.AcceptedRevision, acceptance =>
        {
            acceptance.ToTable("checkout_acceptances");
            acceptance.WithOwner().HasForeignKey("checkout_session_id");
            acceptance.Property<Guid>("checkout_session_id").HasColumnName("checkout_session_id");
            acceptance.Property(value => value.RevisionNumber).HasColumnName("revision_number").IsRequired();
            acceptance.Property(value => value.AcceptedTotal).HasColumnName("accepted_total").HasPrecision(18, 2).IsRequired();
            acceptance.Property(value => value.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
            acceptance.Property(value => value.TermsHash).HasColumnName("terms_hash").HasMaxLength(64).IsRequired();
            acceptance.Property(value => value.PolicyVersion).HasColumnName("policy_version").HasMaxLength(40).IsRequired();
            acceptance.Property(value => value.AcceptedAt).HasColumnName("accepted_at").IsRequired();
        });
    }
}

internal sealed class CheckoutRevisionConfiguration : IEntityTypeConfiguration<CheckoutRevision>
{
    public void Configure(EntityTypeBuilder<CheckoutRevision> builder)
    {
        builder.ToTable("checkout_revisions");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property<Guid>("checkout_session_id").HasColumnName("checkout_session_id");
        builder.HasIndex("checkout_session_id", nameof(CheckoutRevision.Number)).IsUnique();
        builder.Property(value => value.Number).HasColumnName("number").IsRequired();
        builder.Property(value => value.Total).HasColumnName("total").HasPrecision(18, 2).IsRequired();
        builder.Property(value => value.TransactionCurrency).HasColumnName("transaction_currency").HasMaxLength(3).IsRequired();
        builder.Property(value => value.TermsHash).HasColumnName("terms_hash").HasMaxLength(64).IsRequired();
        builder.Property(value => value.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(value => value.ResolvedAt).HasColumnName("resolved_at").IsRequired();

        builder.Ignore(value => value.PriceComponents);
        builder.Navigation(value => value.Components).HasField("components").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(value => value.Components).WithOne().HasForeignKey("checkout_revision_id").OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CheckoutRevisionComponentConfiguration : IEntityTypeConfiguration<CheckoutRevisionComponent>
{
    public void Configure(EntityTypeBuilder<CheckoutRevisionComponent> builder)
    {
        builder.ToTable("checkout_revision_components");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property<Guid>("checkout_revision_id").HasColumnName("checkout_revision_id");
        builder.Property(value => value.Product).HasColumnName("product").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(value => value.OfferId).HasColumnName("offer_id").HasMaxLength(255).IsRequired();
        builder.Property(value => value.ProviderBinding).HasColumnName("provider_binding").HasMaxLength(120).IsRequired();
        builder.Property(value => value.ProductDetail).HasColumnName("product_detail").HasMaxLength(500).IsRequired();
        builder.Property(value => value.MinimumTotal).HasColumnName("minimum_total").HasPrecision(18, 2).IsRequired();
        builder.Property(value => value.ProviderRevision).HasColumnName("provider_revision").HasMaxLength(120).IsRequired();
        builder.Property(value => value.TermsHash).HasColumnName("terms_hash").HasMaxLength(64).IsRequired();
        builder.Property(value => value.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(value => value.ResolvedAt).HasColumnName("resolved_at").IsRequired();
    }
}

internal sealed class TravellerSnapshotConfiguration : IEntityTypeConfiguration<TravellerSnapshot>
{
    public void Configure(EntityTypeBuilder<TravellerSnapshot> builder)
    {
        builder.ToTable("traveller_snapshots");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property<Guid>("checkout_session_id").HasColumnName("checkout_session_id");
        builder.Property(value => value.TravellerId).HasColumnName("traveller_id").IsRequired();
        builder.HasIndex("checkout_session_id", nameof(TravellerSnapshot.TravellerId)).IsUnique();
        builder.Property(value => value.GivenName).HasColumnName("given_name").HasMaxLength(100).IsRequired();
        builder.Property(value => value.FamilyName).HasColumnName("family_name").HasMaxLength(100).IsRequired();
        builder.Property(value => value.IsMinor).HasColumnName("is_minor").IsRequired();
        builder.Property(value => value.GuardianAuthorityConfirmedAt).HasColumnName("guardian_authority_confirmed_at");
        builder.Property(value => value.AgeAtTravel).HasColumnName("age_at_travel");
    }
}

internal sealed class PaymentAttemptConfiguration : IEntityTypeConfiguration<PaymentAttempt>
{
    public void Configure(EntityTypeBuilder<PaymentAttempt> builder)
    {
        builder.ToTable("payment_attempts");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property<Guid>("checkout_session_id").HasColumnName("checkout_session_id");
        builder.Property(value => value.Provider).HasColumnName("provider").HasMaxLength(80).IsRequired();
        builder.Property(value => value.ProviderPaymentReference).HasColumnName("provider_payment_reference").HasMaxLength(255).IsRequired();
        builder.Property(value => value.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
        builder.Property(value => value.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        builder.Property(value => value.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(value => value.ProviderReturnReference).HasColumnName("provider_return_reference").HasMaxLength(255);
        builder.HasIndex(value => value.ProviderReturnReference).IsUnique().HasFilter("provider_return_reference IS NOT NULL");
        builder.Property(value => value.FailureCode).HasColumnName("failure_code").HasMaxLength(120);
        builder.Property(value => value.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(value => value.UpdatedAt).HasColumnName("updated_at").IsRequired();
    }
}

internal sealed class ComponentBookingConfiguration : IEntityTypeConfiguration<ComponentBooking>
{
    public void Configure(EntityTypeBuilder<ComponentBooking> builder)
    {
        builder.ToTable("component_bookings");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property<Guid>("checkout_session_id").HasColumnName("checkout_session_id");
        builder.Property(value => value.Product).HasColumnName("product").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(value => value.OfferId).HasColumnName("offer_id").HasMaxLength(255).IsRequired();
        builder.Property(value => value.ProviderBinding).HasColumnName("provider_binding").HasMaxLength(120).IsRequired();
        builder.Property(value => value.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(value => value.ProviderBookingReference).HasColumnName("provider_booking_reference").HasMaxLength(255);
        builder.HasIndex(value => value.ProviderBookingReference).IsUnique().HasFilter("provider_booking_reference IS NOT NULL");
        builder.Property(value => value.FailureCode).HasColumnName("failure_code").HasMaxLength(120);
        builder.Property(value => value.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(value => value.UpdatedAt).HasColumnName("updated_at").IsRequired();
    }
}

internal sealed class BookingRecoveryCaseConfiguration : IEntityTypeConfiguration<BookingRecoveryCase>
{
    public void Configure(EntityTypeBuilder<BookingRecoveryCase> builder)
    {
        builder.ToTable("booking_recovery_cases");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property<Guid>("checkout_session_id").HasColumnName("checkout_session_id");
        builder.Property(value => value.ComponentBookingId).HasColumnName("component_booking_id");
        builder.Property(value => value.Reason).HasColumnName("reason").HasMaxLength(400).IsRequired();
        builder.Property(value => value.CreatedAt).HasColumnName("created_at").IsRequired();
    }
}

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_records");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(value => value.Operation).HasColumnName("operation").HasMaxLength(80).IsRequired();
        builder.Property(value => value.Key).HasColumnName("key").HasMaxLength(255).IsRequired();
        builder.HasIndex(value => new { value.CustomerId, value.Operation, value.Key })
            .HasDatabaseName("ux_idempotency_records_customer_operation_key")
            .IsUnique();
        builder.Property(value => value.Fingerprint).HasColumnName("fingerprint").HasMaxLength(64).IsRequired();
        builder.Property(value => value.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(value => value.ResponseStatusCode).HasColumnName("response_status_code");
        builder.Property(value => value.ResponseBody).HasColumnName("response_body");
        builder.Property(value => value.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(value => value.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(value => value.ExpiresAt).HasColumnName("expires_at").IsRequired();
    }
}
