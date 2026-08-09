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

        SupportNotificationSendResult result;
        try
        {
            // A fresh guest token is minted here, in memory, immediately before the send
            // attempt, and is never written back to the durable PayloadJson column - only its
            // hash is ever persisted (via GuestAccessTokenService), matching the ticket
            // creation/rotation security model.
            var effectivePayload = SupportNotificationTemplates.RequiresFreshGuestToken(item.Template)
                ? SupportTicketNotificationPayload.FromJson(item.PayloadJson)
                    .ToJsonWithGuestToken(await guestTokens.RotateAsync(item.TicketId, cancellationToken))
                : item.PayloadJson;
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
        else if (item.Attempts >= MaxAttempts)
        {
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
