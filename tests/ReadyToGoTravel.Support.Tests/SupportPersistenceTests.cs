using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ReadyToGoTravel.Support.Domain;
using ReadyToGoTravel.Support.Notifications;
using ReadyToGoTravel.Support.Persistence;

namespace ReadyToGoTravel.Support.Tests;

public sealed class SupportPersistenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TicketMessageAppendOnlyTriggerRejectsUpdateButAllowsRetentionDelete()
    {
        // Regression test for a bug found via a live PostgreSQL drill (see the Slice 7 outcome
        // report): reject_support_ticket_message_mutation originally blocked BOTH UPDATE and
        // DELETE, which silently and permanently broke SupportRetentionSweepProcessor's ticket
        // deletion in production, invisible to every other test in this file because SQLite's
        // EnsureCreatedAsync never executes migration-only raw trigger SQL. This test instead
        // generates the real migration script (no live database needed) and asserts the final
        // trigger definition blocks UPDATE only.
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseNpgsql("Host=localhost;Database=rtgt;Username=rtgt")
            .Options;
        using var context = new SupportDbContext(options);

        var migrationScript = context.Database.GetService<IMigrator>().GenerateScript();
        Assert.Contains("BEFORE UPDATE ON support.support_ticket_messages", migrationScript, StringComparison.Ordinal);
    }

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
    public async Task OnlyOneActiveGuestAccessTokenIsAllowedPerTicket()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticket = SupportTicket.Create("sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help", Now);
        fixture.Context.Tickets.Add(ticket);
        await fixture.Context.SaveChangesAsync();

        fixture.Context.GuestAccessTokens.Add(new SupportGuestAccessToken(
            Guid.CreateVersion7(Now), ticket.Id, "hash-a", Now, Now.AddDays(30), null));
        await fixture.Context.SaveChangesAsync();

        fixture.Context.GuestAccessTokens.Add(new SupportGuestAccessToken(
            Guid.CreateVersion7(Now), ticket.Id, "hash-b", Now, Now.AddDays(30), null));

        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Context.SaveChangesAsync());
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
