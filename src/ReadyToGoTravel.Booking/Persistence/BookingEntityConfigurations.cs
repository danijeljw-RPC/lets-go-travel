using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReadyToGoTravel.Booking.Bookings;
using ReadyToGoTravel.Booking.Checkout;
using ReadyToGoTravel.Booking.Idempotency;
using ReadyToGoTravel.Booking.Notifications;
using ReadyToGoTravel.Booking.Payments;
using ReadyToGoTravel.Booking.Reconciliation;
using ReadyToGoTravel.Booking.Webhooks;

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
        builder.Property(value => value.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired().IsConcurrencyToken();
        builder.Property(value => value.CustomerLocale).HasColumnName("customer_locale").HasMaxLength(35).IsRequired();
        builder.Property(value => value.NotificationTimeZoneId).HasColumnName("notification_time_zone_id").HasMaxLength(80).IsRequired();
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
        builder.OwnsOne(value => value.PaymentPlan, plan =>
        {
            plan.ToTable("payment_plans");
            plan.WithOwner().HasForeignKey("checkout_session_id");
            plan.Property<Guid>("checkout_session_id").HasColumnName("checkout_session_id");
            plan.Property(value => value.Provider).HasColumnName("provider").HasMaxLength(80);
            plan.Property(value => value.MerchantModel).HasColumnName("merchant_model").HasMaxLength(40);
            plan.Property(value => value.CustomerPaymentRoute).HasColumnName("customer_payment_route").HasMaxLength(80);
            plan.Property(value => value.SettlementRoute).HasColumnName("settlement_route").HasMaxLength(80);
            plan.Property(value => value.Amount).HasColumnName("amount").HasPrecision(18, 2);
            plan.Property(value => value.Currency).HasColumnName("currency").HasMaxLength(3);
            plan.Property(value => value.RequiredCustomerAction).HasColumnName("required_customer_action").HasMaxLength(40);
            plan.Property(value => value.RefundOwner).HasColumnName("refund_owner").HasMaxLength(40);
            plan.Property(value => value.SeparateComponentCharges).HasColumnName("separate_component_charges");
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
        builder.Property(value => value.OfferId).HasColumnName("offer_id").HasMaxLength(255).IsRequired();
        builder.Property(value => value.TravellerId).HasColumnName("traveller_id").IsRequired();
        builder.HasIndex("checkout_session_id", nameof(TravellerSnapshot.OfferId), nameof(TravellerSnapshot.TravellerId)).IsUnique();
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
        builder.Property(value => value.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired().IsConcurrencyToken();
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
        builder.Property(value => value.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired().IsConcurrencyToken();
        builder.Property(value => value.ProviderBookingReference).HasColumnName("provider_booking_reference").HasMaxLength(255);
        builder.HasIndex(value => value.ProviderBookingReference).IsUnique().HasFilter("provider_booking_reference IS NOT NULL");
        builder.Property(value => value.FailureCode).HasColumnName("failure_code").HasMaxLength(120);
        builder.Property(value => value.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(value => value.UpdatedAt).HasColumnName("updated_at").IsRequired().IsConcurrencyToken();
        builder.Property(value => value.CurrentCanonicalHash).HasColumnName("current_canonical_hash").HasMaxLength(64);
        builder.Property(value => value.CurrentVersionNumber).HasColumnName("current_version_number").IsRequired().IsConcurrencyToken();
        builder.Property(value => value.LastReconciledAt).HasColumnName("last_reconciled_at");
        builder.Property(value => value.NextDepartureAt).HasColumnName("next_departure_at");
    }
}

internal sealed class BookingVersionConfiguration : IEntityTypeConfiguration<BookingVersion>
{
    public void Configure(EntityTypeBuilder<BookingVersion> builder)
    {
        builder.ToTable("booking_versions");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.ComponentBookingId).HasColumnName("component_booking_id");
        builder.HasOne<ComponentBooking>().WithMany().HasForeignKey(value => value.ComponentBookingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(value => new { value.ComponentBookingId, value.VersionNumber }).IsUnique();
        builder.Property(value => value.VersionNumber).HasColumnName("version_number").IsRequired();
        builder.Property(value => value.ObservedAt).HasColumnName("observed_at").IsRequired();
        builder.Property(value => value.EffectiveAt).HasColumnName("effective_at");
        builder.Property(value => value.Source).HasColumnName("source").HasMaxLength(40).IsRequired();
        builder.Property(value => value.CanonicalisationVersion).HasColumnName("canonicalisation_version").HasMaxLength(40).IsRequired();
        builder.Property(value => value.CanonicalSnapshotJson).HasColumnName("canonical_snapshot_json").IsRequired();
        builder.Property(value => value.CanonicalHash).HasColumnName("canonical_hash").HasMaxLength(64).IsRequired();
        builder.Property(value => value.MetadataJson).HasColumnName("metadata_json").IsRequired();
        builder.Property(value => value.FlagsJson).HasColumnName("flags_json").IsRequired();
        builder.Property(value => value.DiffJson).HasColumnName("diff_json").IsRequired();
        builder.Property(value => value.Severity).HasColumnName("severity").HasMaxLength(32).IsRequired();
        builder.Property(value => value.CorrelationId).HasColumnName("correlation_id").HasMaxLength(128).IsRequired();
        builder.Property(value => value.NextDepartureAt).HasColumnName("next_departure_at");
    }
}

internal sealed class ReconciliationWorkConfiguration : IEntityTypeConfiguration<ReconciliationWork>
{
    public void Configure(EntityTypeBuilder<ReconciliationWork> builder)
    {
        builder.ToTable("reconciliation_work");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.ComponentBookingId).HasColumnName("component_booking_id");
        builder.HasOne<ComponentBooking>().WithMany().HasForeignKey(value => value.ComponentBookingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(value => value.ComponentBookingId).IsUnique();
        builder.HasIndex(value => new { value.Product, value.Status, value.DueAtUtc });
        builder.Property(value => value.Product).HasColumnName("product").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(value => value.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16).IsRequired().IsConcurrencyToken();
        builder.Ignore(value => value.DueAt);
        builder.Property(value => value.DueAtUtc).HasColumnName("due_at");
        builder.Property(value => value.LeaseOwner).HasColumnName("lease_owner").HasMaxLength(120);
        builder.Ignore(value => value.LeaseExpiresAt);
        builder.Property(value => value.LeaseExpiresAtUtc).HasColumnName("lease_expires_at");
        builder.Property(value => value.Attempts).HasColumnName("attempts").IsRequired();
        builder.Property(value => value.ConsecutiveFailures).HasColumnName("consecutive_failures").IsRequired();
        builder.Property(value => value.Source).HasColumnName("source").HasMaxLength(40).IsRequired();
        builder.Property(value => value.CorrelationId).HasColumnName("correlation_id").HasMaxLength(128).IsRequired();
        builder.Property(value => value.LastErrorCode).HasColumnName("last_error_code").HasMaxLength(120);
        builder.Property(value => value.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(value => value.UpdatedAt).HasColumnName("updated_at").IsRequired().IsConcurrencyToken();
    }
}

internal sealed class ReconciliationAttemptConfiguration : IEntityTypeConfiguration<ReconciliationAttempt>
{
    public void Configure(EntityTypeBuilder<ReconciliationAttempt> builder)
    {
        builder.ToTable("reconciliation_attempts");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.WorkId).HasColumnName("work_id");
        builder.HasOne<ReconciliationWork>().WithMany().HasForeignKey(value => value.WorkId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(value => value.ComponentBookingId).HasColumnName("component_booking_id");
        builder.Property(value => value.AttemptNumber).HasColumnName("attempt_number").IsRequired();
        builder.HasIndex(value => new { value.WorkId, value.AttemptNumber }).IsUnique();
        builder.Property(value => value.StartedAt).HasColumnName("started_at").IsRequired();
        builder.Property(value => value.CompletedAt).HasColumnName("completed_at").IsRequired();
        builder.Property(value => value.Outcome).HasColumnName("outcome").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(value => value.ErrorCode).HasColumnName("error_code").HasMaxLength(120);
        builder.Property(value => value.CorrelationId).HasColumnName("correlation_id").HasMaxLength(128).IsRequired();
    }
}

internal sealed class OperationalCaseConfiguration : IEntityTypeConfiguration<OperationalCase>
{
    public void Configure(EntityTypeBuilder<OperationalCase> builder)
    {
        builder.ToTable("operational_cases");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.ComponentBookingId).HasColumnName("component_booking_id");
        builder.HasIndex(value => value.DedupeKey).IsUnique();
        builder.Property(value => value.DedupeKey).HasColumnName("dedupe_key").HasMaxLength(180).IsRequired();
        builder.Property(value => value.Category).HasColumnName("category").HasMaxLength(40).IsRequired();
        builder.Property(value => value.Reason).HasColumnName("reason").HasMaxLength(400).IsRequired();
        builder.Property(value => value.CreatedAt).HasColumnName("created_at").IsRequired();
    }
}

internal sealed class WebhookInboxItemConfiguration : IEntityTypeConfiguration<WebhookInboxItem>
{
    public void Configure(EntityTypeBuilder<WebhookInboxItem> builder)
    {
        builder.ToTable("webhook_inbox");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.Provider).HasColumnName("provider").HasMaxLength(40).IsRequired();
        builder.Property(value => value.Environment).HasColumnName("environment").HasMaxLength(20).IsRequired();
        builder.Property(value => value.EventId).HasColumnName("event_id").HasMaxLength(255).IsRequired();
        builder.HasIndex(value => new { value.Provider, value.Environment, value.EventId }).IsUnique();
        builder.Property(value => value.EventName).HasColumnName("event_name").HasMaxLength(160).IsRequired();
        builder.Property(value => value.RawBody).HasColumnName("raw_body").IsRequired();
        builder.Property(value => value.PayloadHash).HasColumnName("payload_hash").HasMaxLength(64).IsRequired();
        builder.Property(value => value.Sandbox).HasColumnName("sandbox").IsRequired();
        builder.Property(value => value.CorrelationId).HasColumnName("correlation_id").HasMaxLength(128).IsRequired();
        builder.Property(value => value.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired().IsConcurrencyToken();
        builder.Property(value => value.Attempts).HasColumnName("attempts").IsRequired();
        builder.Property(value => value.NextAttemptAtUtc).HasColumnName("next_attempt_at");
        builder.Property(value => value.LeaseOwner).HasColumnName("lease_owner").HasMaxLength(120);
        builder.Property(value => value.LeaseExpiresAtUtc).HasColumnName("lease_expires_at");
        builder.Property(value => value.ErrorCode).HasColumnName("error_code").HasMaxLength(120);
        builder.Property(value => value.ReceivedAt).HasColumnName("received_at").IsRequired();
        builder.Property(value => value.CompletedAt).HasColumnName("completed_at");
        builder.Property(value => value.ComponentBookingId).HasColumnName("component_booking_id");
        builder.HasIndex(value => new { value.Status, value.NextAttemptAtUtc });
        builder.HasIndex(value => value.ComponentBookingId);
    }
}

internal sealed class NotificationOutboxItemConfiguration : IEntityTypeConfiguration<NotificationOutboxItem>
{
    public void Configure(EntityTypeBuilder<NotificationOutboxItem> builder)
    {
        builder.ToTable("notification_outbox");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.CustomerId).HasColumnName("customer_id");
        builder.Property(value => value.CheckoutId).HasColumnName("checkout_id");
        builder.Property(value => value.ComponentBookingId).HasColumnName("component_booking_id");
        builder.Property(value => value.BookingVersionId).HasColumnName("booking_version_id");
        builder.HasOne<BookingVersion>().WithMany().HasForeignKey(value => value.BookingVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(value => value.DedupeKey).HasColumnName("dedupe_key").HasMaxLength(64).IsRequired();
        builder.HasIndex(value => value.DedupeKey).IsUnique();
        builder.Property(value => value.Channel).HasColumnName("channel").HasMaxLength(20).IsRequired();
        builder.Property(value => value.Template).HasColumnName("template").HasMaxLength(80).IsRequired();
        builder.Property(value => value.Severity).HasColumnName("severity").HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(value => value.Locale).HasColumnName("locale").HasMaxLength(35).IsRequired();
        builder.Property(value => value.TimeZoneId).HasColumnName("time_zone_id").HasMaxLength(80).IsRequired();
        builder.Property(value => value.PayloadJson).HasColumnName("payload_json").IsRequired();
        builder.Property(value => value.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16).IsRequired().IsConcurrencyToken();
        builder.Property(value => value.Attempts).HasColumnName("attempts").IsRequired();
        builder.Ignore(value => value.NotBefore);
        builder.Property(value => value.NotBeforeUtc).HasColumnName("not_before").IsRequired();
        builder.Property(value => value.LeaseOwner).HasColumnName("lease_owner").HasMaxLength(120);
        builder.Property(value => value.LeaseExpiresAtUtc).HasColumnName("lease_expires_at");
        builder.Property(value => value.DeliveryReference).HasColumnName("delivery_reference").HasMaxLength(255);
        builder.Property(value => value.ErrorCode).HasColumnName("error_code").HasMaxLength(120);
        builder.Property(value => value.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(value => value.UpdatedAt).HasColumnName("updated_at").IsRequired().IsConcurrencyToken();
        builder.HasIndex(value => new { value.Status, value.NotBeforeUtc });
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
        builder.Property(value => value.DedupeKey).HasColumnName("dedupe_key").HasMaxLength(32).IsRequired();
        builder.Property(value => value.Reason).HasColumnName("reason").HasMaxLength(400).IsRequired();
        builder.HasIndex("checkout_session_id", nameof(BookingRecoveryCase.DedupeKey), nameof(BookingRecoveryCase.Reason))
            .HasDatabaseName("ux_booking_recovery_cases_checkout_dedupe_reason")
            .IsUnique();
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
        builder.Property(value => value.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16).IsRequired().IsConcurrencyToken();
        builder.Property(value => value.ResponseStatusCode).HasColumnName("response_status_code");
        builder.Property(value => value.ResponseBody).HasColumnName("response_body");
        builder.Property(value => value.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(value => value.UpdatedAt).HasColumnName("updated_at").IsRequired().IsConcurrencyToken();
        builder.Property(value => value.ExpiresAt).HasColumnName("expires_at").IsRequired();
    }
}
