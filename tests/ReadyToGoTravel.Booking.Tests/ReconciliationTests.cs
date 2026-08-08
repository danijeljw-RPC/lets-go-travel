using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Booking.Bookings;
using ReadyToGoTravel.Booking.Checkout;
using ReadyToGoTravel.Booking.Notifications;
using ReadyToGoTravel.Booking.Payments;
using ReadyToGoTravel.Booking.Persistence;
using ReadyToGoTravel.Booking.Providers;
using ReadyToGoTravel.Booking.Reconciliation;

namespace ReadyToGoTravel.Booking.Tests;

public sealed class ReconciliationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 8, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DuplicateImmediateRequestsCollapseIntoOneEarlierSchedule()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var component = await StoreConfirmedComponentAsync(fixture.Context, CheckoutProduct.Hotel, "hotel_123");
        var clock = new MutableTimeProvider(Now);
        var scheduler = new ReconciliationScheduler(fixture.Context, clock);

        await scheduler.EnqueueImmediateAsync(component.Id, "Booking", "correlation-1", default);
        clock.Advance(TimeSpan.FromMinutes(5));
        await scheduler.EnqueueImmediateAsync(component.Id, "Webhook", "correlation-2", default);

        var work = await fixture.Context.ReconciliationWork.SingleAsync();
        Assert.Equal(Now, work.DueAt);
        Assert.Equal("Webhook", work.Source);
        Assert.Equal("correlation-2", work.CorrelationId);
    }

    [Fact]
    public async Task ScheduledRetrievalCreatesOneVersionAndDailyFlightWorkWithoutAWebhook()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var component = await StoreConfirmedComponentAsync(fixture.Context, CheckoutProduct.Flight, "flight_123");
        var clock = new MutableTimeProvider(Now);
        var state = FlightState(Now.AddDays(3));
        var provider = new QueueBookingProvider(
            new BookingProviderExecutionResult(BookingProviderStatus.Confirmed, "flight_123", null)
            {
                RetrievedState = state,
            });
        var scheduler = new ReconciliationScheduler(fixture.Context, clock);
        await scheduler.EnqueueImmediateAsync(component.Id, "Scheduled", "correlation-1", default);
        var processor = new BookingReconciliationProcessor(fixture.Context, provider, scheduler, clock);

        Assert.True(await processor.ProcessNextAsync(CheckoutProduct.Flight, "flight-worker", default));

        var version = await fixture.Context.BookingVersions.SingleAsync();
        var work = await fixture.Context.ReconciliationWork.SingleAsync();
        var attempt = await fixture.Context.ReconciliationAttempts.SingleAsync();
        Assert.Equal(1, version.VersionNumber);
        Assert.Equal(ReconciliationWorkStatus.Pending, work.Status);
        Assert.Equal(Now.AddDays(1), work.DueAt);
        Assert.Equal(ReconciliationAttemptOutcome.Succeeded, attempt.Outcome);
        Assert.Equal(1, provider.RetrieveCount);
    }

    [Fact]
    public async Task ExpiredReconciliationLeaseIsReclaimedAfterWorkerRestart()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var component = await StoreConfirmedComponentAsync(fixture.Context, CheckoutProduct.Hotel, "hotel_123");
        var clock = new MutableTimeProvider(Now);
        var scheduler = new ReconciliationScheduler(fixture.Context, clock);
        await scheduler.EnqueueImmediateAsync(component.Id, "Scheduled", "correlation-lease", default);
        await fixture.Context.ReconciliationWork.ExecuteUpdateAsync(setters => setters
            .SetProperty(value => value.Status, ReconciliationWorkStatus.Processing)
            .SetProperty(value => value.LeaseOwner, "crashed-worker")
            .SetProperty(value => value.LeaseExpiresAtUtc, Now.AddMinutes(-1).UtcDateTime));
        fixture.Context.ChangeTracker.Clear();
        var provider = new QueueBookingProvider(Result("hotel_123", HotelState()));
        var processor = new BookingReconciliationProcessor(fixture.Context, provider, scheduler, clock);

        Assert.True(await processor.ProcessNextAsync(CheckoutProduct.Hotel, "replacement-worker", default));

        Assert.Equal(1, provider.RetrieveCount);
        Assert.Equal(1, await fixture.Context.BookingVersions.CountAsync());
        Assert.Equal(ReconciliationAttemptOutcome.Succeeded,
            (await fixture.Context.ReconciliationAttempts.SingleAsync()).Outcome);
    }

    [Fact]
    public async Task FlightInsideFinalTwentyFourHoursReconcilesHourlyAndUnchangedStateAddsNoVersion()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var component = await StoreConfirmedComponentAsync(fixture.Context, CheckoutProduct.Flight, "flight_123");
        var clock = new MutableTimeProvider(Now);
        var state = FlightState(Now.AddHours(12));
        var provider = new QueueBookingProvider(
            Result("flight_123", state),
            Result("flight_123", state));
        var scheduler = new ReconciliationScheduler(fixture.Context, clock);
        await scheduler.EnqueueImmediateAsync(component.Id, "Scheduled", "correlation-1", default);
        var processor = new BookingReconciliationProcessor(fixture.Context, provider, scheduler, clock);

        Assert.True(await processor.ProcessNextAsync(CheckoutProduct.Flight, "flight-worker", default));
        clock.Advance(TimeSpan.FromHours(1));
        Assert.True(await processor.ProcessNextAsync(CheckoutProduct.Flight, "flight-worker", default));

        Assert.Equal(1, await fixture.Context.BookingVersions.CountAsync());
        Assert.Equal(2, await fixture.Context.ReconciliationAttempts.CountAsync());
        Assert.Equal(Now.AddHours(2), (await fixture.Context.ReconciliationWork.SingleAsync()).DueAt);
    }

    [Fact]
    public async Task RetrievalFailureIsInspectableAndDoesNotClaimTheBookingIsUnchanged()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var component = await StoreConfirmedComponentAsync(fixture.Context, CheckoutProduct.Hotel, "hotel_123");
        var clock = new MutableTimeProvider(Now);
        var provider = new ThrowingBookingProvider(new TimeoutException("supplier payload must not be persisted"));
        var scheduler = new ReconciliationScheduler(fixture.Context, clock);
        await scheduler.EnqueueImmediateAsync(component.Id, "Scheduled", "correlation-1", default);
        var processor = new BookingReconciliationProcessor(fixture.Context, provider, scheduler, clock);

        Assert.True(await processor.ProcessNextAsync(CheckoutProduct.Hotel, "general-worker", default));

        Assert.Empty(await fixture.Context.BookingVersions.ToArrayAsync());
        var attempt = await fixture.Context.ReconciliationAttempts.SingleAsync();
        var work = await fixture.Context.ReconciliationWork.SingleAsync();
        Assert.Equal(ReconciliationAttemptOutcome.Retrying, attempt.Outcome);
        Assert.Equal("supplier_retrieval_failed", attempt.ErrorCode);
        Assert.Equal(ReconciliationWorkStatus.Pending, work.Status);
        Assert.True(work.DueAt > Now);
        Assert.DoesNotContain("payload", work.LastErrorCode ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancelledSupplierStateStopsFutureChecks()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var component = await StoreConfirmedComponentAsync(fixture.Context, CheckoutProduct.Flight, "flight_123");
        var clock = new MutableTimeProvider(Now);
        var cancelled = FlightState(Now.AddHours(12)) with { Status = RetrievedBookingStatus.Cancelled };
        var provider = new QueueBookingProvider(Result("flight_123", cancelled));
        var scheduler = new ReconciliationScheduler(fixture.Context, clock);
        await scheduler.EnqueueImmediateAsync(component.Id, "Scheduled", "correlation-1", default);
        var processor = new BookingReconciliationProcessor(fixture.Context, provider, scheduler, clock);

        Assert.True(await processor.ProcessNextAsync(CheckoutProduct.Flight, "flight-worker", default));

        var work = await fixture.Context.ReconciliationWork.SingleAsync();
        Assert.Equal(ReconciliationWorkStatus.Completed, work.Status);
        Assert.Null(work.DueAt);
        var checkout = await fixture.Context.Checkouts
            .Include(value => value.Components)
            .SingleAsync();
        Assert.Equal(CheckoutStatus.RequiresSupport, checkout.Status);
        Assert.Equal(ComponentBookingStatus.Cancelled, checkout.Components.Single().Status);
    }

    [Fact]
    public async Task MeaningfulChangeCommitsVersionProjectionAndNotificationIntentTogether()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var component = await StoreConfirmedComponentAsync(fixture.Context, CheckoutProduct.Flight, "flight_123");
        var clock = new MutableTimeProvider(Now);
        var provider = new QueueBookingProvider(
            Result("flight_123", FlightState(Now.AddHours(12))),
            Result("flight_123", FlightState(Now.AddHours(12).AddMinutes(45))));
        var scheduler = new ReconciliationScheduler(fixture.Context, clock);
        await scheduler.EnqueueImmediateAsync(component.Id, "Scheduled", "correlation-1", default);
        var processor = new BookingReconciliationProcessor(fixture.Context, provider, scheduler, clock);

        Assert.True(await processor.ProcessNextAsync(CheckoutProduct.Flight, "flight-worker", default));
        Assert.Empty(await fixture.Context.NotificationOutbox.ToArrayAsync());
        clock.Advance(TimeSpan.FromHours(1));
        Assert.True(await processor.ProcessNextAsync(CheckoutProduct.Flight, "flight-worker", default));

        Assert.Equal(2, await fixture.Context.BookingVersions.CountAsync());
        var notification = await fixture.Context.NotificationOutbox.SingleAsync();
        Assert.Equal(BookingChangeSeverity.Material, notification.Severity);
        Assert.Equal(component.Id, notification.ComponentBookingId);
        Assert.Equal(NotificationOutboxStatus.Pending, notification.Status);
    }

    private static BookingProviderExecutionResult Result(string reference, RetrievedBookingState state) =>
        new(BookingProviderStatus.Confirmed, reference, null) { RetrievedState = state };

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

    private static RetrievedBookingState HotelState() => new(
        CheckoutProduct.Hotel,
        RetrievedBookingStatus.Confirmed,
        "HTL-123",
        new RetrievedHotelStay(
            "Harbour Lane Hotel",
            DateOnly.FromDateTime(Now.AddDays(10).Date),
            DateOnly.FromDateTime(Now.AddDays(12).Date),
            "Harbour King",
            ["Breakfast"],
            "Flexible until 48 hours before arrival"),
        [],
        420m,
        "AUD",
        Now);

    private static async Task<ComponentBooking> StoreConfirmedComponentAsync(
        BookingDbContext database,
        CheckoutProduct product,
        string externalReference)
    {
        var clock = new MutableTimeProvider(Now);
        var result = CheckoutSession.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            [new ResolvedCheckoutOffer(
                product,
                $"{product.ToString().ToLowerInvariant()}-1",
                $"fixture-{product.ToString().ToLowerInvariant()}",
                product == CheckoutProduct.Flight ? "QF401 SYD-MEL" : "Harbour Lane Hotel",
                product == CheckoutProduct.Flight ? 309.40m : 420m,
                "AUD",
                $"terms-{product.ToString().ToLowerInvariant()}-v1",
                $"{product.ToString().ToLowerInvariant()}-r1",
                Now.AddHours(1),
                Now)],
            [new TravellerSnapshot(
                $"{product.ToString().ToLowerInvariant()}-1",
                Guid.CreateVersion7(),
                "Ari",
                "Taylor",
                false,
                null)],
            clock);
        Assert.True(result.IsSuccess);
        var checkout = result.Value!;
        Assert.True(checkout.AcceptRevision(
            1,
            checkout.CurrentRevision.Total,
            "AUD",
            checkout.CurrentRevision.TermsHash,
            CheckoutAcceptancePolicy.CurrentVersion,
            clock).IsSuccess);
        Assert.True(checkout.BeginPayment("fixture", "payment_123", clock).IsSuccess);
        Assert.True(checkout.RecordPayment(PaymentProviderResult.Captured("return_123"), clock).IsSuccess);
        Assert.True(checkout.BeginBooking(clock).IsSuccess);
        var component = checkout.Components.Single();
        Assert.True(checkout.RecordBookingResult(
            component.Id,
            BookingProviderResult.Confirmed(externalReference),
            clock).IsSuccess);
        database.Checkouts.Add(checkout);
        await database.SaveChangesAsync();
        return component;
    }

    private sealed class QueueBookingProvider(params BookingProviderExecutionResult[] results) : IBookingProvider
    {
        private readonly Queue<BookingProviderExecutionResult> results = new(results);

        public int RetrieveCount { get; private set; }

        public Task<BookingProviderExecutionResult> BookAsync(
            BookingCommand command,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Reconciliation must never initiate a booking.");

        public Task<BookingProviderExecutionResult> RetrieveAsync(
            string externalReference,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RetrieveCount++;
            return Task.FromResult(results.Dequeue());
        }
    }

    private sealed class ThrowingBookingProvider(Exception exception) : IBookingProvider
    {
        public Task<BookingProviderExecutionResult> BookAsync(
            BookingCommand command,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Reconciliation must never initiate a booking.");

        public Task<BookingProviderExecutionResult> RetrieveAsync(
            string externalReference,
            CancellationToken cancellationToken = default) =>
            Task.FromException<BookingProviderExecutionResult>(exception);
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan value) => now = now.Add(value);
    }
}
