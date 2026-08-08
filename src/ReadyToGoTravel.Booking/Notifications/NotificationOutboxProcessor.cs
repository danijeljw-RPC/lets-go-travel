using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Booking.Persistence;

namespace ReadyToGoTravel.Booking.Notifications;

public interface INotificationOutboxProcessor
{
    Task<bool> ProcessNextAsync(
        string workerId,
        CancellationToken cancellationToken = default);
}

internal sealed class NotificationOutboxProcessor(
    BookingDbContext database,
    ICustomerNotificationSender sender,
    TimeProvider timeProvider) : INotificationOutboxProcessor
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);

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
        else if (result.Retryable)
        {
            var minutes = Math.Min(60, Math.Pow(2, Math.Min(item.Attempts, 5)));
            item.Retry(result.ErrorCode ?? "notification_delivery_failed", now.AddMinutes(minutes), now);
        }
        else
        {
            item.Fail(result.ErrorCode ?? "notification_delivery_failed", now);
        }

        await database.SaveChangesAsync(cancellationToken);
        return true;
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
