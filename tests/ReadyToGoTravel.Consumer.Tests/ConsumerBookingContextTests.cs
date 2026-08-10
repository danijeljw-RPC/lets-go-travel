using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Consumer.Application;
using ReadyToGoTravel.Consumer.Customers;
using ReadyToGoTravel.Consumer.Persistence;
using ReadyToGoTravel.Consumer.Travellers;
using ReadyToGoTravel.Consumer.Trips;

namespace ReadyToGoTravel.Consumer.Tests;

public sealed class ConsumerBookingContextTests
{
    [Fact]
    public async Task BookingContextReturnsOnlyAnActiveOwnedTripAndOwnedTravellers()
    {
        await using var fixture = await ConsumerFixture.CreateAsync();
        var owner = await fixture.CreateActiveCustomerAsync("owner");
        var trip = await fixture.CreateTripAsync(owner.Id, "Melbourne");
        var traveller = await fixture.CreateTravellerAsync(owner.Id, "Ari", "Taylor");

        var result = await fixture.ContextResolver.ResolveAsync("owner", trip.Id, [traveller.Id], default);

        Assert.True(result.IsSuccess);
        Assert.Equal(owner.Id, result.Value!.CustomerId);
        Assert.Equal(trip.Id, result.Value.TripId);
        Assert.Equal("en-AU", result.Value.PreferredLocale);
        Assert.Equal([traveller.Id], result.Value.Travellers.Select(value => value.TravellerId));
    }

    [Fact]
    public async Task BookingContextDoesNotRevealAnotherCustomersTrip()
    {
        await using var fixture = await ConsumerFixture.CreateAsync();
        var owner = await fixture.CreateActiveCustomerAsync("owner");
        var ownersTrip = await fixture.CreateTripAsync(owner.Id, "Melbourne");
        await fixture.CreateActiveCustomerAsync("stranger");

        var result = await fixture.ContextResolver.ResolveAsync(
            "stranger",
            ownersTrip.Id,
            [],
            default);

        Assert.False(result.IsSuccess);
        Assert.Equal("trip_not_found", result.ErrorCode);
    }

    [Fact]
    public async Task BookingContextDoesNotRevealAnotherCustomersTraveller()
    {
        await using var fixture = await ConsumerFixture.CreateAsync();
        var owner = await fixture.CreateActiveCustomerAsync("owner");
        var ownersTrip = await fixture.CreateTripAsync(owner.Id, "Melbourne");
        var stranger = await fixture.CreateActiveCustomerAsync("stranger");
        var strangersTraveller = await fixture.CreateTravellerAsync(stranger.Id, "Sam", "Lee");

        var result = await fixture.ContextResolver.ResolveAsync(
            "owner",
            ownersTrip.Id,
            [strangersTraveller.Id],
            default);

        Assert.False(result.IsSuccess);
        Assert.Equal("traveller_not_found", result.ErrorCode);
    }

    private sealed class ConsumerFixture(SqliteConnection connection, ConsumerDbContext context) : IAsyncDisposable
    {
        public ConsumerDbContext Context { get; } = context;

        public ConsumerBookingContextResolver ContextResolver { get; } = new ConsumerBookingContextResolver(context);

        public static async Task<ConsumerFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var context = new ConsumerDbContext(new DbContextOptionsBuilder<ConsumerDbContext>()
                .UseSqlite(connection)
                .Options);
            await context.Database.EnsureCreatedAsync();
            return new ConsumerFixture(connection, context);
        }

        public async Task<Customer> CreateActiveCustomerAsync(string subject)
        {
            var customer = Customer.Create(subject, "en-AU", true, TimeProvider.System).Value!;
            Context.Customers.Add(customer);
            await Context.SaveChangesAsync();
            return customer;
        }

        public async Task<Trip> CreateTripAsync(Guid customerId, string destination)
        {
            var trip = Trip.Create(customerId, destination, destination, null, null, TimeProvider.System).Value!;
            Context.Trips.Add(trip);
            await Context.SaveChangesAsync();
            return trip;
        }

        public async Task<Traveller> CreateTravellerAsync(Guid customerId, string givenName, string familyName)
        {
            var traveller = Traveller.Create(
                customerId,
                givenName,
                familyName,
                null,
                false,
                false,
                TimeProvider.System).Value!;
            Context.Travellers.Add(traveller);
            await Context.SaveChangesAsync();
            return traveller;
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
