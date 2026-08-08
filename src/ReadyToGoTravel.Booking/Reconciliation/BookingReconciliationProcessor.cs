using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Booking.Checkout;
using ReadyToGoTravel.Booking.Notifications;
using ReadyToGoTravel.Booking.Persistence;
using ReadyToGoTravel.Booking.Providers;

namespace ReadyToGoTravel.Booking.Reconciliation;

public interface IReconciliationWorkProcessor
{
    Task<bool> ProcessNextAsync(
        CheckoutProduct? product,
        string workerId,
        CancellationToken cancellationToken = default);
}

internal sealed class BookingReconciliationProcessor(
    BookingDbContext database,
    IBookingProvider provider,
    ReconciliationScheduler scheduler,
    TimeProvider timeProvider) : IReconciliationWorkProcessor
{
    public async Task<bool> ProcessNextAsync(
        CheckoutProduct? product,
        string workerId,
        CancellationToken cancellationToken = default)
    {
        var work = await scheduler.ClaimNextAsync(product, workerId, cancellationToken);
        if (work is null)
        {
            return false;
        }

        var startedAt = timeProvider.GetUtcNow().ToUniversalTime();
        var component = await database.ComponentBookings
            .SingleOrDefaultAsync(value => value.Id == work.ComponentBookingId, cancellationToken);
        if (component is null || string.IsNullOrWhiteSpace(component.ProviderBookingReference))
        {
            await FailPermanentlyAsync(work, "booking_reference_unavailable", startedAt, cancellationToken);
            return true;
        }

        BookingProviderExecutionResult result;
        try
        {
            result = await provider.RetrieveAsync(component.ProviderBookingReference, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            await RetryAsync(work, "supplier_retrieval_failed", startedAt, cancellationToken);
            return true;
        }

        if (!string.Equals(result.ExternalReference, component.ProviderBookingReference, StringComparison.Ordinal))
        {
            await FailPermanentlyAsync(work, "booking_reference_mismatch", startedAt, cancellationToken);
            return true;
        }

        if (result.RetrievedState is null)
        {
            await RetryAsync(work, "supplier_state_unavailable", startedAt, cancellationToken);
            return true;
        }

        if (result.RetrievedState.Product != component.Product)
        {
            await FailPermanentlyAsync(work, "booking_product_mismatch", startedAt, cancellationToken);
            return true;
        }

        var checkoutId = database.Entry(component).Property<Guid>("checkout_session_id").CurrentValue;
        var checkout = await database.Checkouts
            .Include(value => value.Components)
            .SingleAsync(value => value.Id == checkoutId, cancellationToken);
        component = checkout.Components.Single(value => value.Id == component.Id);
        var previous = await database.BookingVersions
            .Where(value => value.ComponentBookingId == component.Id)
            .OrderByDescending(value => value.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var version = CanonicalBookingVersioner.Create(
            component,
            result.RetrievedState,
            previous,
            now,
            work.Source,
            work.CorrelationId);
        var changed = component.ApplyReconciliationVersion(version, now);
        checkout.ApplyReconciledComponentStatus(component.Id, result.RetrievedState.Status, now);
        if (changed)
        {
            var classification = BookingChangeClassifier.Classify(previous, version);
            version.SetSeverity(classification.Severity.ToString());
            database.BookingVersions.Add(version);
            if (previous is not null && classification.ShouldEmail)
            {
                using var diffDocument = JsonDocument.Parse(version.DiffJson);
                var payload = JsonSerializer.Serialize(new
                {
                    componentId = component.Id,
                    product = component.Product.ToString(),
                    versionNumber = version.VersionNumber,
                    previousVersionId = previous.Id,
                    versionId = version.Id,
                    flags = classification.Flags,
                    diff = diffDocument.RootElement,
                });
                database.NotificationOutbox.Add(NotificationOutboxItem.Create(
                    checkout.CustomerId,
                    checkout.Id,
                    component.Id,
                    version.Id,
                    classification,
                    checkout.CustomerLocale,
                    checkout.NotificationTimeZoneId,
                    payload,
                    now));
                if (classification.Severity == BookingChangeSeverity.TravelBlocking)
                {
                    database.OperationalCases.Add(new OperationalCase(
                        Guid.CreateVersion7(now),
                        component.Id,
                        $"booking-change:{component.Id:N}:{version.Id:N}",
                        "TravelBlockingChange",
                        "customer_action_required",
                        now));
                }
            }
        }

        database.ReconciliationAttempts.Add(new ReconciliationAttempt(
            Guid.CreateVersion7(now),
            work.Id,
            component.Id,
            work.Attempts,
            startedAt,
            now,
            ReconciliationAttemptOutcome.Succeeded,
            null,
            work.CorrelationId));
        var nextDue = NextDueAt(result.RetrievedState, now);
        if (nextDue.HasValue)
        {
            work.Reschedule(nextDue.Value, now);
        }
        else
        {
            work.Complete(now);
        }

        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task RetryAsync(
        ReconciliationWork work,
        string errorCode,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var delayMinutes = Math.Min(60, Math.Pow(2, Math.Min(work.Attempts, 5)));
        work.Retry(errorCode, now.AddMinutes(delayMinutes), now);
        database.ReconciliationAttempts.Add(new ReconciliationAttempt(
            Guid.CreateVersion7(now),
            work.Id,
            work.ComponentBookingId,
            work.Attempts,
            startedAt,
            now,
            ReconciliationAttemptOutcome.Retrying,
            errorCode,
            work.CorrelationId));
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task FailPermanentlyAsync(
        ReconciliationWork work,
        string errorCode,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().ToUniversalTime();
        work.Complete(now);
        database.ReconciliationAttempts.Add(new ReconciliationAttempt(
            Guid.CreateVersion7(now),
            work.Id,
            work.ComponentBookingId,
            work.Attempts,
            startedAt,
            now,
            ReconciliationAttemptOutcome.Failed,
            errorCode,
            work.CorrelationId));
        database.OperationalCases.Add(new OperationalCase(
            Guid.CreateVersion7(now),
            work.ComponentBookingId,
            $"reconciliation:{work.ComponentBookingId:N}:{errorCode}",
            "Reconciliation",
            errorCode,
            now));
        await database.SaveChangesAsync(cancellationToken);
    }

    internal static DateTimeOffset? NextDueAt(RetrievedBookingState state, DateTimeOffset now)
    {
        if (state.Status is RetrievedBookingStatus.Cancelled or RetrievedBookingStatus.Failed or RetrievedBookingStatus.Completed)
        {
            return null;
        }

        if (state.Product == CheckoutProduct.Hotel)
        {
            return now.AddDays(1);
        }

        var departure = state.FlightSegments
            .Select(value => value.ScheduledDeparture.ToUniversalTime())
            .Where(value => value > now)
            .DefaultIfEmpty()
            .Min();
        if (departure == default)
        {
            return null;
        }

        return departure - now <= TimeSpan.FromHours(24)
            ? now.AddHours(1)
            : now.AddDays(1);
    }
}
