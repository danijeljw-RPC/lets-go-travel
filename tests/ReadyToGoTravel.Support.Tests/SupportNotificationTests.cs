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
        var service = new SupportTicketService(
            fixture.Context, new FixedTimeProvider(Now), new GuestAccessTokenService(fixture.Context, new FixedTimeProvider(Now)));

        await service.CreateTicketAsync(new CreateSupportTicketCommand(
            null, "Guest", "guest@example.test", SupportTicketCategory.General, null, "Help please"));

        var outboxItems = await fixture.Context.SupportNotificationOutbox.ToListAsync();
        var item = Assert.Single(outboxItems);
        Assert.Equal("SupportTicketAcknowledgementGuest", item.Template);
        Assert.DoesNotContain("GuestToken", item.PayloadJson, StringComparison.Ordinal);

        // Ticket creation stages a hash-only placeholder token (owned by the acknowledgement item)
        // atomically with the ticket, closing the revoke-before-first-token race; its raw value was
        // never generated into the payload above and is discarded once GuestAccessTokenGenerator
        // returns, so this still proves no raw token is durably persisted.
        var stagedToken = Assert.Single(fixture.Context.GuestAccessTokens);
        Assert.Equal(item.Id, stagedToken.IssuedForOutboxItemId);
        Assert.Null(stagedToken.RevokedAt);
    }

    [Fact]
    public async Task ProcessingAGuestAcknowledgementMintsAFreshTokenAndSendsItExactlyOnceWithoutPersistingIt()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var service = new SupportTicketService(
            fixture.Context, new FixedTimeProvider(Now), new GuestAccessTokenService(fixture.Context, new FixedTimeProvider(Now)));
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

        // The synchronous placeholder from creation was rotated away (revoked, superseded by the
        // real deliverable token), so two rows exist: the spent placeholder and the active token.
        var storedTokens = await fixture.Context.GuestAccessTokens.Where(value => value.TicketId == ticket.Id).ToListAsync();
        Assert.Equal(2, storedTokens.Count);
        Assert.Single(storedTokens, value => value.RevokedAt == null);
    }

    [Fact]
    public async Task CreatingAnAuthenticatedTicketEnqueuesAnAcknowledgementWithNoToken()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var service = new SupportTicketService(
            fixture.Context, new FixedTimeProvider(Now), new GuestAccessTokenService(fixture.Context, new FixedTimeProvider(Now)));

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
        var service = new SupportTicketService(
            fixture.Context, new FixedTimeProvider(Now), new GuestAccessTokenService(fixture.Context, new FixedTimeProvider(Now)));

        var ticket = await service.CreateTicketAsync(new CreateSupportTicketCommand(
            "sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help please"));

        Assert.Equal(1, await fixture.Context.Tickets.CountAsync(value => value.Id == ticket.Id));
        Assert.Equal(1, await fixture.Context.SupportNotificationOutbox.CountAsync(value => value.TicketId == ticket.Id));
    }

    [Fact]
    public async Task EveryAcceptedReplyEnqueuesAnUpdateNotification()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var service = new SupportTicketService(
            fixture.Context, new FixedTimeProvider(Now), new GuestAccessTokenService(fixture.Context, new FixedTimeProvider(Now)));
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
        var service = new SupportTicketService(
            fixture.Context, new FixedTimeProvider(Now), new GuestAccessTokenService(fixture.Context, new FixedTimeProvider(Now)));
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
        var service = new SupportTicketService(
            fixture.Context, new FixedTimeProvider(Now), new GuestAccessTokenService(fixture.Context, new FixedTimeProvider(Now)));
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
        var service = new SupportTicketService(
            fixture.Context, new FixedTimeProvider(Now), new GuestAccessTokenService(fixture.Context, new FixedTimeProvider(Now)));
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
        var service = new SupportTicketService(
            fixture.Context, new FixedTimeProvider(Now), new GuestAccessTokenService(fixture.Context, new FixedTimeProvider(Now)));
        await service.CreateTicketAsync(new CreateSupportTicketCommand(
            "sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help please"));
        var sender = new RecordingSupportNotificationSender();
        var processor = CreateProcessor(fixture, sender, new FixedTimeProvider(Now));
        await processor.ProcessNextAsync("worker-1");

        var didWorkAgain = await processor.ProcessNextAsync("worker-1");

        Assert.False(didWorkAgain);
        Assert.Single(sender.Sent);
    }

    [Fact]
    public async Task StaffRevokingBeforeTheQueuedAcknowledgementIsProcessedCancelsItWithoutIssuingAToken()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var clock = new FixedTimeProvider(Now);
        var tokens = new GuestAccessTokenService(fixture.Context, clock);
        var service = new SupportTicketService(fixture.Context, clock, tokens);
        var ticket = await service.CreateTicketAsync(new CreateSupportTicketCommand(
            null, "Guest", "guest@example.test", SupportTicketCategory.General, null, "Help please"));

        await tokens.RevokeAsync(ticket.Id, actorSubject: "staff-1");

        var sender = new RecordingSupportNotificationSender();
        var processor = new SupportNotificationOutboxProcessor(fixture.Context, sender, tokens, clock);
        var didWork = await processor.ProcessNextAsync("worker-1");

        Assert.True(didWork);
        Assert.Empty(sender.Sent);
        var item = await fixture.Context.SupportNotificationOutbox.SingleAsync(value => value.TicketId == ticket.Id);
        Assert.Equal(SupportNotificationOutboxStatus.Cancelled, item.Status);
        Assert.Empty(await fixture.Context.GuestAccessTokens
            .Where(value => value.TicketId == ticket.Id && value.RevokedAt == null).ToListAsync());
    }

    [Fact]
    public async Task StaffRevokingAfterAFailedSendAttemptCancelsTheRetryWithoutIssuingANewToken()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var clock = new MutableTimeProvider(Now);
        var tokens = new GuestAccessTokenService(fixture.Context, clock);
        var service = new SupportTicketService(fixture.Context, clock, tokens);
        var ticket = await service.CreateTicketAsync(new CreateSupportTicketCommand(
            null, "Guest", "guest@example.test", SupportTicketCategory.General, null, "Help please"));
        var sender = new RecordingSupportNotificationSender
        {
            Behavior = _ => SupportNotificationSendResult.Retry("support_notification_delivery_failed"),
        };
        var processor = new SupportNotificationOutboxProcessor(fixture.Context, sender, tokens, clock);
        await processor.ProcessNextAsync("worker-1");
        Assert.Single(sender.Sent);
        Assert.Equal(2, await fixture.Context.GuestAccessTokens.CountAsync(value => value.TicketId == ticket.Id));

        await tokens.RevokeAsync(ticket.Id, actorSubject: "staff-1");
        clock.Advance(TimeSpan.FromMinutes(5));
        sender.Behavior = null;
        var retried = await processor.ProcessNextAsync("worker-1");

        Assert.True(retried);
        Assert.Single(sender.Sent);
        var item = await fixture.Context.SupportNotificationOutbox.SingleAsync(value => value.TicketId == ticket.Id);
        Assert.Equal(SupportNotificationOutboxStatus.Cancelled, item.Status);
        Assert.Empty(await fixture.Context.GuestAccessTokens
            .Where(value => value.TicketId == ticket.Id && value.RevokedAt == null).ToListAsync());
    }

    [Fact]
    public async Task StaffRotatingBeforeTheQueuedAcknowledgementIsProcessedCancelsItAndPreservesStaffsLink()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var clock = new FixedTimeProvider(Now);
        var tokens = new GuestAccessTokenService(fixture.Context, clock);
        var service = new SupportTicketService(fixture.Context, clock, tokens);
        var ticket = await service.CreateTicketAsync(new CreateSupportTicketCommand(
            null, "Guest", "guest@example.test", SupportTicketCategory.General, null, "Help please"));

        var staffToken = await tokens.RotateAsync(ticket.Id, actorSubject: "staff-1");

        var sender = new RecordingSupportNotificationSender();
        var processor = new SupportNotificationOutboxProcessor(fixture.Context, sender, tokens, clock);
        var didWork = await processor.ProcessNextAsync("worker-1");

        Assert.True(didWork);
        Assert.Empty(sender.Sent);
        var item = await fixture.Context.SupportNotificationOutbox.SingleAsync(value => value.TicketId == ticket.Id);
        Assert.Equal(SupportNotificationOutboxStatus.Cancelled, item.Status);
        Assert.Equal(ticket.Id, await tokens.ResolveAsync(staffToken));
    }

    [Fact]
    public async Task StaffRotatingAfterTheAcknowledgementMintedItsOwnTokenCancelsTheStaleRetryAndPreservesStaffsLink()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var clock = new MutableTimeProvider(Now);
        var tokens = new GuestAccessTokenService(fixture.Context, clock);
        var service = new SupportTicketService(fixture.Context, clock, tokens);
        var ticket = await service.CreateTicketAsync(new CreateSupportTicketCommand(
            null, "Guest", "guest@example.test", SupportTicketCategory.General, null, "Help please"));
        var sender = new RecordingSupportNotificationSender
        {
            Behavior = _ => SupportNotificationSendResult.Retry("support_notification_delivery_failed"),
        };
        var processor = new SupportNotificationOutboxProcessor(fixture.Context, sender, tokens, clock);
        await processor.ProcessNextAsync("worker-1");
        Assert.Single(sender.Sent);

        var staffToken = await tokens.RotateAsync(ticket.Id, actorSubject: "staff-1");
        clock.Advance(TimeSpan.FromMinutes(5));
        sender.Behavior = null;
        var retried = await processor.ProcessNextAsync("worker-1");

        Assert.True(retried);
        Assert.Single(sender.Sent);
        var item = await fixture.Context.SupportNotificationOutbox.SingleAsync(value => value.TicketId == ticket.Id);
        Assert.Equal(SupportNotificationOutboxStatus.Cancelled, item.Status);
        Assert.Equal(ticket.Id, await tokens.ResolveAsync(staffToken));
    }

    [Fact]
    public async Task ANormalInitialGuestAcknowledgementWithNoStaffInterferenceDeliversAWorkingLink()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var clock = new FixedTimeProvider(Now);
        var tokens = new GuestAccessTokenService(fixture.Context, clock);
        var service = new SupportTicketService(fixture.Context, clock, tokens);
        var ticket = await service.CreateTicketAsync(new CreateSupportTicketCommand(
            null, "Guest", "guest@example.test", SupportTicketCategory.General, null, "Help please"));
        var sender = new RecordingSupportNotificationSender();
        var processor = new SupportNotificationOutboxProcessor(fixture.Context, sender, tokens, clock);

        var didWork = await processor.ProcessNextAsync("worker-1");

        Assert.True(didWork);
        var delivered = Assert.Single(sender.Sent);
        var item = await fixture.Context.SupportNotificationOutbox.SingleAsync(value => value.TicketId == ticket.Id);
        Assert.Equal(SupportNotificationOutboxStatus.Sent, item.Status);
        using var document = System.Text.Json.JsonDocument.Parse(delivered.PayloadJson);
        var token = document.RootElement.GetProperty("GuestToken").GetString()!;
        Assert.Equal(ticket.Id, await tokens.ResolveAsync(token));
    }

    [Fact]
    public async Task ARetryAfterATransientFailureWithNoStaffInterferenceStillDeliversExactlyOneWorkingLink()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var clock = new MutableTimeProvider(Now);
        var tokens = new GuestAccessTokenService(fixture.Context, clock);
        var service = new SupportTicketService(fixture.Context, clock, tokens);
        var ticket = await service.CreateTicketAsync(new CreateSupportTicketCommand(
            null, "Guest", "guest@example.test", SupportTicketCategory.General, null, "Help please"));
        var sender = new RecordingSupportNotificationSender
        {
            Behavior = _ => SupportNotificationSendResult.Retry("support_notification_delivery_failed"),
        };
        var processor = new SupportNotificationOutboxProcessor(fixture.Context, sender, tokens, clock);
        await processor.ProcessNextAsync("worker-1");
        clock.Advance(TimeSpan.FromMinutes(5));
        sender.Behavior = null;

        var retried = await processor.ProcessNextAsync("worker-1");

        Assert.True(retried);
        Assert.Equal(2, sender.Sent.Count);
        var item = await fixture.Context.SupportNotificationOutbox.SingleAsync(value => value.TicketId == ticket.Id);
        Assert.Equal(SupportNotificationOutboxStatus.Sent, item.Status);
        using var document = System.Text.Json.JsonDocument.Parse(sender.Sent[^1].PayloadJson);
        var token = document.RootElement.GetProperty("GuestToken").GetString()!;
        Assert.Equal(ticket.Id, await tokens.ResolveAsync(token));

        var active = await fixture.Context.GuestAccessTokens
            .Where(value => value.TicketId == ticket.Id && value.RevokedAt == null).ToListAsync();
        Assert.Single(active);

        // A retry of the same logical notification must never rotate the credential: both attempts
        // embed the exact same token, and the same DedupeKey, so a provider deduplicating the retry
        // never strands the guest with an invalidated first copy.
        Assert.Equal(ExtractGuestToken(sender.Sent[0].PayloadJson), token);
        Assert.Equal(sender.Sent[0].DedupeKey, sender.Sent[1].DedupeKey);
    }

    [Fact]
    public async Task ASuccessfulDeliveryFollowedByALostAcknowledgementRetriesWithTheIdenticalTokenSoTheAlreadyDeliveredCopyStaysValid()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var clock = new MutableTimeProvider(Now);
        var tokens = new GuestAccessTokenService(fixture.Context, clock);
        var service = new SupportTicketService(fixture.Context, clock, tokens);
        var ticket = await service.CreateTicketAsync(new CreateSupportTicketCommand(
            null, "Guest", "guest@example.test", SupportTicketCategory.General, null, "Help please"));

        // Simulates a provider that actually delivered the email to the guest, but whose delivery
        // acknowledgement never reached us (e.g. a timeout reading the response) - the outbox
        // worker sees this as a transient failure and will retry, even though the guest already
        // holds a working copy of the token embedded in this exact attempt.
        var sender = new RecordingSupportNotificationSender
        {
            Behavior = _ => throw new TimeoutException("acknowledgement lost"),
        };
        var processor = new SupportNotificationOutboxProcessor(fixture.Context, sender, tokens, clock);
        await processor.ProcessNextAsync("worker-1");
        var firstDeliveredToken = ExtractGuestToken(Assert.Single(sender.Sent).PayloadJson);

        // The guest genuinely received firstDeliveredToken; it must resolve before any retry too.
        Assert.Equal(ticket.Id, await tokens.ResolveAsync(firstDeliveredToken));

        clock.Advance(TimeSpan.FromMinutes(5));
        sender.Behavior = null;
        var retried = await processor.ProcessNextAsync("worker-1");

        Assert.True(retried);
        Assert.Equal(2, sender.Sent.Count);
        var secondAttemptToken = ExtractGuestToken(sender.Sent[^1].PayloadJson);
        Assert.Equal(firstDeliveredToken, secondAttemptToken);
        Assert.Equal(sender.Sent[0].DedupeKey, sender.Sent[1].DedupeKey);

        // Whether or not a real provider actually resent (it may have deduplicated the retry using
        // our stable DedupeKey and never resent at all), the token the guest already holds from the
        // first, successfully delivered attempt is still valid.
        Assert.Equal(ticket.Id, await tokens.ResolveAsync(firstDeliveredToken));
        var item = await fixture.Context.SupportNotificationOutbox.SingleAsync(value => value.TicketId == ticket.Id);
        Assert.Equal(SupportNotificationOutboxStatus.Sent, item.Status);
        var active = await fixture.Context.GuestAccessTokens
            .Where(value => value.TicketId == ticket.Id && value.RevokedAt == null).ToListAsync();
        Assert.Single(active);

        // Exactly one non-placeholder mint ever happened for this outbox item: the placeholder
        // staged at ticket creation, and the one deterministic token reused by every attempt.
        Assert.Equal(2, await fixture.Context.GuestAccessTokens.CountAsync(value => value.TicketId == ticket.Id));
    }

    [Fact]
    public async Task AThirdAttemptAfterTwoLostAcknowledgementsStillReusesTheSameToken()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var clock = new MutableTimeProvider(Now);
        var tokens = new GuestAccessTokenService(fixture.Context, clock);
        var service = new SupportTicketService(fixture.Context, clock, tokens);
        await service.CreateTicketAsync(new CreateSupportTicketCommand(
            null, "Guest", "guest@example.test", SupportTicketCategory.General, null, "Help please"));
        var sender = new RecordingSupportNotificationSender
        {
            Behavior = _ => SupportNotificationSendResult.Retry("support_notification_delivery_failed"),
        };
        var processor = new SupportNotificationOutboxProcessor(fixture.Context, sender, tokens, clock);

        await processor.ProcessNextAsync("worker-1");
        clock.Advance(TimeSpan.FromMinutes(5));
        await processor.ProcessNextAsync("worker-1");
        clock.Advance(TimeSpan.FromMinutes(10));
        sender.Behavior = null;
        await processor.ProcessNextAsync("worker-1");

        Assert.Equal(3, sender.Sent.Count);
        var distinctTokens = sender.Sent.Select(value => ExtractGuestToken(value.PayloadJson)).Distinct().ToList();
        Assert.Single(distinctTokens);
    }

    [Fact]
    public async Task APermanentFailureBecomesTerminalOnTheFirstAttemptWithoutFurtherRetries()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var service = new SupportTicketService(
            fixture.Context, new FixedTimeProvider(Now), new GuestAccessTokenService(fixture.Context, new FixedTimeProvider(Now)));
        await service.CreateTicketAsync(new CreateSupportTicketCommand(
            "sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help please"));
        var sender = new RecordingSupportNotificationSender
        {
            Behavior = _ => SupportNotificationSendResult.Permanent("support_recipient_rejected"),
        };
        var processor = CreateProcessor(fixture, sender, new FixedTimeProvider(Now));

        var didWork = await processor.ProcessNextAsync("worker-1");

        Assert.True(didWork);
        Assert.Single(sender.Sent);
        var item = await fixture.Context.SupportNotificationOutbox.SingleAsync();
        Assert.Equal(SupportNotificationOutboxStatus.Failed, item.Status);
        Assert.Equal("support_recipient_rejected", item.ErrorCode);
        Assert.Equal(1, item.Attempts);

        // Terminal means terminal: a later worker cycle must never pick this item back up.
        var pickedUpAgain = await processor.ProcessNextAsync("worker-1");
        Assert.False(pickedUpAgain);
    }

    [Fact]
    public async Task ARetryableFailureIsScheduledForRetryRatherThanFailedImmediately()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var service = new SupportTicketService(
            fixture.Context, new FixedTimeProvider(Now), new GuestAccessTokenService(fixture.Context, new FixedTimeProvider(Now)));
        await service.CreateTicketAsync(new CreateSupportTicketCommand(
            "sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help please"));
        var sender = new RecordingSupportNotificationSender
        {
            Behavior = _ => SupportNotificationSendResult.Retry("support_notification_delivery_failed"),
        };
        var processor = CreateProcessor(fixture, sender, new FixedTimeProvider(Now));

        await processor.ProcessNextAsync("worker-1");

        var item = await fixture.Context.SupportNotificationOutbox.SingleAsync();
        Assert.Equal(SupportNotificationOutboxStatus.Pending, item.Status);
        Assert.Equal(1, item.Attempts);
    }

    [Fact]
    public async Task ASuccessfulSendMarksTheItemSent()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var service = new SupportTicketService(
            fixture.Context, new FixedTimeProvider(Now), new GuestAccessTokenService(fixture.Context, new FixedTimeProvider(Now)));
        await service.CreateTicketAsync(new CreateSupportTicketCommand(
            "sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help please"));
        var sender = new RecordingSupportNotificationSender();
        var processor = CreateProcessor(fixture, sender, new FixedTimeProvider(Now));

        await processor.ProcessNextAsync("worker-1");

        var item = await fixture.Context.SupportNotificationOutbox.SingleAsync();
        Assert.Equal(SupportNotificationOutboxStatus.Sent, item.Status);
        Assert.Null(item.ErrorCode);
    }

    [Fact]
    public async Task APermanentFailureAfterSeveralRetryableFailuresStillBecomesTerminalImmediately()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var service = new SupportTicketService(
            fixture.Context, new FixedTimeProvider(Now), new GuestAccessTokenService(fixture.Context, new FixedTimeProvider(Now)));
        await service.CreateTicketAsync(new CreateSupportTicketCommand(
            "sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help please"));
        var sender = new RecordingSupportNotificationSender
        {
            Behavior = _ => SupportNotificationSendResult.Retry("support_notification_delivery_failed"),
        };
        var clock = new MutableTimeProvider(Now);
        var processor = CreateProcessor(fixture, sender, clock);
        await processor.ProcessNextAsync("worker-1");
        clock.Advance(TimeSpan.FromMinutes(5));
        sender.Behavior = _ => SupportNotificationSendResult.Permanent("support_recipient_rejected");

        await processor.ProcessNextAsync("worker-1");

        var item = await fixture.Context.SupportNotificationOutbox.SingleAsync();
        Assert.Equal(SupportNotificationOutboxStatus.Failed, item.Status);
        Assert.Equal(2, item.Attempts);
    }

    private static string ExtractGuestToken(string payloadJson)
    {
        using var document = System.Text.Json.JsonDocument.Parse(payloadJson);
        return document.RootElement.GetProperty("GuestToken").GetString()!;
    }

    private static SupportNotificationOutboxProcessor CreateProcessor(
        SupportDatabaseFixture fixture,
        RecordingSupportNotificationSender sender,
        TimeProvider clock) =>
        new(fixture.Context, sender, new GuestAccessTokenService(fixture.Context, clock), clock);
}
