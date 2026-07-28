using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Consumer.Customers;
using ReadyToGoTravel.Consumer.Travellers;
using ReadyToGoTravel.Consumer.Trips;

namespace ReadyToGoTravel.Consumer.Persistence;

internal sealed class ConsumerDbContext(DbContextOptions<ConsumerDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Trip> Trips => Set<Trip>();

    public DbSet<Traveller> Travellers => Set<Traveller>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("consumer");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ConsumerDbContext).Assembly);
    }
}
