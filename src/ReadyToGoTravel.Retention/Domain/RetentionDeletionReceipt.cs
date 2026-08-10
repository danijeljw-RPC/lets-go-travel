namespace ReadyToGoTravel.Retention.Domain;

/// <summary>
/// A non-sensitive audit receipt for one sweep batch of one record class
/// (docs/security/data-retention-and-legal-hold.md "Deletion Receipt"). Must never carry deleted
/// personal content - only counts, an action label and a short failure summary.
/// </summary>
public sealed class RetentionDeletionReceipt
{
    internal RetentionDeletionReceipt(
        Guid id,
        RetentionRecordClass recordClass,
        int policyVersion,
        string action,
        int successCount,
        int failureCount,
        DateTimeOffset completedAtUtc,
        string? failureSummary)
    {
        Id = id;
        RecordClass = recordClass;
        PolicyVersion = policyVersion;
        Action = action;
        SuccessCount = successCount;
        FailureCount = failureCount;
        CompletedAtUtc = completedAtUtc;
        FailureSummary = failureSummary;
    }

    public Guid Id { get; private set; }

    public RetentionRecordClass RecordClass { get; private set; }

    public int PolicyVersion { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public int SuccessCount { get; private set; }

    public int FailureCount { get; private set; }

    public DateTimeOffset CompletedAtUtc { get; private set; }

    public string? FailureSummary { get; private set; }
}
