using ReadyToGoTravel.Retention.Domain;

namespace ReadyToGoTravel.Retention.Application;

/// <summary>
/// The public contract Booking, Support and Consumer retention sweeps depend on to record
/// non-sensitive deletion receipts and operational failures, centralising both concepts in the
/// Retention module's own schema rather than duplicating an audit table per module.
/// </summary>
public interface IRetentionReceiptRecorder
{
    /// <summary>One receipt per sweep batch per record class. Must never receive deleted personal content.</summary>
    Task RecordAsync(
        RetentionRecordClass recordClass,
        int policyVersion,
        string action,
        int successCount,
        int failureCount,
        DateTimeOffset completedAtUtc,
        string? failureSummary,
        CancellationToken cancellationToken = default);

    /// <summary>Idempotent, dedupe-keyed: repeated identical failures do not create duplicate cases.</summary>
    Task RecordOperationalFailureAsync(
        RetentionRecordClass recordClass,
        string scope,
        string reason,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
}
