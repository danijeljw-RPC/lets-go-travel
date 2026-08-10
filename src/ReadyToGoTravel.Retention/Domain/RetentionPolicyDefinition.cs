namespace ReadyToGoTravel.Retention.Domain;

/// <summary>Calendar-period unit a policy's retention window is expressed in.</summary>
public enum RetentionPeriodUnit
{
    Days,
    Years,
}

/// <summary>
/// One row of the approved retention schedule, expressed as pure data. The trigger event itself
/// (for example "ticket closure" or "successful processing and reconciliation") is documentary -
/// callers supply the actual trigger timestamp already resolved from their own record; this type
/// only knows how to turn a trigger timestamp into an expiry timestamp.
/// </summary>
public sealed record RetentionPolicyDefinition(
    RetentionRecordClass RecordClass,
    int PolicyVersion,
    int PeriodValue,
    RetentionPeriodUnit PeriodUnit,
    string TriggerDescription,
    RetentionAction Action)
{
    public DateTimeOffset CalculateExpiry(DateTimeOffset triggerAtUtc) => PeriodUnit switch
    {
        RetentionPeriodUnit.Days => triggerAtUtc.AddDays(PeriodValue),
        RetentionPeriodUnit.Years => triggerAtUtc.AddYears(PeriodValue),
        _ => throw new InvalidOperationException($"Unsupported retention period unit '{PeriodUnit}'."),
    };
}
