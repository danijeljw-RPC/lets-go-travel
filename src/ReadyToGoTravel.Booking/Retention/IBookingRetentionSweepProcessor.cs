namespace ReadyToGoTravel.Booking.Retention;

public interface IBookingRetentionSweepProcessor
{
    /// <summary>
    /// Sweeps every Booking-owned retention record class once (webhook payload bodies,
    /// notification rendered content, abandoned checkout state), each in a bounded, idempotent
    /// batch. Returns whether any candidate was actually acted on, for the worker's didWork chain.
    /// A disabled Retention:Enabled flag makes this a no-op.
    /// </summary>
    Task<bool> ProcessCycleAsync(CancellationToken cancellationToken = default);
}
