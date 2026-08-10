namespace ReadyToGoTravel.Support.Http;

public sealed record CreateTicketRequest(
    string? ContactName,
    string? ContactEmail,
    string Category,
    string? BookingReference,
    string Message);

public sealed record AddMessageRequest(string Body);

public sealed record MessageResponse(
    Guid Id,
    int SequenceNumber,
    string AuthorType,
    string Body,
    DateTimeOffset CreatedAt);

public sealed record AttachmentResponse(
    Guid Id,
    Guid MessageId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string ScanStatus,
    DateTimeOffset CreatedAt);

public sealed record TicketResponse(
    Guid Id,
    string ContactName,
    string ContactEmail,
    string Category,
    bool IsUrgent,
    string? BookingReference,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ClosedAt,
    IReadOnlyList<MessageResponse> Messages,
    IReadOnlyList<AttachmentResponse> Attachments);

public sealed record TicketSummaryResponse(
    Guid Id,
    string Category,
    bool IsUrgent,
    string Status,
    DateTimeOffset UpdatedAt);

public sealed record DownloadUrlResponse(string Url, DateTimeOffset ExpiresAt);

public sealed record RotateGuestLinkResponse(bool Delivered);
