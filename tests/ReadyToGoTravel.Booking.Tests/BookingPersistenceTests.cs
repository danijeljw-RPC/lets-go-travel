using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Booking.Bookings;
using ReadyToGoTravel.Booking.Checkout;
using ReadyToGoTravel.Booking.Idempotency;
using ReadyToGoTravel.Booking.Payments;
using ReadyToGoTravel.Booking.Persistence;

namespace ReadyToGoTravel.Booking.Tests;

public sealed class BookingPersistenceTests
{
    [Fact]
    public async Task TravellerSnapshotDoesNotChangeWhenConsumerDataChanges()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var checkout = CheckoutFactory.CreateWithTraveller("Ari", "Taylor");
        fixture.Context.Checkouts.Add(checkout);
        await fixture.Context.SaveChangesAsync();

        var stored = await fixture.Context.Checkouts.AsNoTracking()
            .Include(value => value.Revisions)
                .ThenInclude(value => value.Components)
            .Include(value => value.TravellerSnapshots)
            .SingleAsync();

        Assert.Equal("Ari", stored.TravellerSnapshots.Single().GivenName);
        Assert.Equal("hotel-1", stored.CurrentRevision.Components.Single().OfferId);
        var properties = fixture.Context.Model.FindEntityType(typeof(TravellerSnapshot))!
            .GetProperties()
            .Select(property => property.Name);
        Assert.DoesNotContain(properties, property =>
            property.Contains("Passport", StringComparison.OrdinalIgnoreCase)
            || property.Contains("IdentityDocument", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ProviderReturnReferenceIsUnique()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var entity = fixture.Context.Model.FindEntityType(typeof(PaymentAttempt))!;
        var index = entity.GetIndexes().Single(value =>
            value.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(PaymentAttempt.ProviderReturnReference)]));

        Assert.True(index.IsUnique);
    }

    [Fact]
    public async Task ConcurrentSameStatusComponentUpdatesCannotOverwriteProviderState()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"ready-to-go-travel-component-concurrency-{Guid.CreateVersion7():N}.db");
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;
        var initialTime = new FixtureTimeProvider(
            new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero));

        try
        {
            Guid componentId;
            await using (var setup = new BookingDbContext(options))
            {
                await setup.Database.EnsureCreatedAsync();
                var checkout = CheckoutFactory.CreateWithTraveller("Ari", "Taylor");
                Assert.True(checkout.AcceptRevision(
                    checkout.CurrentRevision.Number,
                    checkout.CurrentRevision.Total,
                    checkout.CurrentRevision.TransactionCurrency,
                    checkout.CurrentRevision.TermsHash,
                    "checkout-v1",
                    initialTime).IsSuccess);
                Assert.True(checkout.BeginPayment("fixture", "pay-001", initialTime).IsSuccess);
                Assert.True(checkout.RecordPayment(PaymentProviderResult.Captured("return-001"), initialTime).IsSuccess);
                Assert.True(checkout.BeginBooking(initialTime).IsSuccess);
                componentId = checkout.Components.Single().Id;
                setup.Checkouts.Add(checkout);
                await setup.SaveChangesAsync();
            }

            await using var first = new BookingDbContext(options);
            await using var second = new BookingDbContext(options);
            var firstCheckout = await first.Checkouts.Include(value => value.Components).SingleAsync();
            var secondCheckout = await second.Checkouts.Include(value => value.Components).SingleAsync();
            Assert.True(firstCheckout.RecordBookingResult(
                componentId,
                BookingProviderResult.Pending("provider-reference-one"),
                initialTime).IsSuccess);
            Assert.True(secondCheckout.RecordBookingResult(
                componentId,
                BookingProviderResult.Pending("provider-reference-two"),
                initialTime).IsSuccess);

            await first.SaveChangesAsync();

            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    private sealed class FixtureTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

internal sealed class BookingDatabaseFixture(SqliteConnection connection, BookingDbContext context) : IAsyncDisposable
{
    public BookingDbContext Context { get; } = context;

    public IIdempotencyService Idempotency => new IdempotencyService(Context, TimeProvider.System);

    public static async Task<BookingDatabaseFixture> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var context = new BookingDbContext(new DbContextOptionsBuilder<BookingDbContext>()
            .UseSqlite(connection)
            .Options);
        await context.Database.EnsureCreatedAsync();
        return new BookingDatabaseFixture(connection, context);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await connection.DisposeAsync();
    }
}

internal static class CheckoutFactory
{
    private static readonly TimeProvider Clock = new FixtureTimeProvider(
        new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero));

    public static CheckoutSession CreateWithTraveller(string givenName, string familyName)
    {
        var result = CheckoutSession.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            [new ResolvedCheckoutOffer(
                CheckoutProduct.Hotel,
                "hotel-1",
                "fixture-hotel",
                "Melbourne hotel",
                400m,
                "AUD",
                "terms-hotel-v1",
                "hotel-r1",
                Clock.GetUtcNow().AddHours(1),
                Clock.GetUtcNow())],
            [new TravellerSnapshot("hotel-1", Guid.CreateVersion7(), givenName, familyName, false, null)],
            Clock);

        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private sealed class FixtureTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
