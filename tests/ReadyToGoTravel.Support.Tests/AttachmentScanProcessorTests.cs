using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Support.Application;
using ReadyToGoTravel.Support.Domain;
using ReadyToGoTravel.Support.Scanning;

namespace ReadyToGoTravel.Support.Tests;

public sealed class AttachmentScanProcessorTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ACleanScanMarksTheAttachmentCleanAndCompletesTheWork()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var storage = new InMemoryObjectStorage();
        var (_, attachmentId) = await UploadAsync(fixture, storage);
        var processor = new AttachmentScanProcessor(
            fixture.Context, storage, new RecordingAttachmentScanner(AttachmentScanOutcome.Clean), new FixedTimeProvider(Now));

        var didWork = await processor.ProcessNextAsync("worker-1");

        Assert.True(didWork);
        var attachment = await fixture.Context.Attachments.SingleAsync(value => value.Id == attachmentId);
        Assert.Equal(AttachmentScanStatus.Clean, attachment.ScanStatus);
        var work = await fixture.Context.AttachmentScanWork.SingleAsync(value => value.AttachmentId == attachmentId);
        Assert.Equal(AttachmentScanWorkStatus.Completed, work.Status);
    }

    [Fact]
    public async Task AnInfectedScanMarksInfectedDeletesTheObjectAndAudits()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var storage = new InMemoryObjectStorage();
        var (ticketId, attachmentId) = await UploadAsync(fixture, storage);
        var storageKey = (await fixture.Context.Attachments.SingleAsync(value => value.Id == attachmentId)).StorageKey;
        var processor = new AttachmentScanProcessor(
            fixture.Context, storage, new RecordingAttachmentScanner(AttachmentScanOutcome.Infected), new FixedTimeProvider(Now));

        await processor.ProcessNextAsync("worker-1");

        var attachment = await fixture.Context.Attachments.SingleAsync(value => value.Id == attachmentId);
        Assert.Equal(AttachmentScanStatus.Infected, attachment.ScanStatus);
        Assert.False(storage.Contains(storageKey));
        Assert.Contains(
            fixture.Context.AuditEvents,
            value => value.TicketId == ticketId && value.EventType == SupportAuditEventType.AttachmentScanInfected);
    }

    [Fact]
    public async Task AnUnavailableScanRetriesWithBackoffAndDoesNotMarkClean()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var storage = new InMemoryObjectStorage();
        var (_, attachmentId) = await UploadAsync(fixture, storage);
        var processor = new AttachmentScanProcessor(
            fixture.Context, storage, new RecordingAttachmentScanner(AttachmentScanOutcome.Unavailable), new FixedTimeProvider(Now));

        await processor.ProcessNextAsync("worker-1");

        var attachment = await fixture.Context.Attachments.SingleAsync(value => value.Id == attachmentId);
        Assert.Equal(AttachmentScanStatus.Pending, attachment.ScanStatus);
        var work = await fixture.Context.AttachmentScanWork.SingleAsync(value => value.AttachmentId == attachmentId);
        Assert.Equal(AttachmentScanWorkStatus.Retrying, work.Status);
        Assert.Equal(1, work.Attempts);
    }

    [Fact]
    public async Task AfterEightExhaustedAttemptsTheAttachmentIsMarkedFailed()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var storage = new InMemoryObjectStorage();
        var (ticketId, attachmentId) = await UploadAsync(fixture, storage);
        var clock = new MutableTimeProvider(Now);
        var scanner = new RecordingAttachmentScanner(AttachmentScanOutcome.Unavailable);

        for (var attempt = 1; attempt <= 8; attempt++)
        {
            var processor = new AttachmentScanProcessor(fixture.Context, storage, scanner, clock);
            await processor.ProcessNextAsync("worker-1");
            clock.Advance(TimeSpan.FromHours(2));
        }

        var attachment = await fixture.Context.Attachments.SingleAsync(value => value.Id == attachmentId);
        Assert.Equal(AttachmentScanStatus.Failed, attachment.ScanStatus);
        var work = await fixture.Context.AttachmentScanWork.SingleAsync(value => value.AttachmentId == attachmentId);
        Assert.Equal(AttachmentScanWorkStatus.Failed, work.Status);
        Assert.Contains(
            fixture.Context.AuditEvents,
            value => value.TicketId == ticketId && value.EventType == SupportAuditEventType.AttachmentScanFailed);
    }

    [Fact]
    public async Task AnActiveLeaseIsNotClaimedByAnotherWorker()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var storage = new InMemoryObjectStorage();
        await UploadAsync(fixture, storage);
        var clock = new FixedTimeProvider(Now);
        var firstProcessor = new AttachmentScanProcessor(
            fixture.Context, storage, new RecordingAttachmentScanner(AttachmentScanOutcome.Unavailable), clock);

        // Manually claim the work row as if another worker is mid-processing with an unexpired lease.
        await fixture.Context.AttachmentScanWork.ExecuteUpdateAsync(setters => setters
            .SetProperty(value => value.Status, AttachmentScanWorkStatus.Processing)
            .SetProperty(value => value.LeaseOwner, "other-worker")
            .SetProperty(value => value.LeaseExpiresAtUtc, Now.AddMinutes(2).UtcDateTime));

        var didWork = await firstProcessor.ProcessNextAsync("worker-1");

        Assert.False(didWork);
    }

    [Fact]
    public async Task AnExpiredLeaseIsReclaimableByADifferentWorker()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var storage = new InMemoryObjectStorage();
        var (_, attachmentId) = await UploadAsync(fixture, storage);
        await fixture.Context.AttachmentScanWork.ExecuteUpdateAsync(setters => setters
            .SetProperty(value => value.Status, AttachmentScanWorkStatus.Processing)
            .SetProperty(value => value.LeaseOwner, "stalled-worker")
            .SetProperty(value => value.LeaseExpiresAtUtc, Now.AddMinutes(-1).UtcDateTime));
        var processor = new AttachmentScanProcessor(
            fixture.Context, storage, new RecordingAttachmentScanner(AttachmentScanOutcome.Clean), new FixedTimeProvider(Now));

        var didWork = await processor.ProcessNextAsync("worker-2");

        Assert.True(didWork);
        var attachment = await fixture.Context.Attachments.SingleAsync(value => value.Id == attachmentId);
        Assert.Equal(AttachmentScanStatus.Clean, attachment.ScanStatus);
    }

    private static async Task<(Guid TicketId, Guid AttachmentId)> UploadAsync(SupportDatabaseFixture fixture, InMemoryObjectStorage storage)
    {
        var ticket = SupportTicket.Create("sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help", Now);
        fixture.Context.Tickets.Add(ticket);
        await fixture.Context.SaveChangesAsync();
        var attachmentService = new SupportAttachmentService(fixture.Context, storage, new FixedTimeProvider(Now));
        var result = (SupportAttachmentUploadResult.Accepted)await attachmentService.UploadAsync(
            ticket.Id, ticket.Messages[0].Id, "sub-1", null, "receipt.pdf", "application/pdf",
            new MemoryStream("%PDF-1.7"u8.ToArray()));
        return (ticket.Id, result.Attachment.Id);
    }
}
