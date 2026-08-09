namespace ReadyToGoTravel.Support.Domain;

public sealed class SupportAttachment
{
    private SupportAttachment()
    {
    }

    internal SupportAttachment(
        Guid id,
        Guid ticketId,
        Guid messageId,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string storageKey,
        string sha256Checksum,
        string? uploaderCustomerSubject,
        Guid? uploaderGuestTokenId,
        DateTimeOffset createdAt)
    {
        Id = id;
        TicketId = ticketId;
        MessageId = messageId;
        OriginalFileName = originalFileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        StorageKey = storageKey;
        Sha256Checksum = sha256Checksum;
        UploaderCustomerSubject = uploaderCustomerSubject;
        UploaderGuestTokenId = uploaderGuestTokenId;
        ScanStatus = AttachmentScanStatus.Pending;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public Guid MessageId { get; private set; }

    public string OriginalFileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public string StorageKey { get; private set; } = string.Empty;

    public string Sha256Checksum { get; private set; } = string.Empty;

    public string? UploaderCustomerSubject { get; private set; }

    public Guid? UploaderGuestTokenId { get; private set; }

    public AttachmentScanStatus ScanStatus { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal void MarkClean() => ScanStatus = AttachmentScanStatus.Clean;

    internal void MarkInfected() => ScanStatus = AttachmentScanStatus.Infected;

    internal void MarkFailed() => ScanStatus = AttachmentScanStatus.Failed;
}
