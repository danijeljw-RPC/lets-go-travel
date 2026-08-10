namespace ReadyToGoTravel.Retention.Domain;

/// <summary>The kind of key a legal-hold scope or a retention candidate is identified by.</summary>
public enum RetentionSubjectKind
{
    Customer,
    ComponentBooking,
    SupportTicket,
}
