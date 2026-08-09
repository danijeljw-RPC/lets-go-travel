using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Support.Domain;
using ReadyToGoTravel.Support.Persistence;
using ReadyToGoTravel.Support.Storage;

namespace ReadyToGoTravel.Support.Scanning;

public interface IAttachmentScanProcessor
{
    Task<bool> ProcessNextAsync(string workerId, CancellationToken cancellationToken = default);

    Task<bool> RequeueAsync(Guid id, CancellationToken cancellationToken = default);
}

internal sealed class AttachmentScanProcessor(
    SupportDbContext database,
    IObjectStorage storage,
    IAttachmentScanner scanner,
    TimeProvider timeProvider) : IAttachmentScanProcessor
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);
    private const int MaxAttempts = 8;

    public async Task<bool> ProcessNextAsync(string workerId, CancellationToken cancellationToken = default)
    {
        var work = await ClaimNextAsync(workerId, cancellationToken);
        if (work is null)
        {
            return false;
        }

        var attachment = await database.Attachments.SingleAsync(value => value.Id == work.AttachmentId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        AttachmentScanOutcome outcome;
        try
        {
            await using var content = await storage.OpenReadAsync(attachment.StorageKey, cancellationToken);
            outcome = await scanner.ScanAsync(content, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            outcome = AttachmentScanOutcome.Unavailable;
        }

        switch (outcome)
        {
            case AttachmentScanOutcome.Clean:
                attachment.MarkClean();
                work.Complete(now);
                break;
            case AttachmentScanOutcome.Infected:
                attachment.MarkInfected();
                await storage.DeleteAsync(attachment.StorageKey, cancellationToken);
                await AddAuditEventAsync(attachment.TicketId, SupportAuditEventType.AttachmentScanInfected, "Attachment failed malware scan.", now, cancellationToken);
                work.Complete(now);
                break;
            case AttachmentScanOutcome.Unavailable when work.Attempts >= MaxAttempts:
                attachment.MarkFailed();
                await AddAuditEventAsync(attachment.TicketId, SupportAuditEventType.AttachmentScanFailed, "Attachment scan retries exhausted.", now, cancellationToken);
                work.Fail("attachment_scan_unavailable", now);
                break;
            case AttachmentScanOutcome.Unavailable:
                var delayMinutes = Math.Min(60, Math.Pow(2, Math.Min(work.Attempts, 5)));
                work.Retry("attachment_scan_unavailable", now.AddMinutes(delayMinutes));
                break;
        }

        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RequeueAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var work = await database.AttachmentScanWork.SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (work is null || work.Status != AttachmentScanWorkStatus.Failed)
        {
            return false;
        }

        work.Requeue(timeProvider.GetUtcNow());
        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<AttachmentScanWork?> ClaimNextAsync(string workerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        var now = timeProvider.GetUtcNow();
        var nowUtc = now.UtcDateTime;
        var id = await database.AttachmentScanWork.AsNoTracking()
            .Where(value =>
                value.NextAttemptAtUtc <= nowUtc &&
                (value.Status == AttachmentScanWorkStatus.Pending ||
                 value.Status == AttachmentScanWorkStatus.Retrying ||
                 value.Status == AttachmentScanWorkStatus.Processing && value.LeaseExpiresAtUtc <= nowUtc))
            .OrderBy(value => value.NextAttemptAtUtc)
            .ThenBy(value => value.Id)
            .Select(value => (Guid?)value.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (!id.HasValue)
        {
            return null;
        }

        var claimed = await database.AttachmentScanWork
            .Where(value => value.Id == id &&
                value.NextAttemptAtUtc <= nowUtc &&
                (value.Status == AttachmentScanWorkStatus.Pending ||
                 value.Status == AttachmentScanWorkStatus.Retrying ||
                 value.Status == AttachmentScanWorkStatus.Processing && value.LeaseExpiresAtUtc <= nowUtc))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.Status, AttachmentScanWorkStatus.Processing)
                .SetProperty(value => value.LeaseOwner, workerId)
                .SetProperty(value => value.LeaseExpiresAtUtc, now.Add(LeaseDuration).UtcDateTime)
                .SetProperty(value => value.Attempts, value => value.Attempts + 1), cancellationToken);
        if (claimed == 0)
        {
            return null;
        }

        database.ChangeTracker.Clear();
        return await database.AttachmentScanWork.SingleAsync(value => value.Id == id, cancellationToken);
    }

    private async Task AddAuditEventAsync(
        Guid ticketId,
        SupportAuditEventType eventType,
        string detail,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        database.AuditEvents.Add(new SupportAuditEvent(Guid.CreateVersion7(now), ticketId, eventType, detail, now));
        await Task.CompletedTask;
    }
}
