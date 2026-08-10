namespace ReadyToGoTravel.Retention.Domain;

public enum LegalHoldAuditEventType
{
    HoldOpened,
    HoldReleased,
    GuardCheckHeld,
}

/// <summary>
/// Append-only compliance audit trail for legal-hold administration and enforcement checks,
/// following the exact shape of ReadyToGoTravel.Support's SupportAuditEvent. Enforced append-only
/// by both the RetentionDbContext.SaveChanges guard and a PostgreSQL trigger (see the initial
/// migration) - legal-hold history is exactly as tamper-evident as canonical booking version
/// history.
/// </summary>
public sealed class LegalHoldAuditEvent
{
    internal LegalHoldAuditEvent(
        Guid id,
        Guid legalHoldId,
        LegalHoldAuditEventType eventType,
        string detail,
        DateTimeOffset createdAtUtc,
        string? actorSubject)
    {
        Id = id;
        LegalHoldId = legalHoldId;
        EventType = eventType;
        Detail = detail;
        CreatedAtUtc = createdAtUtc;
        ActorSubject = actorSubject;
    }

    public Guid Id { get; private set; }

    public Guid LegalHoldId { get; private set; }

    public LegalHoldAuditEventType EventType { get; private set; }

    public string Detail { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Null for a system-initiated guard check; the staff subject for opened/released.</summary>
    public string? ActorSubject { get; private set; }
}
