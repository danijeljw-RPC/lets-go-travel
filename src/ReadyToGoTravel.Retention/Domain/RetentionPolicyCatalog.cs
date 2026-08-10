using System.Collections.ObjectModel;

namespace ReadyToGoTravel.Retention.Domain;

/// <summary>
/// The approved retention schedule from docs/security/data-retention-and-legal-hold.md and closed
/// OI-0011, encoded as data. This is deliberately a fixed, explicit table rather than a generic
/// rules engine: the documented schedule is a known, closed set of record classes, not an
/// open-ended one, and a compiler-checked enum plus an explicit switch keeps every class's policy
/// directly traceable to the document row it implements.
/// </summary>
public static class RetentionPolicyCatalog
{
    private static readonly ReadOnlyDictionary<RetentionRecordClass, RetentionPolicyDefinition> Definitions =
        new Dictionary<RetentionRecordClass, RetentionPolicyDefinition>
        {
            [RetentionRecordClass.BookingRelatedSupportTicket] = new(
                RetentionRecordClass.BookingRelatedSupportTicket,
                PolicyVersion: 1,
                PeriodValue: 7,
                RetentionPeriodUnit.Years,
                "Later of ticket closure or final linked booking/payment dispute resolution (implemented as ticket closure; see design doc).",
                RetentionAction.Delete),
            [RetentionRecordClass.GeneralSupportTicket] = new(
                RetentionRecordClass.GeneralSupportTicket,
                PolicyVersion: 1,
                PeriodValue: 2,
                RetentionPeriodUnit.Years,
                "Ticket closure.",
                RetentionAction.Delete),
            [RetentionRecordClass.SupportAttachment] = new(
                RetentionRecordClass.SupportAttachment,
                PolicyVersion: 1,
                PeriodValue: 90,
                RetentionPeriodUnit.Days,
                "Ticket closure (not attachment creation).",
                RetentionAction.Delete),
            [RetentionRecordClass.SecurityAuditRecord] = new(
                RetentionRecordClass.SecurityAuditRecord,
                PolicyVersion: 1,
                PeriodValue: 2,
                RetentionPeriodUnit.Years,
                "Audit event.",
                RetentionAction.Delete),
            [RetentionRecordClass.WebhookPayloadBody] = new(
                RetentionRecordClass.WebhookPayloadBody,
                PolicyVersion: 1,
                PeriodValue: 90,
                RetentionPeriodUnit.Days,
                "Successful processing and reconciliation of the event.",
                RetentionAction.Delete),
            [RetentionRecordClass.NotificationRenderedContent] = new(
                RetentionRecordClass.NotificationRenderedContent,
                PolicyVersion: 1,
                PeriodValue: 90,
                RetentionPeriodUnit.Days,
                "Final delivery attempt.",
                RetentionAction.Delete),
            [RetentionRecordClass.AbandonedCheckoutState] = new(
                RetentionRecordClass.AbandonedCheckoutState,
                PolicyVersion: 1,
                PeriodValue: 30,
                RetentionPeriodUnit.Days,
                "Last customer or system activity.",
                RetentionAction.Delete),
            [RetentionRecordClass.CanonicalBookingEvidence] = new(
                RetentionRecordClass.CanonicalBookingEvidence,
                PolicyVersion: 1,
                PeriodValue: 7,
                RetentionPeriodUnit.Years,
                "Later of final travel completion, cancellation, final supplier/customer settlement, refund, chargeback or dispute resolution.",
                RetentionAction.PolicyOnlyNoLiveSweep),
            [RetentionRecordClass.SuccessfulSupplierPayload] = new(
                RetentionRecordClass.SuccessfulSupplierPayload,
                PolicyVersion: 1,
                PeriodValue: 90,
                RetentionPeriodUnit.Days,
                "Successful completion and reconciliation of the related supplier operation.",
                RetentionAction.PolicyOnlyNoLiveSweep),
            [RetentionRecordClass.ExceptionalSupplierPayload] = new(
                RetentionRecordClass.ExceptionalSupplierPayload,
                PolicyVersion: 1,
                PeriodValue: 1,
                RetentionPeriodUnit.Years,
                "Resolution of the ambiguous operation, mapping failure, incident or dispute.",
                RetentionAction.PolicyOnlyNoLiveSweep),
            [RetentionRecordClass.TravellerSensitiveFieldMinimisation] = new(
                RetentionRecordClass.TravellerSensitiveFieldMinimisation,
                PolicyVersion: 1,
                PeriodValue: 90,
                RetentionPeriodUnit.Days,
                "Final travel completion.",
                RetentionAction.PolicyOnlyNoLiveSweep),
        }.AsReadOnly();

    public static RetentionPolicyDefinition Get(RetentionRecordClass recordClass) =>
        Definitions.TryGetValue(recordClass, out var definition)
            ? definition
            : throw new ArgumentOutOfRangeException(
                nameof(recordClass), recordClass, "No retention policy is defined for this record class.");

    public static DateTimeOffset CalculateExpiry(RetentionRecordClass recordClass, DateTimeOffset triggerAtUtc) =>
        Get(recordClass).CalculateExpiry(triggerAtUtc);

    /// <summary>Inclusive at the exact expiry instant: a record is expired the moment it reaches its boundary.</summary>
    public static bool IsExpired(RetentionRecordClass recordClass, DateTimeOffset triggerAtUtc, DateTimeOffset nowUtc) =>
        nowUtc >= CalculateExpiry(recordClass, triggerAtUtc);
}
