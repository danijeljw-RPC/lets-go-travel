namespace ReadyToGoTravel.Support.Domain;

public enum SupportAuditEventType
{
    GuestLinkIssued,
    GuestLinkRotated,
    GuestLinkRevoked,
    GuestLinkAuthenticated,
    GuestLinkAuthenticationFailed,
    AttachmentUploaded,
    AttachmentScanClean,
    AttachmentScanInfected,
    AttachmentScanFailed,
    AttachmentDownloadAuthorized,
    AttachmentDownloadDenied,
}

public sealed class SupportAuditEvent
{
    private SupportAuditEvent()
    {
    }

    internal SupportAuditEvent(
        Guid id,
        Guid? ticketId,
        SupportAuditEventType eventType,
        string detail,
        DateTimeOffset createdAt)
    {
        Id = id;
        TicketId = ticketId;
        EventType = eventType;
        Detail = detail;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid? TicketId { get; private set; }

    public SupportAuditEventType EventType { get; private set; }

    public string Detail { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }
}
