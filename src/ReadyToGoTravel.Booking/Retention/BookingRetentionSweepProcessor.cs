using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReadyToGoTravel.Booking.Checkout;
using ReadyToGoTravel.Booking.Notifications;
using ReadyToGoTravel.Booking.Persistence;
using ReadyToGoTravel.Booking.Webhooks;
using ReadyToGoTravel.Retention;
using ReadyToGoTravel.Retention.Application;
using ReadyToGoTravel.Retention.Domain;

namespace ReadyToGoTravel.Booking.Retention;

internal sealed class BookingRetentionSweepProcessor(
    BookingDbContext database,
    ILegalHoldGuard legalHoldGuard,
    IRetentionReceiptRecorder receiptRecorder,
    TimeProvider timeProvider,
    IOptions<RetentionSweepOptions> options) : IBookingRetentionSweepProcessor
{
    private static readonly CheckoutStatus[] AbandonableStatuses =
        [CheckoutStatus.AwaitingAcceptance, CheckoutStatus.ReadyForPayment];

    public async Task<bool> ProcessCycleAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            return false;
        }

        var now = timeProvider.GetUtcNow();
        var didWork = false;
        didWork |= await SweepWebhookPayloadBodiesAsync(now, cancellationToken);
        didWork |= await SweepNotificationRenderedContentAsync(now, cancellationToken);
        didWork |= await SweepAbandonedCheckoutStateAsync(now, cancellationToken);
        return didWork;
    }

    private async Task<bool> SweepWebhookPayloadBodiesAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        const RetentionRecordClass recordClass = RetentionRecordClass.WebhookPayloadBody;
        var policy = RetentionPolicyCatalog.Get(recordClass);

        // The date-boundary filter, ordering and Take all happen client-side after a cheap
        // server-side status/non-empty prefilter: the SQLite provider used in tests cannot
        // translate relational (<=) comparisons on a DateTimeOffset column (PostgreSQL can), and
        // this candidate set is already self-limiting - RawBody != "" excludes every item this
        // sweep has already redacted, so it only ever holds the genuine not-yet-processed backlog.
        // Filtered via the policy catalog (not a manually computed cutoff) so the period-unit
        // (days/years) can never be silently miscalculated here.
        var candidates = (await database.WebhookInbox
            .Where(item => item.Status == WebhookInboxStatus.Completed && item.CompletedAt != null && item.RawBody != "")
            .Select(item => new { item.Id, item.ComponentBookingId, item.CompletedAt })
            .ToListAsync(cancellationToken))
            .Where(item => RetentionPolicyCatalog.IsExpired(recordClass, item.CompletedAt!.Value, now))
            .OrderBy(item => item.CompletedAt)
            .Take(RetentionSweepConstants.BatchSize)
            .ToList();
        if (candidates.Count == 0)
        {
            return false;
        }

        var bookingIds = candidates.Where(c => c.ComponentBookingId.HasValue).Select(c => c.ComponentBookingId!.Value).ToList();
        var held = await legalHoldGuard.ExcludeHeldAsync(recordClass, RetentionSubjectKind.ComponentBooking, bookingIds, cancellationToken);

        var successCount = 0;
        var failureCount = 0;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (candidate.ComponentBookingId.HasValue && held.Contains(candidate.ComponentBookingId.Value))
            {
                continue;
            }

            try
            {
                if (candidate.ComponentBookingId.HasValue &&
                    await legalHoldGuard.IsHeldAsync(recordClass, RetentionSubjectKind.ComponentBooking, candidate.ComponentBookingId.Value, cancellationToken))
                {
                    continue;
                }

                await database.WebhookInbox
                    .Where(item => item.Id == candidate.Id && item.RawBody != "")
                    .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.RawBody, string.Empty), cancellationToken);
                successCount++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                failureCount++;
                await receiptRecorder.RecordOperationalFailureAsync(recordClass, "webhook-payload-body", "redact_failed", now, cancellationToken);
            }
        }

        await receiptRecorder.RecordAsync(recordClass, policy.PolicyVersion, "Delete", successCount, failureCount, now, failureCount > 0 ? "redaction failures occurred" : null, cancellationToken);
        return successCount > 0;
    }

    private async Task<bool> SweepNotificationRenderedContentAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        const RetentionRecordClass recordClass = RetentionRecordClass.NotificationRenderedContent;
        var policy = RetentionPolicyCatalog.Get(recordClass);

        var candidates = (await database.NotificationOutbox
            .Where(item =>
                (item.Status == NotificationOutboxStatus.Sent || item.Status == NotificationOutboxStatus.Failed) &&
                item.PayloadJson != "")
            .Select(item => new { item.Id, item.ComponentBookingId, item.UpdatedAt })
            .ToListAsync(cancellationToken))
            .Where(item => RetentionPolicyCatalog.IsExpired(recordClass, item.UpdatedAt, now))
            .OrderBy(item => item.UpdatedAt)
            .Take(RetentionSweepConstants.BatchSize)
            .ToList();
        if (candidates.Count == 0)
        {
            return false;
        }

        var bookingIds = candidates.Select(c => c.ComponentBookingId).Distinct().ToList();
        var held = await legalHoldGuard.ExcludeHeldAsync(recordClass, RetentionSubjectKind.ComponentBooking, bookingIds, cancellationToken);

        var successCount = 0;
        var failureCount = 0;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (held.Contains(candidate.ComponentBookingId))
            {
                continue;
            }

            try
            {
                if (await legalHoldGuard.IsHeldAsync(recordClass, RetentionSubjectKind.ComponentBooking, candidate.ComponentBookingId, cancellationToken))
                {
                    continue;
                }

                await database.NotificationOutbox
                    .Where(item => item.Id == candidate.Id && item.PayloadJson != "")
                    .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.PayloadJson, string.Empty), cancellationToken);
                successCount++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                failureCount++;
                await receiptRecorder.RecordOperationalFailureAsync(recordClass, "notification-rendered-content", "redact_failed", now, cancellationToken);
            }
        }

        await receiptRecorder.RecordAsync(recordClass, policy.PolicyVersion, "Delete", successCount, failureCount, now, failureCount > 0 ? "redaction failures occurred" : null, cancellationToken);
        return successCount > 0;
    }

    private async Task<bool> SweepAbandonedCheckoutStateAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        const RetentionRecordClass recordClass = RetentionRecordClass.AbandonedCheckoutState;
        var policy = RetentionPolicyCatalog.Get(recordClass);

        var candidates = (await database.Checkouts
            .Where(session => AbandonableStatuses.Contains(session.Status))
            .Select(session => new { session.Id, session.CustomerId, session.UpdatedAt })
            .ToListAsync(cancellationToken))
            .Where(session => RetentionPolicyCatalog.IsExpired(recordClass, session.UpdatedAt, now))
            .OrderBy(session => session.UpdatedAt)
            .Take(RetentionSweepConstants.BatchSize)
            .ToList();
        if (candidates.Count == 0)
        {
            return false;
        }

        var customerIds = candidates.Select(c => c.CustomerId).Distinct().ToList();
        var held = await legalHoldGuard.ExcludeHeldAsync(recordClass, RetentionSubjectKind.Customer, customerIds, cancellationToken);

        var successCount = 0;
        var failureCount = 0;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (held.Contains(candidate.CustomerId))
            {
                continue;
            }

            try
            {
                if (await legalHoldGuard.IsHeldAsync(recordClass, RetentionSubjectKind.Customer, candidate.CustomerId, cancellationToken))
                {
                    continue;
                }

                // Re-verify the eligibility predicate immediately before deleting: the candidate
                // may have progressed past AwaitingAcceptance/ReadyForPayment (e.g. the customer
                // resumed checkout) since it was selected for this batch, in which case it now
                // holds real supplier/payment-interaction risk and must never be swept by this
                // 30-day abandoned-state rule.
                var session = await database.Checkouts.SingleOrDefaultAsync(value => value.Id == candidate.Id, cancellationToken);
                if (session is null)
                {
                    successCount++;
                    continue;
                }

                if (!AbandonableStatuses.Contains(session.Status) || !RetentionPolicyCatalog.IsExpired(recordClass, session.UpdatedAt, now))
                {
                    continue;
                }

                database.Checkouts.Remove(session);
                await database.SaveChangesAsync(cancellationToken);
                successCount++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                failureCount++;
                await receiptRecorder.RecordOperationalFailureAsync(recordClass, "abandoned-checkout-state", "delete_failed", now, cancellationToken);
            }
        }

        await receiptRecorder.RecordAsync(recordClass, policy.PolicyVersion, "Delete", successCount, failureCount, now, failureCount > 0 ? "deletion failures occurred" : null, cancellationToken);
        return successCount > 0;
    }
}
