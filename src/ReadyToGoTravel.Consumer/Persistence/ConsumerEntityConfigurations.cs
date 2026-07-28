using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReadyToGoTravel.Consumer.Customers;
using ReadyToGoTravel.Consumer.Travellers;
using ReadyToGoTravel.Consumer.Trips;

namespace ReadyToGoTravel.Consumer.Persistence;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(customer => customer.Id);
        builder.Property(customer => customer.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(customer => customer.Subject).HasColumnName("subject").HasMaxLength(255).IsRequired();
        builder.HasIndex(customer => customer.Subject).IsUnique();
        builder.Property(customer => customer.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16);
        builder.Property(customer => customer.PreferredLocale).HasColumnName("preferred_locale").HasMaxLength(35);
        builder.Property(customer => customer.DisplayCurrency).HasColumnName("display_currency").HasMaxLength(3);
        builder.Property(customer => customer.AdultConfirmedAt).HasColumnName("adult_confirmed_at");
        builder.Property(customer => customer.AdultPolicyVersion).HasColumnName("adult_policy_version").HasMaxLength(20);
        builder.Property(customer => customer.CreatedAt).HasColumnName("created_at");
        builder.Property(customer => customer.UpdatedAt).HasColumnName("updated_at");
    }
}

internal sealed class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.ToTable("trips", table => table.HasCheckConstraint(
            "ck_trips_dates",
            "end_date IS NULL OR start_date IS NULL OR end_date >= start_date"));
        builder.HasKey(trip => trip.Id);
        builder.Property(trip => trip.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(trip => trip.CustomerId).HasColumnName("customer_id");
        builder.HasIndex(trip => trip.CustomerId);
        builder.HasOne<Customer>().WithMany().HasForeignKey(trip => trip.CustomerId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(trip => trip.Title).HasColumnName("title").HasMaxLength(120).IsRequired();
        builder.Property(trip => trip.PrimaryDestination).HasColumnName("primary_destination").HasMaxLength(160);
        builder.Property(trip => trip.StartDate).HasColumnName("start_date");
        builder.Property(trip => trip.EndDate).HasColumnName("end_date");
        builder.Property(trip => trip.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16);
        builder.Property(trip => trip.CreatedAt).HasColumnName("created_at");
        builder.Property(trip => trip.UpdatedAt).HasColumnName("updated_at");
    }
}

internal sealed class TravellerConfiguration : IEntityTypeConfiguration<Traveller>
{
    public void Configure(EntityTypeBuilder<Traveller> builder)
    {
        builder.ToTable("travellers");
        builder.HasKey(traveller => traveller.Id);
        builder.Property(traveller => traveller.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(traveller => traveller.CustomerId).HasColumnName("customer_id");
        builder.HasIndex(traveller => traveller.CustomerId);
        builder.HasOne<Customer>().WithMany().HasForeignKey(traveller => traveller.CustomerId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(traveller => traveller.GivenName).HasColumnName("given_name").HasMaxLength(100).IsRequired();
        builder.Property(traveller => traveller.FamilyName).HasColumnName("family_name").HasMaxLength(100).IsRequired();
        builder.Property(traveller => traveller.RelationshipLabel).HasColumnName("relationship_label").HasMaxLength(60);
        builder.Property(traveller => traveller.IsMinor).HasColumnName("is_minor");
        builder.Property(traveller => traveller.GuardianAuthorityConfirmedAt).HasColumnName("guardian_authority_confirmed_at");
        builder.Property(traveller => traveller.CreatedAt).HasColumnName("created_at");
        builder.Property(traveller => traveller.UpdatedAt).HasColumnName("updated_at");
    }
}
