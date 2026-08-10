namespace ReadyToGoTravel.Retention.Domain;

/// <summary>
/// Every record class named in the approved retention baseline
/// (docs/security/data-retention-and-legal-hold.md and closed OI-0011). Not every
/// value has a live sweep implemented against it yet - see
/// docs/superpowers/specs/2026-08-10-slice-7-retention-privacy-production-readiness-design.md
/// for which classes are policy-only and why.
/// </summary>
public enum RetentionRecordClass
{
    /// <summary>Support ticket thread evidence with a non-empty booking reference.</summary>
    BookingRelatedSupportTicket,

    /// <summary>Support ticket thread evidence with no booking or financial relationship.</summary>
    GeneralSupportTicket,

    /// <summary>Private object-storage attachments to a support ticket.</summary>
    SupportAttachment,

    /// <summary>Security and privileged-access audit records.</summary>
    SecurityAuditRecord,

    /// <summary>Raw webhook request bodies stored in the durable inbox.</summary>
    WebhookPayloadBody,

    /// <summary>Rendered customer-notification content stored in the durable outbox.</summary>
    NotificationRenderedContent,

    /// <summary>Search results, abandoned offers and unconfirmed checkout state.</summary>
    AbandonedCheckoutState,

    /// <summary>Canonical booking, price, payment, refund and immutable version evidence.</summary>
    CanonicalBookingEvidence,

    /// <summary>Successful allowlisted raw supplier request/response payloads.</summary>
    SuccessfulSupplierPayload,

    /// <summary>Raw payloads for ambiguous, mapping-failure, incident or dispute operations.</summary>
    ExceptionalSupplierPayload,

    /// <summary>Reusable date-of-birth, contact and direct traveller-identification fields.</summary>
    TravellerSensitiveFieldMinimisation,
}
