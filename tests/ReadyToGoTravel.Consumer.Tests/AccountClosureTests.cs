using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Consumer.Application;
using ReadyToGoTravel.Consumer.Customers;
using ReadyToGoTravel.Consumer.Persistence;

namespace ReadyToGoTravel.Consumer.Tests;

public sealed class AccountClosureTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ClosingAnActiveCustomerSetsStatusAndClosedAtUtc()
    {
        var customer = Customer.Create("sub-1", "en-AU", true, new FixedTimeProvider(Now)).Value!;

        customer.Close(new FixedTimeProvider(Now.AddDays(1)));

        Assert.Equal(CustomerStatus.Closed, customer.Status);
        Assert.Equal(Now.AddDays(1), customer.ClosedAtUtc);
    }

    [Fact]
    public void ClosingAnAlreadyClosedCustomerIsANoOpAndDoesNotOverwriteTheOriginalClosedAtUtc()
    {
        var customer = Customer.Create("sub-1", "en-AU", true, new FixedTimeProvider(Now)).Value!;
        customer.Close(new FixedTimeProvider(Now.AddDays(1)));

        customer.Close(new FixedTimeProvider(Now.AddDays(30)));

        Assert.Equal(Now.AddDays(1), customer.ClosedAtUtc);
    }

    [Fact]
    public async Task ConsumerBookingContextResolverDeniesAClosedCustomerEvenWithATechnicallyValidSubject()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var customer = Customer.Create("sub-1", "en-AU", true, new FixedTimeProvider(Now)).Value!;
        customer.Close(new FixedTimeProvider(Now));
        fixture.Context.Customers.Add(customer);
        await fixture.Context.SaveChangesAsync();
        var resolver = new ConsumerBookingContextResolver(fixture.Context);

        var resolved = await resolver.ResolveCustomerIdAsync("sub-1");

        Assert.Null(resolved);
    }

    [Fact]
    public async Task ConsumerBookingContextResolverStillResolvesAnActiveCustomer()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var customer = Customer.Create("sub-1", "en-AU", true, new FixedTimeProvider(Now)).Value!;
        fixture.Context.Customers.Add(customer);
        await fixture.Context.SaveChangesAsync();
        var resolver = new ConsumerBookingContextResolver(fixture.Context);

        var resolved = await resolver.ResolveCustomerIdAsync("sub-1");

        Assert.Equal(customer.Id, resolved);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class DatabaseFixture(SqliteConnection connection, ConsumerDbContext context) : IAsyncDisposable
    {
        public ConsumerDbContext Context { get; } = context;

        public static async Task<DatabaseFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ConsumerDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new ConsumerDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new DatabaseFixture(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
