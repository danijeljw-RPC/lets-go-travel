using ReadyToGoTravel.Retention.Domain;

namespace ReadyToGoTravel.Retention.Application;

/// <summary>
/// The public contract Booking, Support and Consumer retention sweeps depend on to respect legal
/// holds. Retention never references those modules; they reference this interface only
/// (dependency inversion), preserving the "a module cannot query another module's private tables"
/// boundary from ADR-0009 in both directions.
/// </summary>
public interface ILegalHoldGuard
{
    /// <summary>
    /// Coarse batch-level pre-filter: returns the subset of <paramref name="candidateSubjectIds"/>
    /// that are currently held for <paramref name="recordClass"/>. Callers should still call
    /// <see cref="IsHeldAsync"/> again for each individual candidate immediately before acting on
    /// it, to close the race window where a hold is opened or released while a batch is in flight.
    /// </summary>
    Task<IReadOnlySet<Guid>> ExcludeHeldAsync(
        RetentionRecordClass recordClass,
        RetentionSubjectKind subjectKind,
        IReadOnlyCollection<Guid> candidateSubjectIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Precise, single-subject check. Callers must call this immediately before the destructive
    /// action itself, not only once at the start of a batch.
    /// </summary>
    Task<bool> IsHeldAsync(
        RetentionRecordClass recordClass,
        RetentionSubjectKind subjectKind,
        Guid subjectId,
        CancellationToken cancellationToken = default);
}
