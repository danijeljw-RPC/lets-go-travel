using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Support.Application;
using ReadyToGoTravel.Support.Domain;
using ReadyToGoTravel.Support.Guest;
using ReadyToGoTravel.Support.Notifications;

namespace ReadyToGoTravel.Support.Tests;

public sealed class SupportNotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ADurableGuestAcknowledgementNeverPersistsTheRawToken()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var service = new SupportTicketService(fixture.Context, new FixedTimeProvider(Now));

        await service.CreateTicketAsync(new CreateSupportTicketCommand(
            null, "Guest", "guest@example.test", SupportTicketCategory.General, null, "Help please"));

        var outboxItems = await fixture.Context.SupportNotificationOutbox.ToListAsync();
        var item = Assert.Single(outboxItems);
        Assert.Equal("SupportTicketAcknowledgementGuest", item.Template);
        Assert.DoesNotContain("GuestToken", item.PayloadJson, StringComparison.Ordinal);
        Assert.Empty(fixture.Context.GuestAccessTokens);
    }

    [Fact]
    public async Task ProcessingAGuestAcknowledgementMintsAFreshTokenAndSendsItExactlyOnceWithoutPersistingIt()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var service = new SupportTicketService(fixture.Context, new FixedTimeProvider(Now));
        var ticket = await service.CreateTicketAsync(new CreateSupportTicketCommand(
            null, "Guest", "guest@example.test", SupportTicketCategory.General, null, "Help please"));
        var sender = new RecordingSupportNotificationSender();
        var tokens = new GuestAccessTokenService(fixture.Context, new FixedTimeProvider(Now));
        var processor = new SupportNotificationOutboxProcessor(fixture.Context, sender, tokens, new FixedTimeProvider(Now));

        await processor.ProcessNextAsync("worker-1");

        var sent = Assert.Single(sender.Sent);
        Assert.Contains("GuestToken", sent.PayloadJson, StringComparison.Ordinal);
        var occurrences = sent.PayloadJson.Split("GuestToken", StringSplitOptions.None).Length - 1;
        Assert.Equal(1, occurrences);

        var storedItem = await fixture.Context.SupportNotificationOutbox.SingleAsync(value => value.TicketId == ticket.Id);
        Assert.DoesNotContain("GuestToken", storedItem.PayloadJson, StringComparison.Ordinal);
        Assert.Single(fixture.Context.GuestAccessTokens);
    }

    [Fact]
    public async Task CreatingAnAuthenticatedTicketEnqueuesAnAcknowledgementWithNoToken()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var service = new SupportTicketService(fixture.Context, new FixedTimeProvider(Now));

        var ticket = await service.CreateTicketAsync(new CreateSupportTicketCommand(
            "sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help please"));

        var item = await fixture.Context.SupportNotificationOutbox.SingleAsync(value => value.TicketId == ticket.Id);
        Assert.Equal("SupportTicketAcknowledgementCustomer", item.Template);
        Assert.DoesNotContain("GuestToken", item.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheTicketAndItsAcknowledgementCommitInTheSameTransaction()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var service = new SupportTicketService(fixture.Context, new FixedTimeProvider(Now));

        var ticket = await service.CreateTicketAsync(new CreateSupportTicketCommand(
            "sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help please"));

        Assert.Equal(1, await fixture.Context.Tickets.CountAsync(value => value.Id == ticket.Id));
        Assert.Equal(1, await fixture.Context.SupportNotificationOutbox.CountAsync(value => value.TicketId == ticket.Id));
    }

    [Fact]
    public async Task EveryAcceptedReplyEnqueuesAnUpdateNotification()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var service = new SupportTicketService(fixture.Context, new FixedTimeProvider(Now));
        var ticket = await service.CreateTicketAsync(new CreateSupportTicketCommand(
            "sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help please"));

        await service.AddMessageAsync(ticket.Id, SupportAuthorType.Support, "staff-1", "We are on it.");

        var items = await fixture.Context.SupportNotificationOutbox
            .Where(value => value.TicketId == ticket.Id)
            .ToListAsync();
        Assert.Equal(2, items.Count);
        Assert.Contains(items, value => value.Template == "SupportTicketMessageAdded");
    }

    [Fact]
    public async Task TheRecordingSenderReceivesTheItemAfterProcessing()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var service = new SupportTicketService(fixture.Context, new FixedTimeProvider(Now));
        await service.CreateTicketAsync(new CreateSupportTicketCommand(
            "sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help please"));
        var sender = new RecordingSupportNotificationSender();
        var processor = CreateProcessor(fixture, sender, new FixedTimeProvider(Now));

        var didWork = await processor.ProcessNextAsync("worker-1");

        Assert.True(didWork);
        Assert.Single(sender.Sent);
    }

    [Fact]
    public async Task ASenderExceptionLeavesTheItemRetryableWithBoundedBackoff()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var service = new SupportTicketService(fixture.Context, new FixedTimeProvider(Now));
        await service.CreateTicketAsync(new CreateSupportTicketCommand(
            "sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help please"));
        var sender = new RecordingSupportNotificationSender
        {
            Behavior = _ => throw new InvalidOperationException("boom"),
        };
        var processor = CreateProcessor(fixture, sender, new FixedTimeProvider(Now));

        await processor.ProcessNextAsync("worker-1");

        var item = await fixture.Context.SupportNotificationOutbox.SingleAsync();
        Assert.Equal(SupportNotificationOutboxStatus.Pending, item.Status);
        Assert.Equal(1, item.Attempts);
    }

    [Fact]
    public async Task AfterEightExhaustedAttemptsTheItemIsMarkedFailedAndInspectable()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var service = new SupportTicketService(fixture.Context, new FixedTimeProvider(Now));
        await service.CreateTicketAsync(new CreateSupportTicketCommand(
            "sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help please"));
        var sender = new RecordingSupportNotificationSender
        {
            Behavior = _ => SupportNotificationSendResult.Retry("support_notification_capability_unavailable"),
        };
        var clock = new MutableTimeProvider(Now);

        for (var attempt = 1; attempt <= 8; attempt++)
        {
            var processor = CreateProcessor(fixture, sender, clock);
            await processor.ProcessNextAsync("worker-1");
            clock.Advance(TimeSpan.FromHours(2));
        }

        var item = await fixture.Context.SupportNotificationOutbox.SingleAsync();
        Assert.Equal(SupportNotificationOutboxStatus.Failed, item.Status);
    }

    [Fact]
    public async Task ACompletedItemIsNeverReprocessed()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var service = new SupportTicketService(fixture.Context, new FixedTimeProvider(Now));
        await service.CreateTicketAsync(new CreateSupportTicketCommand(
            "sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help please"));
        var sender = new RecordingSupportNotificationSender();
        var processor = CreateProcessor(fixture, sender, new FixedTimeProvider(Now));
        await processor.ProcessNextAsync("worker-1");

        var didWorkAgain = await processor.ProcessNextAsync("worker-1");

        Assert.False(didWorkAgain);
        Assert.Single(sender.Sent);
    }

    private static SupportNotificationOutboxProcessor CreateProcessor(
        SupportDatabaseFixture fixture,
        RecordingSupportNotificationSender sender,
        TimeProvider clock) =>
        new(fixture.Context, sender, new GuestAccessTokenService(fixture.Context, clock), clock);
}
