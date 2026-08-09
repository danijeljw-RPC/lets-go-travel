using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Support.Guest;
using ReadyToGoTravel.Support.Persistence;

namespace ReadyToGoTravel.Support.Notifications;

public interface ISupportNotificationOutboxProcessor
{
    Task<bool> ProcessNextAsync(string workerId, CancellationToken cancellationToken = default);

    Task<bool> RequeueAsync(Guid id, CancellationToken cancellationToken = default);
}

internal sealed class SupportNotificationOutboxProcessor(
    SupportDbContext database,
    ISupportNotificationSender sender,
    IGuestAccessTokenService guestTokens,
    TimeProvider timeProvider) : ISupportNotificationOutboxProcessor
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);
    private const int MaxAttempts = 8;

    public async Task<bool> ProcessNextAsync(string workerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        var item = await ClaimNextAsync(workerId, cancellationToken);
        if (item is null)
        {
            return false;
        }

        if (SupportNotificationTemplates.RequiresFreshGuestToken(item.Template))
        {
            // Only mint (and embed) a fresh guest token when it is still this item's own
            // guest-link generation to deliver. A staff revoke or rotate that has since
            // superseded it means the guest already has the authoritative link (or none, if
            // staff revoked) delivered through a different path; this stale item must become
            // Cancelled rather than mint another credential or clobber staff's.
            var issued = await guestTokens.IssueForNotificationAsync(item.TicketId, item.Id, cancellationToken);
            if (issued.Superseded)
            {
                item.Cancel("support_notification_superseded_by_guest_link_change", timeProvider.GetUtcNow());
                await database.SaveChangesAsync(cancellationToken);
                return true;
            }

            return await SendAndRecordAsync(
                item,
                SupportTicketNotificationPayload.FromJson(item.PayloadJson).ToJsonWithGuestToken(issued.RawToken!),
                cancellationToken);
        }

        return await SendAndRecordAsync(item, item.PayloadJson, cancellationToken);
    }

    private async Task<bool> SendAndRecordAsync(
        SupportNotificationOutboxItem item, string effectivePayload, CancellationToken cancellationToken)
    {
        SupportNotificationSendResult result;
        try
        {
            result = await sender.SendAsync(
                new SupportNotification(item.DedupeKey, item.RecipientEmail, item.Template, effectivePayload),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            result = SupportNotificationSendResult.Retry("support_notification_delivery_failed");
        }

        var now = timeProvider.GetUtcNow();
        if (result.Success && !string.IsNullOrWhiteSpace(result.DeliveryReference))
        {
            item.MarkSent(result.DeliveryReference, now);
        }
        else if (!result.Retryable || item.Attempts >= MaxAttempts)
        {
            // A permanent failure (Retryable == false, e.g. an invalid recipient) must become
            // terminal on the attempt that discovers it, not after burning through the same retry
            // budget as a transient outage - the sender has already told us retrying cannot help.
            item.Fail(result.ErrorCode ?? "support_notification_retry_exhausted", now);
        }
        else
        {
            var minutes = Math.Min(60, Math.Pow(2, Math.Min(item.Attempts, 5)));
            item.Retry(result.ErrorCode ?? "support_notification_delivery_failed", now.AddMinutes(minutes), now);
        }

        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RequeueAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await database.SupportNotificationOutbox.SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (item is null || item.Status != SupportNotificationOutboxStatus.Failed)
        {
            return false;
        }

        item.Requeue(timeProvider.GetUtcNow());
        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<SupportNotificationOutboxItem?> ClaimNextAsync(string workerId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var nowUtc = now.UtcDateTime;
        var id = await database.SupportNotificationOutbox.AsNoTracking()
            .Where(value =>
                value.NotBeforeUtc <= nowUtc &&
                (value.Status == SupportNotificationOutboxStatus.Pending ||
                 value.Status == SupportNotificationOutboxStatus.Processing && value.LeaseExpiresAtUtc <= nowUtc))
            .OrderBy(value => value.NotBeforeUtc)
            .ThenBy(value => value.Id)
            .Select(value => (Guid?)value.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (!id.HasValue)
        {
            return null;
        }

        var claimed = await database.SupportNotificationOutbox
            .Where(value => value.Id == id.Value &&
                value.NotBeforeUtc <= nowUtc &&
                (value.Status == SupportNotificationOutboxStatus.Pending ||
                 value.Status == SupportNotificationOutboxStatus.Processing && value.LeaseExpiresAtUtc <= nowUtc))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.Status, SupportNotificationOutboxStatus.Processing)
                .SetProperty(value => value.LeaseOwner, workerId)
                .SetProperty(value => value.LeaseExpiresAtUtc, now.Add(LeaseDuration).UtcDateTime)
                .SetProperty(value => value.Attempts, value => value.Attempts + 1)
                .SetProperty(value => value.UpdatedAt, now), cancellationToken);
        if (claimed == 0)
        {
            return null;
        }

        database.ChangeTracker.Clear();
        return await database.SupportNotificationOutbox.SingleAsync(value => value.Id == id.Value, cancellationToken);
    }
}

internal sealed class DisabledSupportNotificationSender : ISupportNotificationSender
{
    public Task<SupportNotificationSendResult> SendAsync(SupportNotification notification, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(SupportNotificationSendResult.Retry("support_notification_capability_unavailable"));
    }
}
