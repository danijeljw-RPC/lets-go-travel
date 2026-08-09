using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Booking.Bookings;
using ReadyToGoTravel.Booking.Checkout;
using ReadyToGoTravel.Booking.Payments;
using ReadyToGoTravel.Booking.Persistence;
using ReadyToGoTravel.Booking.Reconciliation;
using ReadyToGoTravel.Booking.Webhooks;

namespace ReadyToGoTravel.Booking.Tests;

public sealed class WebhookInboxProcessorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 8, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task KnownEventEnqueuesRetrievalWithoutMutatingBookingFromPayload()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var component = await StoreConfirmedHotelAsync(fixture.Context, "hotel_123");
        var clock = new FixedTimeProvider(Now);
        var writer = new WebhookInboxService(fixture.Context, clock);
        await writer.AcceptAsync(Input(
            "evt-known",
            "booking.cancel",
            "{\"booking_id\":\"hotel_123\",\"status\":\"cancelled\"}"));
        var scheduler = new ReconciliationScheduler(fixture.Context, clock);
        var processor = new WebhookInboxProcessor(fixture.Context, scheduler, clock);

        Assert.True(await processor.ProcessNextAsync("general-worker", default));

        var inbox = await fixture.Context.WebhookInbox.SingleAsync();
        var work = await fixture.Context.ReconciliationWork.SingleAsync();
        var storedComponent = await fixture.Context.ComponentBookings.SingleAsync();
        Assert.Equal(WebhookInboxStatus.Completed, inbox.Status);
        Assert.Equal(component.Id, work.ComponentBookingId);
        Assert.Equal(ComponentBookingStatus.Confirmed, storedComponent.Status);
        Assert.Empty(await fixture.Context.BookingVersions.ToArrayAsync());
    }

    [Fact]
    public async Task UnsupportedEventCreatesOneInspectableOperationalCase()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var clock = new FixedTimeProvider(Now);
        var writer = new WebhookInboxService(fixture.Context, clock);
        var input = Input("evt-unsupported", "supplier.new_event", "{}");
        await writer.AcceptAsync(input);
        var scheduler = new ReconciliationScheduler(fixture.Context, clock);
        var processor = new WebhookInboxProcessor(fixture.Context, scheduler, clock);

        Assert.True(await processor.ProcessNextAsync("general-worker", default));
        Assert.False(await processor.ProcessNextAsync("general-worker", default));

        Assert.Equal(WebhookInboxStatus.Completed, (await fixture.Context.WebhookInbox.SingleAsync()).Status);
        var operationalCase = await fixture.Context.OperationalCases.SingleAsync();
        Assert.Equal("UnsupportedWebhook", operationalCase.Category);
        Assert.Equal("unsupported_webhook_event", operationalCase.Reason);
        Assert.Empty(await fixture.Context.ReconciliationWork.ToArrayAsync());
    }

    [Fact]
    public async Task MalformedNestedPayloadIsQuarantinedAfterDurableReceipt()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var clock = new FixedTimeProvider(Now);
        var writer = new WebhookInboxService(fixture.Context, clock);
        await writer.AcceptAsync(Input("evt-poison", "booking.book", "not-json"));
        var scheduler = new ReconciliationScheduler(fixture.Context, clock);
        var processor = new WebhookInboxProcessor(fixture.Context, scheduler, clock);

        Assert.True(await processor.ProcessNextAsync("general-worker", default));

        var inbox = await fixture.Context.WebhookInbox.SingleAsync();
        Assert.Equal(WebhookInboxStatus.Quarantined, inbox.Status);
        Assert.Equal("webhook_nested_payload_invalid", inbox.ErrorCode);
        Assert.Empty(await fixture.Context.ReconciliationWork.ToArrayAsync());
        var operationalCase = await fixture.Context.OperationalCases.SingleAsync();
        Assert.Equal("WebhookPayload", operationalCase.Category);
        Assert.Equal("webhook_nested_payload_invalid", operationalCase.Reason);
    }

    [Fact]
    public async Task TransientSchedulingFailureLeavesInboxReadyForRetry()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        await StoreConfirmedHotelAsync(fixture.Context, "hotel_123");
        var clock = new FixedTimeProvider(Now);
        var writer = new WebhookInboxService(fixture.Context, clock);
        await writer.AcceptAsync(Input("evt-retry", "booking.book", "{\"booking_id\":\"hotel_123\"}"));
        var processor = new WebhookInboxProcessor(fixture.Context, new ThrowingScheduler(), clock);

        Assert.True(await processor.ProcessNextAsync("general-worker", default));

        var inbox = await fixture.Context.WebhookInbox.SingleAsync();
        Assert.Equal(WebhookInboxStatus.Retrying, inbox.Status);
        Assert.Equal("reconciliation_enqueue_failed", inbox.ErrorCode);
        Assert.True(inbox.NextAttemptAtUtc > Now.UtcDateTime);
    }

    [Fact]
    public async Task SchedulingStopsRetryingAfterEightAttempts()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        await StoreConfirmedHotelAsync(fixture.Context, "hotel_123");
        var clock = new FixedTimeProvider(Now);
        var writer = new WebhookInboxService(fixture.Context, clock);
        await writer.AcceptAsync(Input("evt-retry-limit", "booking.book", "{\"booking_id\":\"hotel_123\"}"));
        await fixture.Context.WebhookInbox.ExecuteUpdateAsync(
            setters => setters.SetProperty(value => value.Attempts, 7));
        fixture.Context.ChangeTracker.Clear();
        var processor = new WebhookInboxProcessor(fixture.Context, new ThrowingScheduler(), clock);

        Assert.True(await processor.ProcessNextAsync("general-worker", default));

        var inbox = await fixture.Context.WebhookInbox.SingleAsync();
        Assert.Equal(WebhookInboxStatus.Quarantined, inbox.Status);
        Assert.Equal("reconciliation_enqueue_retry_exhausted", inbox.ErrorCode);
        Assert.Equal(8, inbox.Attempts);
        var operationalCase = await fixture.Context.OperationalCases.SingleAsync();
        Assert.Equal("WebhookScheduling", operationalCase.Category);
        Assert.Equal("reconciliation_enqueue_retry_exhausted", operationalCase.Reason);
    }

    [Fact]
    public async Task RepeatedSchedulingExhaustionReusesTheOperationalCase()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        await StoreConfirmedHotelAsync(fixture.Context, "hotel_123");
        var clock = new FixedTimeProvider(Now);
        var writer = new WebhookInboxService(fixture.Context, clock);
        await writer.AcceptAsync(Input("evt-retry-limit", "booking.book", "{\"booking_id\":\"hotel_123\"}"));
        await fixture.Context.WebhookInbox.ExecuteUpdateAsync(
            setters => setters.SetProperty(value => value.Attempts, 7));
        fixture.Context.ChangeTracker.Clear();
        var processor = new WebhookInboxProcessor(fixture.Context, new ThrowingScheduler(), clock);
        Assert.True(await processor.ProcessNextAsync("general-worker", default));
        var quarantined = await fixture.Context.WebhookInbox.SingleAsync();

        Assert.True(await processor.RequeueAsync(quarantined.Id, default));
        var requeued = await fixture.Context.WebhookInbox.SingleAsync();
        Assert.Equal(WebhookInboxStatus.Pending, requeued.Status);
        Assert.Equal(0, requeued.Attempts);
        await fixture.Context.WebhookInbox.ExecuteUpdateAsync(
            setters => setters.SetProperty(value => value.Attempts, 7));
        fixture.Context.ChangeTracker.Clear();

        Assert.True(await processor.ProcessNextAsync("general-worker", default));

        Assert.Equal(WebhookInboxStatus.Quarantined, (await fixture.Context.WebhookInbox.SingleAsync()).Status);
        Assert.Equal(1, await fixture.Context.OperationalCases.CountAsync());
    }

    [Fact]
    public async Task RequeueIgnoresItemsThatAreNotQuarantined()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        await StoreConfirmedHotelAsync(fixture.Context, "hotel_123");
        var clock = new FixedTimeProvider(Now);
        var writer = new WebhookInboxService(fixture.Context, clock);
        await writer.AcceptAsync(Input("evt-known", "booking.cancel", "{\"booking_id\":\"hotel_123\"}"));
        var scheduler = new ReconciliationScheduler(fixture.Context, clock);
        var processor = new WebhookInboxProcessor(fixture.Context, scheduler, clock);
        Assert.True(await processor.ProcessNextAsync("general-worker", default));
        var completed = await fixture.Context.WebhookInbox.SingleAsync();
        Assert.Equal(WebhookInboxStatus.Completed, completed.Status);

        Assert.False(await processor.RequeueAsync(completed.Id, default));

        Assert.Equal(WebhookInboxStatus.Completed, (await fixture.Context.WebhookInbox.SingleAsync()).Status);
    }

    [Fact]
    public async Task MaximumLengthEventIdentityCreatesABoundedOperationalCaseKey()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var eventId = new string('e', 255);
        var clock = new FixedTimeProvider(Now);
        var writer = new WebhookInboxService(fixture.Context, clock);
        await writer.AcceptAsync(Input(eventId, "supplier.new_event", "{}"));
        var processor = new WebhookInboxProcessor(
            fixture.Context,
            new ReconciliationScheduler(fixture.Context, clock),
            clock);

        Assert.True(await processor.ProcessNextAsync("general-worker", default));

        var operationalCase = await fixture.Context.OperationalCases.SingleAsync();
        Assert.True(operationalCase.DedupeKey.Length <= 180);
    }

    private static WebhookEnvelopeInput Input(string eventId, string eventName, string response) => new(
        "Sandbox",
        eventId,
        eventName,
        $$"""
        {
          "event_id": "{{eventId}}",
          "event_name": "{{eventName}}",
          "request": "{}",
          "response": {{System.Text.Json.JsonSerializer.Serialize(response)}},
          "sandbox": true
        }
        """,
        true,
        $"correlation-{eventId}");

    private static async Task<ComponentBooking> StoreConfirmedHotelAsync(
        BookingDbContext database,
        string reference)
    {
        var clock = new FixedTimeProvider(Now);
        var result = CheckoutSession.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            [new ResolvedCheckoutOffer(
                CheckoutProduct.Hotel,
                "hotel-1",
                "fixture-hotel",
                "Harbour Lane Hotel",
                420m,
                "AUD",
                "terms-hotel-v1",
                "hotel-r1",
                Now.AddHours(1),
                Now)],
            [new TravellerSnapshot("hotel-1", Guid.CreateVersion7(), "Ari", "Taylor", false, null)],
            clock);
        Assert.True(result.IsSuccess);
        var checkout = result.Value!;
        Assert.True(checkout.AcceptRevision(
            1,
            420m,
            "AUD",
            checkout.CurrentRevision.TermsHash,
            CheckoutAcceptancePolicy.CurrentVersion,
            clock).IsSuccess);
        Assert.True(checkout.BeginPayment("fixture", "payment_123", clock).IsSuccess);
        Assert.True(checkout.RecordPayment(PaymentProviderResult.Captured("return_123"), clock).IsSuccess);
        Assert.True(checkout.BeginBooking(clock).IsSuccess);
        var component = checkout.Components.Single();
        Assert.True(checkout.RecordBookingResult(component.Id, BookingProviderResult.Confirmed(reference), clock).IsSuccess);
        database.Checkouts.Add(checkout);
        await database.SaveChangesAsync();
        return component;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class ThrowingScheduler : IReconciliationScheduler
    {
        public Task EnqueueImmediateAsync(
            Guid componentBookingId,
            string source,
            string correlationId,
            CancellationToken cancellationToken = default) =>
            Task.FromException(new TimeoutException("database temporarily unavailable"));
    }
}
