using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Booking.Persistence;
using ReadyToGoTravel.Booking.Reconciliation;

namespace ReadyToGoTravel.Booking.Notifications;

public interface INotificationOutboxProcessor
{
    Task<bool> ProcessNextAsync(
        string workerId,
        CancellationToken cancellationToken = default);

    Task<bool> RequeueAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}

internal sealed class NotificationOutboxProcessor(
    BookingDbContext database,
    ICustomerNotificationSender sender,
    TimeProvider timeProvider) : INotificationOutboxProcessor
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);
    private const int MaxAttempts = 8;

    public async Task<bool> ProcessNextAsync(
        string workerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var nowUtc = now.UtcDateTime;
        var id = await database.NotificationOutbox.AsNoTracking()
            .Where(value =>
                value.NotBeforeUtc <= nowUtc &&
                (value.Status == NotificationOutboxStatus.Pending ||
                 value.Status == NotificationOutboxStatus.Processing && value.LeaseExpiresAtUtc <= nowUtc))
            .OrderBy(value => value.NotBeforeUtc)
            .ThenBy(value => value.Id)
            .Select(value => (Guid?)value.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (!id.HasValue)
        {
            return false;
        }

        var claimed = await database.NotificationOutbox
            .Where(value => value.Id == id.Value &&
                value.NotBeforeUtc <= nowUtc &&
                (value.Status == NotificationOutboxStatus.Pending ||
                 value.Status == NotificationOutboxStatus.Processing && value.LeaseExpiresAtUtc <= nowUtc))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.Status, NotificationOutboxStatus.Processing)
                .SetProperty(value => value.LeaseOwner, workerId)
                .SetProperty(value => value.LeaseExpiresAtUtc, now.Add(LeaseDuration).UtcDateTime)
                .SetProperty(value => value.Attempts, value => value.Attempts + 1)
                .SetProperty(value => value.UpdatedAt, now), cancellationToken);
        if (claimed == 0)
        {
            return false;
        }

        database.ChangeTracker.Clear();
        var item = await database.NotificationOutbox.SingleAsync(value => value.Id == id.Value, cancellationToken);
        NotificationSendResult result;
        try
        {
            result = await sender.SendAsync(
                new CustomerNotification(
                    item.DedupeKey,
                    item.CustomerId,
                    item.CheckoutId,
                    item.ComponentBookingId,
                    item.BookingVersionId,
                    item.Template,
                    item.Locale,
                    item.Severity,
                    item.PayloadJson),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            result = NotificationSendResult.Retry("notification_delivery_failed");
        }

        now = timeProvider.GetUtcNow().ToUniversalTime();
        if (result.Success && !string.IsNullOrWhiteSpace(result.DeliveryReference))
        {
            item.MarkSent(result.DeliveryReference, now);
        }
        else if (result.Retryable && item.Attempts >= MaxAttempts)
        {
            await AddOperationalCaseAsync(item, "notification_retry_exhausted", now, cancellationToken);
            item.Fail("notification_retry_exhausted", now);
        }
        else if (result.Retryable)
        {
            var minutes = Math.Min(60, Math.Pow(2, Math.Min(item.Attempts, 5)));
            item.Retry(result.ErrorCode ?? "notification_delivery_failed", now.AddMinutes(minutes), now);
        }
        else
        {
            var errorCode = result.ErrorCode ?? "notification_delivery_failed";
            await AddOperationalCaseAsync(item, errorCode, now, cancellationToken);
            item.Fail(errorCode, now);
        }

        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RequeueAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var item = await database.NotificationOutbox.SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (item is null || item.Status != NotificationOutboxStatus.Failed)
        {
            return false;
        }

        item.Requeue(timeProvider.GetUtcNow().ToUniversalTime());
        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task AddOperationalCaseAsync(
        NotificationOutboxItem item,
        string errorCode,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var dedupeKey = OperationalCase.CreateDedupeKey(
            "notification",
            item.ComponentBookingId.ToString("N"),
            item.BookingVersionId.ToString("N"),
            errorCode);
        if (await database.OperationalCases.AnyAsync(
                value => value.DedupeKey == dedupeKey,
                cancellationToken))
        {
            return;
        }

        database.OperationalCases.Add(new OperationalCase(
            Guid.CreateVersion7(now),
            item.ComponentBookingId,
            dedupeKey,
            "Notification",
            errorCode,
            now));
    }
}

internal sealed class DisabledCustomerNotificationSender : ICustomerNotificationSender
{
    public Task<NotificationSendResult> SendAsync(
        CustomerNotification notification,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(NotificationSendResult.Retry("notification_capability_unavailable"));
    }
}
