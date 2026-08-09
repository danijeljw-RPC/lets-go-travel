using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Support.Domain;
using ReadyToGoTravel.Support.Notifications;
using ReadyToGoTravel.Support.Persistence;

namespace ReadyToGoTravel.Support.Tests;

public sealed class SupportPersistenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task NotificationOutboxDedupeKeyIsUnique()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var entity = fixture.Context.Model.FindEntityType(typeof(SupportNotificationOutboxItem))!;
        var index = entity.GetIndexes().Single(value =>
            value.Properties.Select(property => property.Name).SequenceEqual([nameof(SupportNotificationOutboxItem.DedupeKey)]));

        Assert.True(index.IsUnique);
    }

    [Fact]
    public async Task GuestAccessTokenHashIsUnique()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var entity = fixture.Context.Model.FindEntityType(typeof(SupportGuestAccessToken))!;
        var index = entity.GetIndexes().Single(value =>
            value.Properties.Select(property => property.Name).SequenceEqual([nameof(SupportGuestAccessToken.TokenHash)]));

        Assert.True(index.IsUnique);
    }

    [Fact]
    public async Task TicketMessageSequenceNumberIsUniquePerTicket()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var entity = fixture.Context.Model.FindEntityType(typeof(SupportTicketMessage))!;
        var index = entity.GetIndexes().Single(value =>
            value.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(SupportTicketMessage.TicketId), nameof(SupportTicketMessage.SequenceNumber)]));

        Assert.True(index.IsUnique);
    }

    [Fact]
    public async Task DirectUpdateOfATicketMessageThroughEfIsRejected()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticket = SupportTicket.Create("sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help", Now);
        fixture.Context.Tickets.Add(ticket);
        await fixture.Context.SaveChangesAsync();

        var message = await fixture.Context.TicketMessages.SingleAsync();
        fixture.Context.Entry(message).Property("Body").CurrentValue = "Tampered";
        fixture.Context.Entry(message).State = EntityState.Modified;

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task DirectDeleteOfATicketMessageThroughEfIsRejected()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticket = SupportTicket.Create("sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help", Now);
        fixture.Context.Tickets.Add(ticket);
        await fixture.Context.SaveChangesAsync();

        var message = await fixture.Context.TicketMessages.SingleAsync();
        fixture.Context.TicketMessages.Remove(message);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task StringColumnsHaveExplicitMaxLength()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        foreach (var entityType in fixture.Context.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType != typeof(string) || property.Name.EndsWith("Json", StringComparison.Ordinal))
                {
                    continue;
                }

                Assert.True(
                    property.GetMaxLength().HasValue,
                    $"{entityType.ClrType.Name}.{property.Name} must declare a maximum length.");
            }
        }
    }
}

internal sealed class SupportDatabaseFixture(SqliteConnection connection, SupportDbContext context) : IAsyncDisposable
{
    public SupportDbContext Context { get; } = context;

    public static async Task<SupportDatabaseFixture> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var context = new SupportDbContext(new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options);
        await context.Database.EnsureCreatedAsync();
        return new SupportDatabaseFixture(connection, context);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await connection.DisposeAsync();
    }
}
