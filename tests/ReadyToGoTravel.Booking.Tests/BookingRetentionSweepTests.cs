using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Booking.Bookings;
using ReadyToGoTravel.Booking.Checkout;
using ReadyToGoTravel.Booking.Notifications;
using ReadyToGoTravel.Booking.Payments;
using ReadyToGoTravel.Booking.Persistence;
using ReadyToGoTravel.Booking.Reconciliation;
using ReadyToGoTravel.Booking.Retention;
using ReadyToGoTravel.Booking.Webhooks;
using ReadyToGoTravel.Retention;
using ReadyToGoTravel.Retention.Application;
using ReadyToGoTravel.Retention.Domain;

namespace ReadyToGoTravel.Booking.Tests;

public sealed class BookingRetentionSweepTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 10, 0, 0, TimeSpan.Zero);
    private static readonly RetentionSweepOptionsWrapper Enabled = new(true);

    // --- Webhook payload body ---

    [Fact]
    public async Task CompletedWebhookBodyPast90DaysIsRedacted()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var componentId = Guid.CreateVersion7(Now);
        await AddCompletedWebhookAsync(fixture.Context, componentId, Now.AddDays(-91));
        var processor = CreateProcessor(fixture.Context, new NoHoldGuard(), new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.True(didWork);
        var item = await fixture.Context.WebhookInbox.SingleAsync();
        Assert.Equal(string.Empty, item.RawBody);
    }

    [Fact]
    public async Task CompletedWebhookBodyBefore90DaysIsUntouched()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var componentId = Guid.CreateVersion7(Now);
        await AddCompletedWebhookAsync(fixture.Context, componentId, Now.AddDays(-89));
        var processor = CreateProcessor(fixture.Context, new NoHoldGuard(), new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.False(didWork);
        var item = await fixture.Context.WebhookInbox.SingleAsync();
        Assert.NotEqual(string.Empty, item.RawBody);
    }

    [Fact]
    public async Task QuarantinedWebhookBodyIsNeverTouchedRegardlessOfAge()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var service = new WebhookInboxService(fixture.Context, new FixedTimeProvider(Now.AddDays(-200)));
        await service.AcceptAsync(new WebhookEnvelopeInput(
            "Production", "evt-quarantine", "booking.book", "{\"raw\":true}", false, "corr-1"));
        await fixture.Context.WebhookInbox.ExecuteUpdateAsync(setters => setters
            .SetProperty(item => item.Status, WebhookInboxStatus.Quarantined)
            .SetProperty(item => item.CompletedAt, Now.AddDays(-200)));
        fixture.Context.ChangeTracker.Clear();
        var processor = CreateProcessor(fixture.Context, new NoHoldGuard(), new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.False(didWork);
        var item = await fixture.Context.WebhookInbox.SingleAsync();
        Assert.NotEqual(string.Empty, item.RawBody);
    }

    [Fact]
    public async Task WebhookMetadataSurvivesRedaction()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var componentId = Guid.CreateVersion7(Now);
        await AddCompletedWebhookAsync(fixture.Context, componentId, Now.AddDays(-91));
        var processor = CreateProcessor(fixture.Context, new NoHoldGuard(), new RecordingReceiptRecorder(), Now);

        await processor.ProcessCycleAsync();

        var item = await fixture.Context.WebhookInbox.SingleAsync();
        Assert.Equal("evt-1", item.EventId);
        Assert.NotEqual(string.Empty, item.PayloadHash);
        Assert.NotEqual(string.Empty, item.CorrelationId);
        Assert.Equal(WebhookInboxStatus.Completed, item.Status);
    }

    [Fact]
    public async Task ActiveLegalHoldOnTheComponentBookingProtectsTheWebhookBody()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var componentId = Guid.CreateVersion7(Now);
        await AddCompletedWebhookAsync(fixture.Context, componentId, Now.AddDays(-91));
        var guard = new SelectiveHoldGuard(RetentionSubjectKind.ComponentBooking, componentId);
        var processor = CreateProcessor(fixture.Context, guard, new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.False(didWork);
        var item = await fixture.Context.WebhookInbox.SingleAsync();
        Assert.NotEqual(string.Empty, item.RawBody);
    }

    [Fact]
    public async Task AnUnrelatedLegalHoldDoesNotProtectTheWebhookBody()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var componentId = Guid.CreateVersion7(Now);
        await AddCompletedWebhookAsync(fixture.Context, componentId, Now.AddDays(-91));
        var guard = new SelectiveHoldGuard(RetentionSubjectKind.ComponentBooking, Guid.CreateVersion7(Now));
        var processor = CreateProcessor(fixture.Context, guard, new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.True(didWork);
    }

    [Fact]
    public async Task RunningTheSweepTwiceIsIdempotent()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var componentId = Guid.CreateVersion7(Now);
        await AddCompletedWebhookAsync(fixture.Context, componentId, Now.AddDays(-91));
        var processor = CreateProcessor(fixture.Context, new NoHoldGuard(), new RecordingReceiptRecorder(), Now);

        await processor.ProcessCycleAsync();
        var secondRun = await processor.ProcessCycleAsync();

        Assert.False(secondRun);
        var item = await fixture.Context.WebhookInbox.SingleAsync();
        Assert.Equal(string.Empty, item.RawBody);
    }

    [Fact]
    public async Task DisabledFlagMakesEveryProcessCycleANoOp()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var componentId = Guid.CreateVersion7(Now);
        await AddCompletedWebhookAsync(fixture.Context, componentId, Now.AddDays(-200));
        var processor = new BookingRetentionSweepProcessor(
            fixture.Context, new NoHoldGuard(), new RecordingReceiptRecorder(), new FixedTimeProvider(Now),
            new RetentionSweepOptionsWrapper(false));

        var didWork = await processor.ProcessCycleAsync();

        Assert.False(didWork);
        var item = await fixture.Context.WebhookInbox.SingleAsync();
        Assert.NotEqual(string.Empty, item.RawBody);
    }

    // --- Notification rendered content ---

    [Fact]
    public async Task SentNotificationPayloadPast90DaysIsRedactedButRecipientReferenceSurvives()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var customerId = Guid.CreateVersion7(Now);
        var (componentId, versionId) = await CreateConfirmedComponentWithVersionAsync(fixture.Context, customerId);
        var item = NotificationOutboxItem.Create(
            customerId, Guid.CreateVersion7(Now), componentId, versionId,
            new BookingChangeClassification(BookingChangeSeverity.Material, true, ["schedule"]),
            "en-AU", "Australia/Sydney", "{\"rendered\":true}", Now.AddDays(-91));
        item.MarkSent("ref-1", Now.AddDays(-91));
        fixture.Context.NotificationOutbox.Add(item);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        var processor = CreateProcessor(fixture.Context, new NoHoldGuard(), new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.True(didWork);
        var saved = await fixture.Context.NotificationOutbox.SingleAsync();
        Assert.Equal(string.Empty, saved.PayloadJson);
        Assert.Equal(customerId, saved.CustomerId);
        Assert.Equal("ref-1", saved.DeliveryReference);
        Assert.Equal(NotificationOutboxStatus.Sent, saved.Status);
    }

    [Fact]
    public async Task PendingNotificationPayloadIsNeverTouched()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var (componentId, versionId) = await CreateConfirmedComponentWithVersionAsync(fixture.Context, Guid.CreateVersion7(Now));
        var item = NotificationOutboxItem.Create(
            Guid.CreateVersion7(Now), Guid.CreateVersion7(Now), componentId, versionId,
            new BookingChangeClassification(BookingChangeSeverity.Material, true, ["schedule"]),
            "en-AU", "Australia/Sydney", "{\"rendered\":true}", Now.AddDays(-200));
        fixture.Context.NotificationOutbox.Add(item);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        var processor = CreateProcessor(fixture.Context, new NoHoldGuard(), new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.False(didWork);
        var saved = await fixture.Context.NotificationOutbox.SingleAsync();
        Assert.NotEqual(string.Empty, saved.PayloadJson);
    }

    [Fact]
    public async Task ActiveLegalHoldOnTheComponentBookingProtectsNotificationContent()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var (componentId, versionId) = await CreateConfirmedComponentWithVersionAsync(fixture.Context, Guid.CreateVersion7(Now));
        var item = NotificationOutboxItem.Create(
            Guid.CreateVersion7(Now), Guid.CreateVersion7(Now), componentId, versionId,
            new BookingChangeClassification(BookingChangeSeverity.Material, true, ["schedule"]),
            "en-AU", "Australia/Sydney", "{\"rendered\":true}", Now.AddDays(-91));
        item.MarkSent("ref-1", Now.AddDays(-91));
        fixture.Context.NotificationOutbox.Add(item);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        var guard = new SelectiveHoldGuard(RetentionSubjectKind.ComponentBooking, componentId);
        var processor = CreateProcessor(fixture.Context, guard, new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.False(didWork);
        var saved = await fixture.Context.NotificationOutbox.SingleAsync();
        Assert.NotEqual(string.Empty, saved.PayloadJson);
    }

    // --- Abandoned checkout state ---

    [Fact]
    public async Task AwaitingAcceptanceCheckoutStalePast30DaysIsDeleted()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var checkout = CreateAcceptanceOnlyCheckout(Guid.CreateVersion7(Now));
        fixture.Context.Checkouts.Add(checkout);
        await fixture.Context.SaveChangesAsync();
        await fixture.Context.Checkouts.ExecuteUpdateAsync(setters => setters
            .SetProperty(value => value.UpdatedAt, Now.AddDays(-31)));
        fixture.Context.ChangeTracker.Clear();
        var processor = CreateProcessor(fixture.Context, new NoHoldGuard(), new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.True(didWork);
        Assert.Equal(0, await fixture.Context.Checkouts.CountAsync());
    }

    [Fact]
    public async Task AwaitingAcceptanceCheckoutWithin30DaysIsUntouched()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var checkout = CreateAcceptanceOnlyCheckout(Guid.CreateVersion7(Now));
        fixture.Context.Checkouts.Add(checkout);
        await fixture.Context.SaveChangesAsync();
        await fixture.Context.Checkouts.ExecuteUpdateAsync(setters => setters
            .SetProperty(value => value.UpdatedAt, Now.AddDays(-29)));
        fixture.Context.ChangeTracker.Clear();
        var processor = CreateProcessor(fixture.Context, new NoHoldGuard(), new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.False(didWork);
        Assert.Equal(1, await fixture.Context.Checkouts.CountAsync());
    }

    [Fact]
    public async Task ACompletedCheckoutIsNeverSweptByTheAbandonedCheckoutRuleRegardlessOfAge()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var checkout = CreateAcceptanceOnlyCheckout(Guid.CreateVersion7(Now));
        fixture.Context.Checkouts.Add(checkout);
        await fixture.Context.SaveChangesAsync();
        await fixture.Context.Checkouts.ExecuteUpdateAsync(setters => setters
            .SetProperty(value => value.Status, CheckoutStatus.Completed)
            .SetProperty(value => value.UpdatedAt, Now.AddDays(-400)));
        fixture.Context.ChangeTracker.Clear();
        var processor = CreateProcessor(fixture.Context, new NoHoldGuard(), new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.False(didWork);
        Assert.Equal(1, await fixture.Context.Checkouts.CountAsync());
    }

    [Fact]
    public async Task APaymentPendingCheckoutIsNeverSweptByTheAbandonedCheckoutRule()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var checkout = CreateAcceptanceOnlyCheckout(Guid.CreateVersion7(Now));
        fixture.Context.Checkouts.Add(checkout);
        await fixture.Context.SaveChangesAsync();
        await fixture.Context.Checkouts.ExecuteUpdateAsync(setters => setters
            .SetProperty(value => value.Status, CheckoutStatus.PaymentPending)
            .SetProperty(value => value.UpdatedAt, Now.AddDays(-400)));
        fixture.Context.ChangeTracker.Clear();
        var processor = CreateProcessor(fixture.Context, new NoHoldGuard(), new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.False(didWork);
        Assert.Equal(1, await fixture.Context.Checkouts.CountAsync());
    }

    [Fact]
    public async Task ActiveLegalHoldOnTheCustomerProtectsTheAbandonedCheckout()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var customerId = Guid.CreateVersion7(Now);
        var checkout = CreateAcceptanceOnlyCheckout(customerId);
        fixture.Context.Checkouts.Add(checkout);
        await fixture.Context.SaveChangesAsync();
        await fixture.Context.Checkouts.ExecuteUpdateAsync(setters => setters
            .SetProperty(value => value.UpdatedAt, Now.AddDays(-31)));
        fixture.Context.ChangeTracker.Clear();
        var guard = new SelectiveHoldGuard(RetentionSubjectKind.Customer, customerId);
        var processor = CreateProcessor(fixture.Context, guard, new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.False(didWork);
        Assert.Equal(1, await fixture.Context.Checkouts.CountAsync());
    }

    [Fact]
    public async Task PartialBatchFailureDoesNotBlockOtherCandidatesAndRecordsAnOperationalFailure()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var componentId = Guid.CreateVersion7(Now);
        await AddCompletedWebhookAsync(fixture.Context, componentId, Now.AddDays(-91), eventId: "evt-fail");
        await AddCompletedWebhookAsync(fixture.Context, Guid.CreateVersion7(Now), Now.AddDays(-92), eventId: "evt-ok");
        var recorder = new RecordingReceiptRecorder();
        var guard = new ThrowingOnceGuard(componentId);
        var processor = CreateProcessor(fixture.Context, guard, recorder, Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.True(didWork);
        Assert.True(recorder.OperationalFailures > 0);
        var items = await fixture.Context.WebhookInbox.ToListAsync();
        Assert.Contains(items, value => value.RawBody == string.Empty);
    }

    private static BookingRetentionSweepProcessor CreateProcessor(
        BookingDbContext database,
        ILegalHoldGuard guard,
        IRetentionReceiptRecorder recorder,
        DateTimeOffset now) =>
        new(database, guard, recorder, new FixedTimeProvider(now), Enabled);

    private static async Task AddCompletedWebhookAsync(
        BookingDbContext database, Guid componentId, DateTimeOffset completedAt, string eventId = "evt-1")
    {
        var service = new WebhookInboxService(database, new FixedTimeProvider(completedAt));
        await service.AcceptAsync(new WebhookEnvelopeInput(
            "Production", eventId, "booking.book", "{\"raw\":true}", false, $"corr-{eventId}"));
        await database.WebhookInbox.Where(item => item.EventId == eventId).ExecuteUpdateAsync(setters => setters
            .SetProperty(item => item.Status, WebhookInboxStatus.Completed)
            .SetProperty(item => item.CompletedAt, completedAt)
            .SetProperty(item => item.ComponentBookingId, componentId));
        // ExecuteUpdateAsync bypasses the change tracker, so the entity inserted moments ago by
        // AcceptAsync (still tracked with its original Pending/null values) would otherwise be
        // returned by a later query against this same context instance instead of the updated row.
        database.ChangeTracker.Clear();
    }

    /// <summary>
    /// Builds a real, saved Confirmed ComponentBooking plus its version-1 BookingVersion, since
    /// NotificationOutboxItem.BookingVersionId is a real foreign key that SQLite (unlike a loosely
    /// coupled correlation field) enforces at insert time.
    /// </summary>
    private static async Task<(Guid ComponentBookingId, Guid BookingVersionId)> CreateConfirmedComponentWithVersionAsync(
        BookingDbContext database, Guid customerId)
    {
        var clock = new FixedTimeProvider(Now);
        var result = CheckoutSession.Create(
            customerId,
            Guid.CreateVersion7(Now),
            [new ResolvedCheckoutOffer(
                CheckoutProduct.Hotel, "hotel-1", "fixture-hotel", "Harbour Lane Hotel", 420m, "AUD",
                "terms-hotel-v1", "hotel-r1", Now.AddHours(1), Now)],
            [new TravellerSnapshot("hotel-1", Guid.CreateVersion7(Now), "Ari", "Taylor", false, null)],
            clock);
        Assert.True(result.IsSuccess);
        var checkout = result.Value!;
        Assert.True(checkout.AcceptRevision(
            1, checkout.CurrentRevision.Total, "AUD", checkout.CurrentRevision.TermsHash,
            CheckoutAcceptancePolicy.CurrentVersion, clock).IsSuccess);
        Assert.True(checkout.BeginPayment("fixture", "payment_123", clock).IsSuccess);
        Assert.True(checkout.RecordPayment(PaymentProviderResult.Captured("return_123"), clock).IsSuccess);
        Assert.True(checkout.BeginBooking(clock).IsSuccess);
        var component = checkout.Components.Single();
        Assert.True(checkout.RecordBookingResult(
            component.Id, BookingProviderResult.Confirmed("ext-ref-1"), clock).IsSuccess);
        database.Checkouts.Add(checkout);
        await database.SaveChangesAsync();

        var version = new BookingVersion(
            Guid.CreateVersion7(Now), component.Id, 1, Now, null, "Retrieval", "v1",
            "{}", "hash-1", "{}", "{}", "{}", "Informational", "corr-version-1", null);
        database.BookingVersions.Add(version);
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();
        return (component.Id, version.Id);
    }

    private static CheckoutSession CreateAcceptanceOnlyCheckout(Guid customerId)
    {
        var result = CheckoutSession.Create(
            customerId,
            Guid.CreateVersion7(Now),
            [new ResolvedCheckoutOffer(
                CheckoutProduct.Hotel, "hotel-1", "fixture-hotel", "Harbour Lane Hotel", 420m, "AUD",
                "terms-hotel-v1", "hotel-r1", Now.AddHours(1), Now)],
            [new TravellerSnapshot("hotel-1", Guid.CreateVersion7(Now), "Ari", "Taylor", false, null)],
            new FixedTimeProvider(Now));
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed record RetentionSweepOptionsWrapper(bool Enabled)
        : Microsoft.Extensions.Options.IOptions<RetentionSweepOptions>
    {
        public RetentionSweepOptions Value { get; } = new() { Enabled = Enabled };
    }

    private sealed class NoHoldGuard : ILegalHoldGuard
    {
        public Task<IReadOnlySet<Guid>> ExcludeHeldAsync(RetentionRecordClass recordClass, RetentionSubjectKind subjectKind, IReadOnlyCollection<Guid> candidateSubjectIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());

        public Task<bool> IsHeldAsync(RetentionRecordClass recordClass, RetentionSubjectKind subjectKind, Guid subjectId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class SelectiveHoldGuard(RetentionSubjectKind heldKind, Guid heldId) : ILegalHoldGuard
    {
        public Task<IReadOnlySet<Guid>> ExcludeHeldAsync(RetentionRecordClass recordClass, RetentionSubjectKind subjectKind, IReadOnlyCollection<Guid> candidateSubjectIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<Guid>>(subjectKind == heldKind && candidateSubjectIds.Contains(heldId)
                ? new HashSet<Guid> { heldId }
                : new HashSet<Guid>());

        public Task<bool> IsHeldAsync(RetentionRecordClass recordClass, RetentionSubjectKind subjectKind, Guid subjectId, CancellationToken cancellationToken = default) =>
            Task.FromResult(subjectKind == heldKind && subjectId == heldId);
    }

    /// <summary>Simulates a hold-guard failure for exactly one subject, to exercise per-item failure isolation.</summary>
    private sealed class ThrowingOnceGuard(Guid failingSubjectId) : ILegalHoldGuard
    {
        private bool thrown;

        public Task<IReadOnlySet<Guid>> ExcludeHeldAsync(RetentionRecordClass recordClass, RetentionSubjectKind subjectKind, IReadOnlyCollection<Guid> candidateSubjectIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());

        public Task<bool> IsHeldAsync(RetentionRecordClass recordClass, RetentionSubjectKind subjectKind, Guid subjectId, CancellationToken cancellationToken = default)
        {
            if (subjectId == failingSubjectId && !thrown)
            {
                thrown = true;
                throw new InvalidOperationException("simulated transient failure");
            }

            return Task.FromResult(false);
        }
    }

    private sealed class RecordingReceiptRecorder : IRetentionReceiptRecorder
    {
        public int OperationalFailures { get; private set; }

        public Task RecordAsync(RetentionRecordClass recordClass, int policyVersion, string action, int successCount, int failureCount, DateTimeOffset completedAtUtc, string? failureSummary, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RecordOperationalFailureAsync(RetentionRecordClass recordClass, string scope, string reason, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
        {
            OperationalFailures++;
            return Task.CompletedTask;
        }
    }
}
