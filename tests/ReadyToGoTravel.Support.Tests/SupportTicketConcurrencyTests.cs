using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Support.Application;
using ReadyToGoTravel.Support.Domain;
using ReadyToGoTravel.Support.Guest;
using ReadyToGoTravel.Support.Notifications;
using ReadyToGoTravel.Support.Persistence;

namespace ReadyToGoTravel.Support.Tests;

// Uses genuinely independent DbContext instances/connections against a shared SQLite in-memory
// database (mirroring GuestAccessTokenTests.ConcurrentRotationsForTheSameTicketNeverLeaveMoreThanOneActiveToken)
// so SaveChangesAsync races for real, rather than being serialized by a single shared context.
public sealed class SupportTicketConcurrencyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ConcurrentCustomerAndStaffRepliesBothSucceedWithUniqueSequenceNumbers()
    {
        var (connectionString, keepAlive) = await CreateSharedDatabaseAsync();
        await using var _ = keepAlive;
        var ticketId = await CreateTicketAsync(connectionString, "sub-1");

        await Task.WhenAll(
            Task.Run(() => ReplyAsync(connectionString, ticketId, SupportAuthorType.Customer, "sub-1", "Customer reply")),
            Task.Run(() => ReplyAsync(connectionString, ticketId, SupportAuthorType.Support, "staff-1", "Staff reply")));

        await AssertRepliesLandedWithUniqueSequencesAsync(connectionString, ticketId, expectedReplyCount: 2);
    }

    [Fact]
    public async Task ConcurrentGuestAndStaffRepliesBothSucceedWithUniqueSequenceNumbers()
    {
        var (connectionString, keepAlive) = await CreateSharedDatabaseAsync();
        await using var _ = keepAlive;
        var ticketId = await CreateTicketAsync(connectionString, customerSubject: null);

        await Task.WhenAll(
            Task.Run(() => ReplyAsync(connectionString, ticketId, SupportAuthorType.Guest, null, "Guest reply")),
            Task.Run(() => ReplyAsync(connectionString, ticketId, SupportAuthorType.Support, "staff-1", "Staff reply")));

        await AssertRepliesLandedWithUniqueSequencesAsync(connectionString, ticketId, expectedReplyCount: 2);
    }

    [Fact]
    public async Task NoNotificationIsDuplicatedOrLostAcrossConcurrentReplies()
    {
        var (connectionString, keepAlive) = await CreateSharedDatabaseAsync();
        await using var _ = keepAlive;
        var ticketId = await CreateTicketAsync(connectionString, "sub-1");

        await Task.WhenAll(
            Task.Run(() => ReplyAsync(connectionString, ticketId, SupportAuthorType.Customer, "sub-1", "Customer reply")),
            Task.Run(() => ReplyAsync(connectionString, ticketId, SupportAuthorType.Support, "staff-1", "Staff reply")));

        await using var verifyContext = CreateContext(connectionString);
        var outboxItems = await verifyContext.SupportNotificationOutbox
            .Where(value => value.TicketId == ticketId && value.Template == SupportNotificationTemplates.MessageAdded)
            .ToListAsync();
        Assert.Equal(2, outboxItems.Count);
        Assert.Equal(outboxItems.Select(value => value.MessageId).Distinct().Count(), outboxItems.Count);
        Assert.Equal(outboxItems.Select(value => value.DedupeKey).Distinct().Count(), outboxItems.Count);
    }

    [Fact]
    public async Task ATicketsUpdatedAtAndStatusRemainConsistentAfterConcurrentReplies()
    {
        var (connectionString, keepAlive) = await CreateSharedDatabaseAsync();
        await using var _ = keepAlive;
        var ticketId = await CreateTicketAsync(connectionString, "sub-1");

        await Task.WhenAll(
            Task.Run(() => ReplyAsync(connectionString, ticketId, SupportAuthorType.Customer, "sub-1", "Customer reply")),
            Task.Run(() => ReplyAsync(connectionString, ticketId, SupportAuthorType.Support, "staff-1", "Staff reply")));

        await using var verifyContext = CreateContext(connectionString);
        var ticket = await verifyContext.Tickets.Include(value => value.Messages).SingleAsync(value => value.Id == ticketId);
        // Whichever reply's retry lands last legitimately decides the final projection - the
        // invariant under test is that the ticket ends up in a valid, non-corrupted state, not a
        // fixed ordering between two genuinely racing writers.
        Assert.True(ticket.Status is SupportTicketStatus.WaitingOnCustomer or SupportTicketStatus.WaitingOnSupport);
        Assert.True(ticket.UpdatedAt >= ticket.CreatedAt);
    }

    [Fact]
    public async Task ACloseAndAReplyRacingForTheSameTicketBothLandWithoutAnUnhandledException()
    {
        var (connectionString, keepAlive) = await CreateSharedDatabaseAsync();
        await using var _ = keepAlive;
        var ticketId = await CreateTicketAsync(connectionString, "sub-1");

        await Task.WhenAll(
            Task.Run(() => CloseAsync(connectionString, ticketId, "staff-1")),
            Task.Run(() => ReplyAsync(connectionString, ticketId, SupportAuthorType.Customer, "sub-1", "Still here")));

        await using var verifyContext = CreateContext(connectionString);
        var ticket = await verifyContext.Tickets.Include(value => value.Messages).SingleAsync(value => value.Id == ticketId);
        // Whichever operation actually observed the other's effect afterwards wins the final state,
        // but both writes must be present in the immutable thread and neither throws unhandled.
        Assert.Contains(ticket.Messages, value => value.AuthorType == SupportAuthorType.Customer && value.Body == "Still here");
        Assert.Contains(ticket.Messages, value => value.Body == "Ticket closed by support.");
    }

    [Fact]
    public async Task AReopenAndAReplyRacingForTheSameClosedTicketBothLandWithoutAnUnhandledException()
    {
        var (connectionString, keepAlive) = await CreateSharedDatabaseAsync();
        await using var _ = keepAlive;
        var ticketId = await CreateTicketAsync(connectionString, "sub-1");
        await CloseAsync(connectionString, ticketId, "staff-1");

        await Task.WhenAll(
            Task.Run(() => ReplyAsync(connectionString, ticketId, SupportAuthorType.Customer, "sub-1", "Reopening one")),
            Task.Run(() => ReplyAsync(connectionString, ticketId, SupportAuthorType.Guest, null, "Reopening two")));

        await using var verifyContext = CreateContext(connectionString);
        var ticket = await verifyContext.Tickets.Include(value => value.Messages).SingleAsync(value => value.Id == ticketId);
        Assert.Equal(SupportTicketStatus.WaitingOnSupport, ticket.Status);
        Assert.Null(ticket.ClosedAt);
        var sequenceNumbers = ticket.Messages.Select(value => value.SequenceNumber).ToList();
        Assert.Equal(sequenceNumbers.Distinct().Count(), sequenceNumbers.Count);
    }

    private static async Task AssertRepliesLandedWithUniqueSequencesAsync(string connectionString, Guid ticketId, int expectedReplyCount)
    {
        await using var verifyContext = CreateContext(connectionString);
        var ticket = await verifyContext.Tickets.Include(value => value.Messages).SingleAsync(value => value.Id == ticketId);
        // The initial message plus every reply, each with its own never-repeated sequence number:
        // no reply was silently lost and none collided with another's slot.
        Assert.Equal(1 + expectedReplyCount, ticket.Messages.Count);
        var sequenceNumbers = ticket.Messages.Select(value => value.SequenceNumber).ToList();
        Assert.Equal(sequenceNumbers.Distinct().Count(), sequenceNumbers.Count);
    }

    private static async Task<(string ConnectionString, SqliteConnection KeepAlive)> CreateSharedDatabaseAsync()
    {
        var connectionString = $"Data Source=file:reply-race-{Guid.CreateVersion7():N};Mode=Memory;Cache=Shared";
        var keepAlive = new SqliteConnection(connectionString);
        await keepAlive.OpenAsync();
        await using (var setupContext = CreateContext(connectionString))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        return (connectionString, keepAlive);
    }

    private static async Task<Guid> CreateTicketAsync(string connectionString, string? customerSubject)
    {
        await using var context = CreateContext(connectionString);
        var service = new SupportTicketService(context, new FixedTimeProvider(Now), new GuestAccessTokenService(context, new FixedTimeProvider(Now)));
        var contactEmail = customerSubject is null ? "guest@example.test" : "ari@example.test";
        var ticket = await service.CreateTicketAsync(new CreateSupportTicketCommand(
            customerSubject, "Ari", contactEmail, SupportTicketCategory.General, null, "Help please"));
        return ticket.Id;
    }

    private static async Task ReplyAsync(string connectionString, Guid ticketId, SupportAuthorType authorType, string? subject, string body)
    {
        await using var context = CreateContext(connectionString);
        var service = new SupportTicketService(context, new FixedTimeProvider(Now), new GuestAccessTokenService(context, new FixedTimeProvider(Now)));
        await service.AddMessageAsync(ticketId, authorType, subject, body);
    }

    private static async Task CloseAsync(string connectionString, Guid ticketId, string staffSubject)
    {
        await using var context = CreateContext(connectionString);
        var service = new SupportTicketService(context, new FixedTimeProvider(Now), new GuestAccessTokenService(context, new FixedTimeProvider(Now)));
        await service.CloseAsync(ticketId, staffSubject);
    }

    private static SupportDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(connectionString).Options);
}
