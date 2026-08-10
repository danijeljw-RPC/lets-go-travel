namespace ReadyToGoTravel.Consumer.Retention;

public interface IConsumerRetentionSweepProcessor
{
    /// <summary>
    /// Minimises eligible personal-preference fields for accounts closed at least 90 days ago that
    /// have no protected booking or support evidence (see IConsumerRetentionEvidencePort). Returns
    /// whether any candidate was actually acted on. A disabled Retention:Enabled flag makes this a
    /// no-op.
    /// </summary>
    Task<bool> ProcessCycleAsync(CancellationToken cancellationToken = default);
}
