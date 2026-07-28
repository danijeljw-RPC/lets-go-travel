using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Consumer.Customers;
using ReadyToGoTravel.Consumer.Persistence;
using ReadyToGoTravel.Consumer.Travellers;
using ReadyToGoTravel.Consumer.Trips;

namespace ReadyToGoTravel.Consumer.Tests;

public sealed class PersistenceTests
{
    [Fact]
    public async Task SubjectIsUnique()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var first = Customer.Create("same-subject", "en-AU", true, TimeProvider.System).Value!;
        var second = Customer.Create("same-subject", "en-AU", true, TimeProvider.System).Value!;
        fixture.Context.Customers.AddRange(first, second);

        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task TripQueriesCanBeScopedToOwningCustomer()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var owner = Customer.Create("owner", "en-AU", true, TimeProvider.System).Value!;
        var stranger = Customer.Create("stranger", "en-AU", true, TimeProvider.System).Value!;
        fixture.Context.Customers.AddRange(owner, stranger);
        fixture.Context.Trips.AddRange(
            Trip.Create(owner.Id, "Owner trip", null, null, null, TimeProvider.System).Value!,
            Trip.Create(stranger.Id, "Stranger trip", null, null, null, TimeProvider.System).Value!);
        await fixture.Context.SaveChangesAsync();

        var visible = await fixture.Context.Trips
            .Where(trip => trip.CustomerId == owner.Id)
            .Select(trip => trip.Title)
            .ToArrayAsync();

        Assert.Equal(["Owner trip"], visible);
    }

    [Fact]
    public async Task TravellerTableContainsNoSensitiveReusableColumns()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var command = fixture.Connection.CreateCommand();
        command.CommandText = "PRAGMA table_info('travellers');";
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new List<string>();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(1));
        }

        Assert.DoesNotContain(columns, column => column.Contains("birth", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(columns, column => column.Contains("passport", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(columns, column => column.Contains("document", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class DatabaseFixture(SqliteConnection connection, ConsumerDbContext context) : IAsyncDisposable
    {
        public SqliteConnection Connection { get; } = connection;

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
            await Connection.DisposeAsync();
        }
    }
}
