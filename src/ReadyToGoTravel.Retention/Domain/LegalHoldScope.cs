namespace ReadyToGoTravel.Retention.Domain;

/// <summary>
/// One concrete scope entry of a legal hold: a record class plus exactly one subject key. A scope
/// row deliberately cannot name more than one key kind (customer, component booking or support
/// ticket) at once - to protect multiple subjects, a hold gets multiple scope rows. This keeps
/// matching an unambiguous equality check rather than a mix of AND/OR semantics, and is enforced
/// both here and by a database CHECK constraint (defence in depth).
/// </summary>
public sealed class LegalHoldScope
{
    private LegalHoldScope(
        Guid id,
        Guid legalHoldId,
        RetentionRecordClass recordClass,
        Guid? customerId,
        Guid? componentBookingId,
        Guid? supportTicketId)
    {
        Id = id;
        LegalHoldId = legalHoldId;
        RecordClass = recordClass;
        CustomerId = customerId;
        ComponentBookingId = componentBookingId;
        SupportTicketId = supportTicketId;
    }

    public Guid Id { get; private set; }

    public Guid LegalHoldId { get; private set; }

    public RetentionRecordClass RecordClass { get; private set; }

    public Guid? CustomerId { get; private set; }

    public Guid? ComponentBookingId { get; private set; }

    public Guid? SupportTicketId { get; private set; }

    internal static LegalHoldScope Create(
        Guid legalHoldId,
        RetentionRecordClass recordClass,
        Guid? customerId,
        Guid? componentBookingId,
        Guid? supportTicketId,
        DateTimeOffset now)
    {
        var keyCount = (customerId is not null ? 1 : 0)
            + (componentBookingId is not null ? 1 : 0)
            + (supportTicketId is not null ? 1 : 0);
        if (keyCount != 1)
        {
            throw new ArgumentException(
                "A legal hold scope must name exactly one of customer, component booking or support ticket.");
        }

        return new LegalHoldScope(Guid.CreateVersion7(now), legalHoldId, recordClass, customerId, componentBookingId, supportTicketId);
    }

    public bool Matches(RetentionRecordClass recordClass, RetentionSubjectKind subjectKind, Guid subjectId)
    {
        if (RecordClass != recordClass)
        {
            return false;
        }

        return subjectKind switch
        {
            RetentionSubjectKind.Customer => CustomerId == subjectId,
            RetentionSubjectKind.ComponentBooking => ComponentBookingId == subjectId,
            RetentionSubjectKind.SupportTicket => SupportTicketId == subjectId,
            _ => false,
        };
    }
}

/// <summary>Input to <see cref="LegalHold.Open"/> - not persisted directly.</summary>
public sealed record LegalHoldScopeRequest(
    RetentionRecordClass RecordClass,
    Guid? CustomerId,
    Guid? ComponentBookingId,
    Guid? SupportTicketId);
