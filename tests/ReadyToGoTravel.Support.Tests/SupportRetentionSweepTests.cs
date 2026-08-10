using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Retention.Application;
using ReadyToGoTravel.Retention.Domain;
using ReadyToGoTravel.Support.Domain;
using ReadyToGoTravel.Support.Persistence;
using ReadyToGoTravel.Support.Retention;

namespace ReadyToGoTravel.Support.Tests;

public sealed class SupportRetentionSweepTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 10, 0, 0, TimeSpan.Zero);
    private static readonly RetentionSweepOptionsWrapper Enabled = new(true);

    // --- General support ticket (2 years) ---

    [Fact]
    public async Task GeneralTicketClosedPast2YearsIsFullyDeleted()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticketId = await CreateClosedTicketAsync(fixture.Context, bookingReference: null, closedAt: Now.AddYears(-2).AddDays(-1));
        var processor = CreateProcessor(fixture.Context, new InMemoryObjectStorage(), new NoHoldGuard(), new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.True(didWork);
        Assert.False(await fixture.Context.Tickets.AnyAsync(value => value.Id == ticketId));
        Assert.False(await fixture.Context.TicketMessages.AnyAsync(value => value.TicketId == ticketId));
    }

    [Fact]
    public async Task GeneralTicketClosedWithin2YearsIsUntouched()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticketId = await CreateClosedTicketAsync(fixture.Context, bookingReference: null, closedAt: Now.AddYears(-2).AddDays(1));
        var processor = CreateProcessor(fixture.Context, new InMemoryObjectStorage(), new NoHoldGuard(), new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.False(didWork);
        Assert.True(await fixture.Context.Tickets.AnyAsync(value => value.Id == ticketId));
    }

    [Fact]
    public async Task StillOpenTicketIsNeverSweptRegardlessOfAge()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticket = SupportTicket.Create("sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help", Now.AddYears(-5));
        fixture.Context.Tickets.Add(ticket);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        var processor = CreateProcessor(fixture.Context, new InMemoryObjectStorage(), new NoHoldGuard(), new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.False(didWork);
        Assert.True(await fixture.Context.Tickets.AnyAsync(value => value.Id == ticket.Id));
    }

    // --- Booking-related support ticket (7 years) ---

    [Fact]
    public async Task BookingRelatedTicketClosedPast2YearsIsNotDeletedByTheGeneralRule()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticketId = await CreateClosedTicketAsync(fixture.Context, bookingReference: "BOOK-1", closedAt: Now.AddYears(-2).AddDays(-1));
        var processor = CreateProcessor(fixture.Context, new InMemoryObjectStorage(), new NoHoldGuard(), new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.False(didWork);
        Assert.True(await fixture.Context.Tickets.AnyAsync(value => value.Id == ticketId));
    }

    [Fact]
    public async Task BookingRelatedTicketClosedPast7YearsIsDeleted()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticketId = await CreateClosedTicketAsync(fixture.Context, bookingReference: "BOOK-1", closedAt: Now.AddYears(-7).AddDays(-1));
        var processor = CreateProcessor(fixture.Context, new InMemoryObjectStorage(), new NoHoldGuard(), new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.True(didWork);
        Assert.False(await fixture.Context.Tickets.AnyAsync(value => value.Id == ticketId));
    }

    [Fact]
    public async Task ActiveLegalHoldProtectsATicketPastItsExpiry()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticketId = await CreateClosedTicketAsync(fixture.Context, bookingReference: null, closedAt: Now.AddYears(-3));
        var guard = new SelectiveHoldGuard(RetentionSubjectKind.SupportTicket, ticketId);
        var processor = CreateProcessor(fixture.Context, new InMemoryObjectStorage(), guard, new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.False(didWork);
        Assert.True(await fixture.Context.Tickets.AnyAsync(value => value.Id == ticketId));
    }

    [Fact]
    public async Task AnUnrelatedLegalHoldDoesNotProtectTheTicket()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticketId = await CreateClosedTicketAsync(fixture.Context, bookingReference: null, closedAt: Now.AddYears(-3));
        var guard = new SelectiveHoldGuard(RetentionSubjectKind.SupportTicket, Guid.CreateVersion7(Now));
        var processor = CreateProcessor(fixture.Context, new InMemoryObjectStorage(), guard, new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.True(didWork);
        Assert.False(await fixture.Context.Tickets.AnyAsync(value => value.Id == ticketId));
    }

    // --- Support attachment (90 days from ticket closure) ---

    [Fact]
    public async Task AttachmentOnATicketClosedPast90DaysIsPurgedFromStorageAndDeleted()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var storage = new InMemoryObjectStorage();
        var (ticketId, attachmentId, storageKey) = await CreateClosedTicketWithAttachmentAsync(fixture.Context, storage, Now.AddDays(-91));
        var processor = CreateProcessor(fixture.Context, storage, new NoHoldGuard(), new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.True(didWork);
        Assert.False(storage.Contains(storageKey));
        Assert.False(await fixture.Context.Attachments.AnyAsync(value => value.Id == attachmentId));
        Assert.True(await fixture.Context.Tickets.AnyAsync(value => value.Id == ticketId));
    }

    [Fact]
    public async Task AttachmentOnATicketClosedWithin90DaysIsUntouched()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var storage = new InMemoryObjectStorage();
        var (_, attachmentId, storageKey) = await CreateClosedTicketWithAttachmentAsync(fixture.Context, storage, Now.AddDays(-89));
        var processor = CreateProcessor(fixture.Context, storage, new NoHoldGuard(), new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.False(didWork);
        Assert.True(storage.Contains(storageKey));
        Assert.True(await fixture.Context.Attachments.AnyAsync(value => value.Id == attachmentId));
    }

    [Fact]
    public async Task AttachmentPurgeDecrementsUsageCounters()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var storage = new InMemoryObjectStorage();
        var (_, _, _) = await CreateClosedTicketWithAttachmentAsync(fixture.Context, storage, Now.AddDays(-91));
        var ticket = await fixture.Context.Tickets.AsNoTracking().SingleAsync();
        var usageBefore = await fixture.Context.TicketAttachmentUsage.AsNoTracking().SingleAsync(value => value.TicketId == ticket.Id);
        Assert.True(usageBefore.BytesUsed > 0);
        var processor = CreateProcessor(fixture.Context, storage, new NoHoldGuard(), new RecordingReceiptRecorder(), Now);

        await processor.ProcessCycleAsync();

        var usageAfter = await fixture.Context.TicketAttachmentUsage.AsNoTracking().SingleAsync(value => value.TicketId == ticket.Id);
        Assert.Equal(0, usageAfter.BytesUsed);
    }

    [Fact]
    public async Task AttachmentStorageDeleteFailureLeavesTheRowIntactForRetry()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var storage = new InMemoryObjectStorage { DeleteFailure = new InvalidOperationException("storage unavailable") };
        var (_, attachmentId, _) = await CreateClosedTicketWithAttachmentAsync(fixture.Context, storage, Now.AddDays(-91));
        var recorder = new RecordingReceiptRecorder();
        var processor = CreateProcessor(fixture.Context, storage, new NoHoldGuard(), recorder, Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.False(didWork);
        Assert.True(recorder.OperationalFailures > 0);
        Assert.True(await fixture.Context.Attachments.AnyAsync(value => value.Id == attachmentId));
    }

    [Fact]
    public async Task ActiveLegalHoldOnTheTicketProtectsItsAttachment()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var storage = new InMemoryObjectStorage();
        var (ticketId, attachmentId, storageKey) = await CreateClosedTicketWithAttachmentAsync(fixture.Context, storage, Now.AddDays(-91));
        var guard = new SelectiveHoldGuard(RetentionSubjectKind.SupportTicket, ticketId);
        var processor = CreateProcessor(fixture.Context, storage, guard, new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.False(didWork);
        Assert.True(storage.Contains(storageKey));
        Assert.True(await fixture.Context.Attachments.AnyAsync(value => value.Id == attachmentId));
    }

    // --- Security audit record (2 years) ---

    [Fact]
    public async Task AuditEventOlderThan2YearsIsDeleted()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticketId = await CreateOpenTicketAsync(fixture.Context);
        var eventId = Guid.CreateVersion7(Now);
        fixture.Context.AuditEvents.Add(new SupportAuditEvent(eventId, ticketId, SupportAuditEventType.GuestLinkIssued, "issued", Now.AddYears(-2).AddDays(-1)));
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        var processor = CreateProcessor(fixture.Context, new InMemoryObjectStorage(), new NoHoldGuard(), new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.True(didWork);
        Assert.False(await fixture.Context.AuditEvents.AnyAsync(value => value.Id == eventId));
    }

    [Fact]
    public async Task AuditEventWithin2YearsIsUntouched()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticketId = await CreateOpenTicketAsync(fixture.Context);
        var eventId = Guid.CreateVersion7(Now);
        fixture.Context.AuditEvents.Add(new SupportAuditEvent(eventId, ticketId, SupportAuditEventType.GuestLinkIssued, "issued", Now.AddYears(-1)));
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        var processor = CreateProcessor(fixture.Context, new InMemoryObjectStorage(), new NoHoldGuard(), new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.False(didWork);
        Assert.True(await fixture.Context.AuditEvents.AnyAsync(value => value.Id == eventId));
    }

    [Fact]
    public async Task ActiveLegalHoldOnTheLinkedTicketProtectsItsAuditEvent()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticketId = await CreateOpenTicketAsync(fixture.Context);
        var eventId = Guid.CreateVersion7(Now);
        fixture.Context.AuditEvents.Add(new SupportAuditEvent(eventId, ticketId, SupportAuditEventType.GuestLinkIssued, "issued", Now.AddYears(-3)));
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        var guard = new SelectiveHoldGuard(RetentionSubjectKind.SupportTicket, ticketId);
        var processor = CreateProcessor(fixture.Context, new InMemoryObjectStorage(), guard, new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.False(didWork);
        Assert.True(await fixture.Context.AuditEvents.AnyAsync(value => value.Id == eventId));
    }

    [Fact]
    public async Task DisabledFlagMakesEveryProcessCycleANoOp()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        await CreateClosedTicketAsync(fixture.Context, bookingReference: null, closedAt: Now.AddYears(-5));
        var processor = new SupportRetentionSweepProcessor(
            fixture.Context, new InMemoryObjectStorage(), new NoHoldGuard(), new RecordingReceiptRecorder(),
            new FixedTimeProvider(Now), new RetentionSweepOptionsWrapper(false));

        var didWork = await processor.ProcessCycleAsync();

        Assert.False(didWork);
        Assert.Equal(1, await fixture.Context.Tickets.CountAsync());
    }

    [Fact]
    public async Task RunningTheSweepTwiceIsIdempotent()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        await CreateClosedTicketAsync(fixture.Context, bookingReference: null, closedAt: Now.AddYears(-5));
        var processor = CreateProcessor(fixture.Context, new InMemoryObjectStorage(), new NoHoldGuard(), new RecordingReceiptRecorder(), Now);

        await processor.ProcessCycleAsync();
        var secondRun = await processor.ProcessCycleAsync();

        Assert.False(secondRun);
        Assert.Equal(0, await fixture.Context.Tickets.CountAsync());
    }

    private static SupportRetentionSweepProcessor CreateProcessor(
        SupportDbContext database, InMemoryObjectStorage storage, ILegalHoldGuard guard, IRetentionReceiptRecorder recorder, DateTimeOffset now) =>
        new(database, storage, guard, recorder, new FixedTimeProvider(now), Enabled);

    private static async Task<Guid> CreateOpenTicketAsync(SupportDbContext database)
    {
        var ticket = SupportTicket.Create("sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help", Now.AddYears(-1));
        database.Tickets.Add(ticket);
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();
        return ticket.Id;
    }

    private static async Task<Guid> CreateClosedTicketAsync(SupportDbContext database, string? bookingReference, DateTimeOffset closedAt)
    {
        var ticket = SupportTicket.Create("sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, bookingReference, "Help", closedAt.AddDays(-1));
        ticket.Close("staff-1", closedAt);
        database.Tickets.Add(ticket);
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();
        return ticket.Id;
    }

    private static async Task<(Guid TicketId, Guid AttachmentId, string StorageKey)> CreateClosedTicketWithAttachmentAsync(
        SupportDbContext database, InMemoryObjectStorage storage, DateTimeOffset closedAt)
    {
        var ticket = SupportTicket.Create("sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help", closedAt.AddDays(-1));
        ticket.Close("staff-1", closedAt);
        database.Tickets.Add(ticket);
        await database.SaveChangesAsync();
        var message = ticket.Messages.OrderBy(value => value.SequenceNumber).First();
        var storageKey = $"support/{ticket.Id}/{Guid.CreateVersion7(closedAt)}.pdf";
        await storage.PutAsync(storageKey, new MemoryStream([1, 2, 3, 4]), "application/pdf");
        var attachment = new SupportAttachment(
            Guid.CreateVersion7(closedAt), ticket.Id, message.Id, "receipt.pdf", "application/pdf", 4, storageKey,
            "checksum-1", "sub-1", null, closedAt.AddDays(-1));
        database.Attachments.Add(attachment);
        database.TicketAttachmentUsage.Add(new SupportTicketAttachmentUsage(ticket.Id));
        database.MessageAttachmentUsage.Add(new SupportMessageAttachmentUsage(message.Id));
        await database.SaveChangesAsync();
        await database.TicketAttachmentUsage.Where(value => value.TicketId == ticket.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.BytesUsed, 4L));
        await database.MessageAttachmentUsage.Where(value => value.MessageId == message.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.FileCount, 1));
        database.ChangeTracker.Clear();
        return (ticket.Id, attachment.Id, storageKey);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed record RetentionSweepOptionsWrapper(bool Enabled)
        : Microsoft.Extensions.Options.IOptions<ReadyToGoTravel.Retention.RetentionSweepOptions>
    {
        public ReadyToGoTravel.Retention.RetentionSweepOptions Value { get; } = new() { Enabled = Enabled };
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
