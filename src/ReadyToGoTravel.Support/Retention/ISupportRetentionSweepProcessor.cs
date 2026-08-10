namespace ReadyToGoTravel.Support.Retention;

public interface ISupportRetentionSweepProcessor
{
    /// <summary>
    /// Sweeps every Support-owned retention record class once (attachments, general and
    /// booking-related tickets, security/audit records), each in a bounded, idempotent batch.
    /// Returns whether any candidate was actually acted on. A disabled Retention:Enabled flag
    /// makes this a no-op.
    /// </summary>
    Task<bool> ProcessCycleAsync(CancellationToken cancellationToken = default);
}
