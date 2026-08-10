using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Retention.Application;
using ReadyToGoTravel.Retention.Domain;
using ReadyToGoTravel.Retention.Persistence;

namespace ReadyToGoTravel.Retention.Tests;

public sealed class LegalHoldServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task OpeningAHoldWithZeroScopesThrows()
    {
        await using var fixture = await RetentionDatabaseFixture.CreateAsync();
        var service = new LegalHoldService(fixture.Context, new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<ArgumentException>(() => service.OpenAsync(
            "MATTER-1", "Dispute", "officer-1", Now.AddDays(90), [], default));
    }

    [Fact]
    public async Task OpeningAHoldWithASubjectKeyItsOwningSweepDoesNotQueryThrows()
    {
        // Defence in depth alongside LegalHoldEndpoints' own validation (github issue #14): even
        // if a future caller of LegalHoldService bypassed the HTTP-layer check, the domain layer
        // itself must not persist a scope that would be silently unenforceable.
        await using var fixture = await RetentionDatabaseFixture.CreateAsync();
        var service = new LegalHoldService(fixture.Context, new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<ArgumentException>(() => service.OpenAsync(
            "MATTER-1", "Dispute", "officer-1", Now.AddDays(90),
            [new LegalHoldScopeRequest(RetentionRecordClass.GeneralSupportTicket, Guid.CreateVersion7(Now), null, null)],
            default));
    }

    [Fact]
    public async Task OpeningAHoldForARecordClassWithNoLiveSweepAcceptsAnySubjectKind()
    {
        // CanonicalBookingEvidence has no ExpectedSubjectKind (PolicyOnlyNoLiveSweep - no sweep
        // exists yet to be incompatible with), so no subject kind can be rejected for it.
        await using var fixture = await RetentionDatabaseFixture.CreateAsync();
        var service = new LegalHoldService(fixture.Context, new FixedTimeProvider(Now));

        var hold = await service.OpenAsync(
            "MATTER-1", "Dispute", "officer-1", Now.AddDays(90),
            [new LegalHoldScopeRequest(RetentionRecordClass.CanonicalBookingEvidence, Guid.CreateVersion7(Now), null, null)],
            default);

        Assert.Single(hold.Scopes);
    }

    [Fact]
    public async Task OpeningAHoldWithAtLeastOneScopePersistsItAndAnAuditEvent()
    {
        await using var fixture = await RetentionDatabaseFixture.CreateAsync();
        var service = new LegalHoldService(fixture.Context, new FixedTimeProvider(Now));
        var customerId = Guid.CreateVersion7(Now);

        var hold = await service.OpenAsync(
            "MATTER-1", "Dispute", "officer-1", Now.AddDays(90),
            [new LegalHoldScopeRequest(RetentionRecordClass.AbandonedCheckoutState, customerId, null, null)],
            default);

        Assert.Single(hold.Scopes);
        Assert.True(hold.IsActive);
        var auditEvent = await fixture.Context.LegalHoldAuditEvents.SingleAsync();
        Assert.Equal(LegalHoldAuditEventType.HoldOpened, auditEvent.EventType);
        Assert.Equal("officer-1", auditEvent.ActorSubject);
    }

    [Fact]
    public async Task ExcludeHeldAsyncReturnsOnlyCandidatesWithAnActiveMatchingScope()
    {
        await using var fixture = await RetentionDatabaseFixture.CreateAsync();
        var service = new LegalHoldService(fixture.Context, new FixedTimeProvider(Now));
        var heldTicket = Guid.CreateVersion7(Now);
        var unrelatedTicket = Guid.CreateVersion7(Now);
        await service.OpenAsync(
            "MATTER-1", "Dispute", "officer-1", Now.AddDays(90),
            [new LegalHoldScopeRequest(RetentionRecordClass.GeneralSupportTicket, null, null, heldTicket)],
            default);

        var held = await service.ExcludeHeldAsync(
            RetentionRecordClass.GeneralSupportTicket,
            RetentionSubjectKind.SupportTicket,
            [heldTicket, unrelatedTicket],
            default);

        Assert.Single(held);
        Assert.Contains(heldTicket, held);
    }

    [Fact]
    public async Task ExcludeHeldAsyncRecordsAGuardCheckAuditEventForEverySuppressedCandidate()
    {
        // A live PostgreSQL drill (see the Slice 7 outcome report) surfaced that the batch-level
        // exclusion path - which protects the overwhelming majority of held items in practice,
        // not the rarer fine-grained IsHeldAsync recheck - was not being audited at all. This
        // guards against regressing that fix.
        await using var fixture = await RetentionDatabaseFixture.CreateAsync();
        var service = new LegalHoldService(fixture.Context, new FixedTimeProvider(Now));
        var heldTicket = Guid.CreateVersion7(Now);
        var hold = await service.OpenAsync(
            "MATTER-1", "Dispute", "officer-1", Now.AddDays(90),
            [new LegalHoldScopeRequest(RetentionRecordClass.GeneralSupportTicket, null, null, heldTicket)],
            default);

        await service.ExcludeHeldAsync(
            RetentionRecordClass.GeneralSupportTicket, RetentionSubjectKind.SupportTicket, [heldTicket], default);

        var guardEvent = await fixture.Context.LegalHoldAuditEvents
            .SingleAsync(value => value.EventType == LegalHoldAuditEventType.GuardCheckHeld);
        Assert.Equal(hold.Id, guardEvent.LegalHoldId);
    }

    [Fact]
    public async Task AnUnrelatedRecordClassIsNeverExcludedByAHoldScopedToADifferentClass()
    {
        await using var fixture = await RetentionDatabaseFixture.CreateAsync();
        var service = new LegalHoldService(fixture.Context, new FixedTimeProvider(Now));
        var ticketId = Guid.CreateVersion7(Now);
        await service.OpenAsync(
            "MATTER-1", "Dispute", "officer-1", Now.AddDays(90),
            [new LegalHoldScopeRequest(RetentionRecordClass.SupportAttachment, null, null, ticketId)],
            default);

        var held = await service.ExcludeHeldAsync(
            RetentionRecordClass.GeneralSupportTicket,
            RetentionSubjectKind.SupportTicket,
            [ticketId],
            default);

        Assert.Empty(held);
    }

    [Fact]
    public async Task AnUnrelatedHoldOnADifferentSubjectDoesNotSuppressDeletionOfThisOne()
    {
        await using var fixture = await RetentionDatabaseFixture.CreateAsync();
        var service = new LegalHoldService(fixture.Context, new FixedTimeProvider(Now));
        var heldCustomer = Guid.CreateVersion7(Now);
        var otherCustomer = Guid.CreateVersion7(Now);
        await service.OpenAsync(
            "MATTER-1", "Dispute", "officer-1", Now.AddDays(90),
            [new LegalHoldScopeRequest(RetentionRecordClass.AbandonedCheckoutState, heldCustomer, null, null)],
            default);

        Assert.True(await service.IsHeldAsync(RetentionRecordClass.AbandonedCheckoutState, RetentionSubjectKind.Customer, heldCustomer, default));
        Assert.False(await service.IsHeldAsync(RetentionRecordClass.AbandonedCheckoutState, RetentionSubjectKind.Customer, otherCustomer, default));
    }

    [Fact]
    public async Task MultipleOverlappingHoldsOnTheSameSubjectStillReportHeld()
    {
        await using var fixture = await RetentionDatabaseFixture.CreateAsync();
        var service = new LegalHoldService(fixture.Context, new FixedTimeProvider(Now));
        var customerId = Guid.CreateVersion7(Now);
        await service.OpenAsync(
            "MATTER-1", "Dispute A", "officer-1", Now.AddDays(90),
            [new LegalHoldScopeRequest(RetentionRecordClass.AbandonedCheckoutState, customerId, null, null)],
            default);
        await service.OpenAsync(
            "MATTER-2", "Dispute B", "officer-2", Now.AddDays(90),
            [new LegalHoldScopeRequest(RetentionRecordClass.AbandonedCheckoutState, customerId, null, null)],
            default);

        Assert.True(await service.IsHeldAsync(RetentionRecordClass.AbandonedCheckoutState, RetentionSubjectKind.Customer, customerId, default));
    }

    [Fact]
    public async Task ReleasingAHoldMakesIsHeldFalseImmediately()
    {
        await using var fixture = await RetentionDatabaseFixture.CreateAsync();
        var service = new LegalHoldService(fixture.Context, new FixedTimeProvider(Now));
        var customerId = Guid.CreateVersion7(Now);
        var hold = await service.OpenAsync(
            "MATTER-1", "Dispute", "officer-1", Now.AddDays(90),
            [new LegalHoldScopeRequest(RetentionRecordClass.AbandonedCheckoutState, customerId, null, null)],
            default);

        await service.ReleaseAsync(hold.Id, "officer-1", "Matter resolved", default);

        Assert.False(await service.IsHeldAsync(RetentionRecordClass.AbandonedCheckoutState, RetentionSubjectKind.Customer, customerId, default));
        var releaseEvent = await fixture.Context.LegalHoldAuditEvents
            .SingleAsync(value => value.EventType == LegalHoldAuditEventType.HoldReleased);
        Assert.Equal("officer-1", releaseEvent.ActorSubject);
    }

    [Fact]
    public async Task ReleasingAnAlreadyReleasedHoldIsIdempotent()
    {
        await using var fixture = await RetentionDatabaseFixture.CreateAsync();
        var service = new LegalHoldService(fixture.Context, new FixedTimeProvider(Now));
        var customerId = Guid.CreateVersion7(Now);
        var hold = await service.OpenAsync(
            "MATTER-1", "Dispute", "officer-1", Now.AddDays(90),
            [new LegalHoldScopeRequest(RetentionRecordClass.AbandonedCheckoutState, customerId, null, null)],
            default);
        await service.ReleaseAsync(hold.Id, "officer-1", "First release", default);

        await service.ReleaseAsync(hold.Id, "officer-2", "Second release attempt", default);

        var released = await service.GetAsync(hold.Id, default);
        Assert.Equal("officer-1", released!.ReleasedBySubject);
        Assert.Equal("First release", released.ReleaseReason);
        var releaseEvents = await fixture.Context.LegalHoldAuditEvents
            .Where(value => value.EventType == LegalHoldAuditEventType.HoldReleased)
            .ToListAsync();
        Assert.Single(releaseEvents);
    }

    [Fact]
    public async Task ReleasedThenRecreatedHoldOnTheSameSubjectIsHeldAgain()
    {
        await using var fixture = await RetentionDatabaseFixture.CreateAsync();
        var service = new LegalHoldService(fixture.Context, new FixedTimeProvider(Now));
        var customerId = Guid.CreateVersion7(Now);
        var first = await service.OpenAsync(
            "MATTER-1", "Dispute", "officer-1", Now.AddDays(90),
            [new LegalHoldScopeRequest(RetentionRecordClass.AbandonedCheckoutState, customerId, null, null)],
            default);
        await service.ReleaseAsync(first.Id, "officer-1", "Resolved", default);

        await service.OpenAsync(
            "MATTER-3", "New dispute", "officer-1", Now.AddDays(90),
            [new LegalHoldScopeRequest(RetentionRecordClass.AbandonedCheckoutState, customerId, null, null)],
            default);

        Assert.True(await service.IsHeldAsync(RetentionRecordClass.AbandonedCheckoutState, RetentionSubjectKind.Customer, customerId, default));
    }

    [Fact]
    public async Task AHoldWithAPastReviewDateRemainsActive()
    {
        await using var fixture = await RetentionDatabaseFixture.CreateAsync();
        var service = new LegalHoldService(fixture.Context, new FixedTimeProvider(Now));
        var customerId = Guid.CreateVersion7(Now);
        await service.OpenAsync(
            "MATTER-1", "Dispute", "officer-1", Now.AddDays(-1),
            [new LegalHoldScopeRequest(RetentionRecordClass.AbandonedCheckoutState, customerId, null, null)],
            default);

        Assert.True(await service.IsHeldAsync(RetentionRecordClass.AbandonedCheckoutState, RetentionSubjectKind.Customer, customerId, default));
    }

    [Fact]
    public async Task IsHeldAsyncRecordsAGuardCheckAuditEventWhenItFindsAHold()
    {
        await using var fixture = await RetentionDatabaseFixture.CreateAsync();
        var service = new LegalHoldService(fixture.Context, new FixedTimeProvider(Now));
        var customerId = Guid.CreateVersion7(Now);
        var hold = await service.OpenAsync(
            "MATTER-1", "Dispute", "officer-1", Now.AddDays(90),
            [new LegalHoldScopeRequest(RetentionRecordClass.AbandonedCheckoutState, customerId, null, null)],
            default);

        await service.IsHeldAsync(RetentionRecordClass.AbandonedCheckoutState, RetentionSubjectKind.Customer, customerId, default);

        var guardEvent = await fixture.Context.LegalHoldAuditEvents
            .SingleAsync(value => value.EventType == LegalHoldAuditEventType.GuardCheckHeld);
        Assert.Equal(hold.Id, guardEvent.LegalHoldId);
    }

    [Fact]
    public async Task RecordAsyncPersistsADeletionReceipt()
    {
        await using var fixture = await RetentionDatabaseFixture.CreateAsync();
        var service = new LegalHoldService(fixture.Context, new FixedTimeProvider(Now));

        await service.RecordAsync(
            RetentionRecordClass.SupportAttachment, 1, "Delete", 5, 1, Now, "one storage failure", default);

        var receipt = await fixture.Context.RetentionDeletionReceipts.SingleAsync();
        Assert.Equal(5, receipt.SuccessCount);
        Assert.Equal(1, receipt.FailureCount);
    }

    [Fact]
    public async Task RecordOperationalFailureAsyncIsIdempotentForTheSameDedupeKey()
    {
        await using var fixture = await RetentionDatabaseFixture.CreateAsync();
        var service = new LegalHoldService(fixture.Context, new FixedTimeProvider(Now));

        await service.RecordOperationalFailureAsync(RetentionRecordClass.SupportAttachment, "support-attachment", "storage_delete_failed", Now, default);
        await service.RecordOperationalFailureAsync(RetentionRecordClass.SupportAttachment, "support-attachment", "storage_delete_failed", Now, default);

        var cases = await fixture.Context.RetentionOperationalCases.ToListAsync();
        Assert.Single(cases);
    }

    [Fact]
    public async Task LosingTheDedupeKeyRaceDoesNotPoisonLaterSaveChangesOnTheSameContext()
    {
        // Regression test for Codex review of PR #12 (github issue #18): the fast-path AnyAsync
        // check in RecordOperationalFailureAsync only prevents a duplicate insert when the calls
        // are sequential. Two real concurrent callers can both pass the check before either
        // commits, so one of them hits the unique-index DbUpdateException for real. Before the
        // fix, the losing entity stayed tracked as Added on that LegalHoldService's
        // RetentionDbContext - which is scoped for a whole worker cycle - so the *next*
        // SaveChangesAsync on that same context (here, RecordAsync writing a receipt) retried the
        // same duplicate insert and failed too. Uses two independent RetentionDbContext instances
        // against a shared-cache SQLite database (the same real-race pattern
        // AttachmentUploadDownloadTests uses) rather than mocking the race away.
        var connectionString = $"Data Source=file:operational-case-race-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var keepAlive = new SqliteConnection(connectionString);
        await keepAlive.OpenAsync();
        await using (var setupContext = new RetentionDbContext(new DbContextOptionsBuilder<RetentionDbContext>().UseSqlite(connectionString).Options))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        async Task RaceThenRecordAReceiptAsync()
        {
            await using var context = new RetentionDbContext(new DbContextOptionsBuilder<RetentionDbContext>().UseSqlite(connectionString).Options);
            var service = new LegalHoldService(context, new FixedTimeProvider(Now));
            await service.RecordOperationalFailureAsync(RetentionRecordClass.SupportAttachment, "support-attachment", "storage_delete_failed", Now, default);
            // Whether this specific instance won or lost the dedupe race, a later SaveChangesAsync
            // on the same context (a real receipt write, exactly as SweepAttachmentsAsync's own
            // RecordAsync call does at the end of a batch) must still succeed.
            await service.RecordAsync(RetentionRecordClass.SupportAttachment, 1, "Delete", 1, 0, Now, null, default);
        }

        await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(RaceThenRecordAReceiptAsync)));

        await using var verifyContext = new RetentionDbContext(new DbContextOptionsBuilder<RetentionDbContext>().UseSqlite(connectionString).Options);
        Assert.Single(await verifyContext.RetentionOperationalCases.ToListAsync());
        Assert.Equal(2, await verifyContext.RetentionDeletionReceipts.CountAsync());
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class RetentionDatabaseFixture(SqliteConnection connection, RetentionDbContext context) : IAsyncDisposable
{
    public RetentionDbContext Context { get; } = context;

    public static async Task<RetentionDatabaseFixture> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var context = new RetentionDbContext(new DbContextOptionsBuilder<RetentionDbContext>()
            .UseSqlite(connection)
            .Options);
        await context.Database.EnsureCreatedAsync();
        return new RetentionDatabaseFixture(connection, context);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await connection.DisposeAsync();
    }
}
