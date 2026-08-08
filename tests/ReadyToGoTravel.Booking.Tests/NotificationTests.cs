using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Booking.Bookings;
using ReadyToGoTravel.Booking.Checkout;
using ReadyToGoTravel.Booking.Notifications;
using ReadyToGoTravel.Booking.Payments;
using ReadyToGoTravel.Booking.Persistence;
using ReadyToGoTravel.Booking.Reconciliation;

namespace ReadyToGoTravel.Booking.Tests;

public sealed class NotificationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 8, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public void InitialVersionDoesNotNotify()
    {
        var component = CreateConfirmedFlight();
        var version = CanonicalBookingVersioner.Create(
            component,
            FlightState(Now.AddDays(3)),
            null,
            Now,
            "Initial",
            "correlation-1");

        var classification = BookingChangeClassifier.Classify(null, version);

        Assert.Equal(BookingChangeSeverity.Informational, classification.Severity);
        Assert.False(classification.ShouldEmail);
    }

    [Theory]
    [InlineData(15, BookingChangeSeverity.Minor)]
    [InlineData(29, BookingChangeSeverity.Minor)]
    [InlineData(30, BookingChangeSeverity.Material)]
    [InlineData(90, BookingChangeSeverity.Material)]
    public void FlightTimeMaterialityUsesThirtyMinuteBoundary(
        int minutes,
        BookingChangeSeverity expected)
    {
        var component = CreateConfirmedFlight();
        var initial = CanonicalBookingVersioner.Create(
            component,
            FlightState(Now.AddDays(3)),
            null,
            Now,
            "Initial",
            "correlation-1");
        Assert.True(component.ApplyReconciliationVersion(initial, Now));
        var changed = CanonicalBookingVersioner.Create(
            component,
            FlightState(Now.AddDays(3).AddMinutes(minutes)),
            initial,
            Now.AddHours(1),
            "Scheduled",
            "correlation-2");

        var classification = BookingChangeClassifier.Classify(initial, changed);

        Assert.Equal(expected, classification.Severity);
        Assert.True(classification.ShouldEmail);
    }

    [Fact]
    public void CancellationIsTravelBlockingAndBypassesQuietHours()
    {
        var component = CreateConfirmedFlight();
        var initial = CanonicalBookingVersioner.Create(
            component,
            FlightState(Now.AddHours(12)),
            null,
            Now,
            "Initial",
            "correlation-1");
        Assert.True(component.ApplyReconciliationVersion(initial, Now));
        var cancelled = CanonicalBookingVersioner.Create(
            component,
            FlightState(Now.AddHours(12)) with { Status = RetrievedBookingStatus.Cancelled },
            initial,
            Now.AddMinutes(10),
            "Webhook",
            "correlation-2");

        var classification = BookingChangeClassifier.Classify(initial, cancelled);
        var item = NotificationOutboxItem.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            component.Id,
            cancelled.Id,
            classification,
            "en-AU",
            "Australia/Sydney",
            cancelled.DiffJson,
            new DateTimeOffset(2026, 8, 8, 13, 0, 0, TimeSpan.Zero));

        Assert.Equal(BookingChangeSeverity.TravelBlocking, classification.Severity);
        Assert.Equal(new DateTimeOffset(2026, 8, 8, 13, 0, 0, TimeSpan.Zero), item.NotBefore);
    }

    [Fact]
    public void PropertyRelocationIsTravelBlocking()
    {
        var component = CheckoutFactory.CreateWithTraveller("Ari", "Taylor").Components.Single();
        var initial = CanonicalBookingVersioner.Create(
            component,
            HotelState("Harbour Lane Hotel"),
            null,
            Now,
            "Initial",
            "correlation-1");
        Assert.True(component.ApplyReconciliationVersion(initial, Now));
        var relocated = CanonicalBookingVersioner.Create(
            component,
            HotelState("Airport Recovery Hotel"),
            initial,
            Now.AddHours(1),
            "Webhook",
            "correlation-2");

        var classification = BookingChangeClassifier.Classify(initial, relocated);

        Assert.Equal(BookingChangeSeverity.TravelBlocking, classification.Severity);
        Assert.True(classification.ShouldEmail);
    }

    [Fact]
    public void FlightArrivalMovementUsesTheSameThirtyMinuteBoundary()
    {
        var component = CreateConfirmedFlight();
        var initialState = FlightState(Now.AddDays(3));
        var initial = CanonicalBookingVersioner.Create(
            component,
            initialState,
            null,
            Now,
            "Initial",
            "correlation-1");
        Assert.True(component.ApplyReconciliationVersion(initial, Now));
        var segment = initialState.FlightSegments.Single();
        var changedState = initialState with
        {
            FlightSegments = [segment with { ScheduledArrival = segment.ScheduledArrival.AddMinutes(30) }],
        };
        var changed = CanonicalBookingVersioner.Create(
            component,
            changedState,
            initial,
            Now.AddHours(1),
            "Scheduled",
            "correlation-2");

        var classification = BookingChangeClassifier.Classify(initial, changed);

        Assert.Equal(BookingChangeSeverity.Material, classification.Severity);
        Assert.True(classification.ShouldEmail);
    }

    [Fact]
    public void MinorEmailWaitsUntilSevenAfterCustomerLocalQuietHours()
    {
        var observedAt = new DateTimeOffset(2026, 8, 8, 13, 0, 0, TimeSpan.Zero);

        var item = NotificationOutboxItem.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            new BookingChangeClassification(BookingChangeSeverity.Minor, true, ["schedule"]),
            "en-AU",
            "Australia/Sydney",
            "[]",
            observedAt);

        Assert.Equal(new DateTimeOffset(2026, 8, 8, 21, 0, 0, TimeSpan.Zero), item.NotBefore);
    }

    [Fact]
    public async Task DueNotificationIsDeliveredOnceAndRecordsTheAttempt()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var item = await StoreNotificationAsync(
            fixture.Context,
            new BookingChangeClassification(BookingChangeSeverity.Material, true, ["airport"]),
            Now);
        var sender = new RecordingSender(NotificationSendResult.Sent("delivery-1"));
        var processor = new NotificationOutboxProcessor(
            fixture.Context,
            sender,
            new FixedTimeProvider(Now));

        Assert.True(await processor.ProcessNextAsync("general-worker", default));
        Assert.False(await processor.ProcessNextAsync("general-worker", default));

        var stored = await fixture.Context.NotificationOutbox.SingleAsync();
        Assert.Equal(NotificationOutboxStatus.Sent, stored.Status);
        Assert.Equal("delivery-1", stored.DeliveryReference);
        Assert.Equal(1, stored.Attempts);
        Assert.Equal(1, sender.SendCount);
    }

    [Fact]
    public async Task TransientNotificationFailureRetriesWithoutChangingBookingHistory()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var item = await StoreNotificationAsync(
            fixture.Context,
            new BookingChangeClassification(BookingChangeSeverity.Material, true, ["status"]),
            Now);
        var sender = new RecordingSender(NotificationSendResult.Retry("email_temporarily_unavailable"));
        var processor = new NotificationOutboxProcessor(
            fixture.Context,
            sender,
            new FixedTimeProvider(Now));

        Assert.True(await processor.ProcessNextAsync("general-worker", default));

        var stored = await fixture.Context.NotificationOutbox.SingleAsync();
        Assert.Equal(NotificationOutboxStatus.Pending, stored.Status);
        Assert.Equal("email_temporarily_unavailable", stored.ErrorCode);
        Assert.True(stored.NotBefore > Now);
        Assert.Equal(1, await fixture.Context.BookingVersions.CountAsync());
    }

    [Fact]
    public async Task ExpiredNotificationLeaseIsReclaimedAfterWorkerRestart()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var item = await StoreNotificationAsync(
            fixture.Context,
            new BookingChangeClassification(BookingChangeSeverity.Material, true, ["status"]),
            Now);
        await fixture.Context.NotificationOutbox
            .Where(value => value.Id == item.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.Status, NotificationOutboxStatus.Processing)
                .SetProperty(value => value.LeaseOwner, "crashed-worker")
                .SetProperty(value => value.LeaseExpiresAtUtc, Now.AddMinutes(-1).UtcDateTime));
        fixture.Context.ChangeTracker.Clear();
        var sender = new RecordingSender(NotificationSendResult.Sent("delivery-after-restart"));
        var processor = new NotificationOutboxProcessor(
            fixture.Context,
            sender,
            new FixedTimeProvider(Now));

        Assert.True(await processor.ProcessNextAsync("replacement-worker", default));

        var stored = await fixture.Context.NotificationOutbox.SingleAsync();
        Assert.Equal(NotificationOutboxStatus.Sent, stored.Status);
        Assert.Equal("delivery-after-restart", stored.DeliveryReference);
        Assert.Equal(1, sender.SendCount);
    }

    private static async Task<NotificationOutboxItem> StoreNotificationAsync(
        BookingDbContext database,
        BookingChangeClassification classification,
        DateTimeOffset observedAt)
    {
        var checkout = CreateConfirmedFlightCheckout();
        var component = checkout.Components.Single();
        var version = CanonicalBookingVersioner.Create(
            component,
            FlightState(Now.AddDays(3)),
            null,
            observedAt,
            "Initial",
            "correlation-notification");
        Assert.True(component.ApplyReconciliationVersion(version, observedAt));
        var item = NotificationOutboxItem.Create(
            checkout.CustomerId,
            checkout.Id,
            component.Id,
            version.Id,
            classification,
            "en-AU",
            "Australia/Sydney",
            "[]",
            observedAt);
        database.Checkouts.Add(checkout);
        database.BookingVersions.Add(version);
        database.NotificationOutbox.Add(item);
        await database.SaveChangesAsync();
        return item;
    }

    private static ComponentBooking CreateConfirmedFlight()
    {
        return CreateConfirmedFlightCheckout().Components.Single();
    }

    private static CheckoutSession CreateConfirmedFlightCheckout()
    {
        var clock = new FixedTimeProvider(Now);
        var result = CheckoutSession.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            [new ResolvedCheckoutOffer(
                CheckoutProduct.Flight,
                "flight-1",
                "fixture-flight",
                "QF401 SYD-MEL",
                309.40m,
                "AUD",
                "terms-flight-v1",
                "flight-r1",
                Now.AddHours(1),
                Now)],
            [new TravellerSnapshot("flight-1", Guid.CreateVersion7(), "Ari", "Taylor", false, null)],
            clock);
        Assert.True(result.IsSuccess);
        var checkout = result.Value!;
        Assert.True(checkout.AcceptRevision(1, 309.40m, "AUD", checkout.CurrentRevision.TermsHash,
            CheckoutAcceptancePolicy.CurrentVersion, clock).IsSuccess);
        Assert.True(checkout.BeginPayment("fixture", "payment_123", clock).IsSuccess);
        Assert.True(checkout.RecordPayment(PaymentProviderResult.Captured("return_123"), clock).IsSuccess);
        Assert.True(checkout.BeginBooking(clock).IsSuccess);
        var component = checkout.Components.Single();
        Assert.True(checkout.RecordBookingResult(component.Id, BookingProviderResult.Confirmed("flight_123"), clock).IsSuccess);
        return checkout;
    }

    private static RetrievedBookingState FlightState(DateTimeOffset departure) => new(
        CheckoutProduct.Flight,
        RetrievedBookingStatus.Confirmed,
        "FLT-123",
        null,
        [new BookingFlightSegment(
            "leg-1",
            "QF",
            "401",
            "SYD",
            "MEL",
            departure,
            departure.AddHours(1).AddMinutes(25))],
        309.40m,
        "AUD",
        Now);

    private static RetrievedBookingState HotelState(string propertyName) => new(
        CheckoutProduct.Hotel,
        RetrievedBookingStatus.Confirmed,
        "HTL-123",
        new RetrievedHotelStay(
            propertyName,
            new DateOnly(2026, 8, 20),
            new DateOnly(2026, 8, 22),
            "Harbour King",
            ["Breakfast"],
            "Flexible until 48 hours before arrival"),
        [],
        420m,
        "AUD",
        Now);

    private sealed class RecordingSender(NotificationSendResult result) : ICustomerNotificationSender
    {
        public int SendCount { get; private set; }

        public Task<NotificationSendResult> SendAsync(
            CustomerNotification notification,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.False(string.IsNullOrWhiteSpace(notification.IdempotencyKey));
            SendCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
