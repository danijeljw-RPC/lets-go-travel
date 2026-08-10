using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Booking.Checkout;
using ReadyToGoTravel.Booking.Persistence;

namespace ReadyToGoTravel.Booking.Reconciliation;

public interface IReconciliationScheduler
{
    Task EnqueueImmediateAsync(
        Guid componentBookingId,
        string source,
        string correlationId,
        CancellationToken cancellationToken = default);
}

internal sealed class ReconciliationScheduler(
    BookingDbContext database,
    TimeProvider timeProvider) : IReconciliationScheduler
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);

    public async Task EnqueueImmediateAsync(
        Guid componentBookingId,
        string source,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var component = database.ComponentBookings.Local
            .SingleOrDefault(value => value.Id == componentBookingId)
            ?? await database.ComponentBookings
                .AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == componentBookingId, cancellationToken)
            ?? throw new InvalidOperationException("Component booking was not found.");
        if (string.IsNullOrWhiteSpace(component.ProviderBookingReference))
        {
            throw new InvalidOperationException("A provider booking reference is required for reconciliation.");
        }

        var work = await database.ReconciliationWork
            .SingleOrDefaultAsync(value => value.ComponentBookingId == componentBookingId, cancellationToken);
        if (work is null)
        {
            database.ReconciliationWork.Add(new ReconciliationWork(
                Guid.CreateVersion7(now),
                componentBookingId,
                component.Product,
                now,
                source,
                correlationId,
                now));
        }
        else
        {
            work.BringForward(now, source, correlationId, now);
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    internal async Task<ReconciliationWork?> ClaimNextAsync(
        CheckoutProduct? product,
        string workerId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var nowUtc = now.UtcDateTime;
        var candidates = database.ReconciliationWork.AsNoTracking()
            .Where(value =>
                value.DueAtUtc != null && value.DueAtUtc <= nowUtc &&
                (value.Status == ReconciliationWorkStatus.Pending ||
                 value.Status == ReconciliationWorkStatus.Processing && value.LeaseExpiresAtUtc <= nowUtc));
        if (product.HasValue)
        {
            candidates = candidates.Where(value => value.Product == product.Value);
        }

        var candidateId = await candidates
            .OrderBy(value => value.DueAtUtc)
            .ThenBy(value => value.Id)
            .Select(value => (Guid?)value.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (!candidateId.HasValue)
        {
            return null;
        }

        var claimed = await database.ReconciliationWork
            .Where(value => value.Id == candidateId.Value &&
                value.DueAtUtc != null && value.DueAtUtc <= nowUtc &&
                (value.Status == ReconciliationWorkStatus.Pending ||
                 value.Status == ReconciliationWorkStatus.Processing && value.LeaseExpiresAtUtc <= nowUtc))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.Status, ReconciliationWorkStatus.Processing)
                .SetProperty(value => value.LeaseOwner, workerId)
                .SetProperty(value => value.LeaseExpiresAtUtc, now.Add(LeaseDuration).UtcDateTime)
                .SetProperty(value => value.Attempts, value => value.Attempts + 1)
                .SetProperty(value => value.UpdatedAt, now), cancellationToken);
        if (claimed == 0)
        {
            return null;
        }

        database.ChangeTracker.Clear();
        return await database.ReconciliationWork.SingleAsync(value => value.Id == candidateId.Value, cancellationToken);
    }
}
