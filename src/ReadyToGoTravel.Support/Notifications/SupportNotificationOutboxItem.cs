using System.Security.Cryptography;
using System.Text;

namespace ReadyToGoTravel.Support.Notifications;

public enum SupportNotificationOutboxStatus
{
    Pending,
    Processing,
    Sent,
    Failed,
}

public sealed class SupportNotificationOutboxItem
{
    private SupportNotificationOutboxItem()
    {
    }

    private SupportNotificationOutboxItem(
        Guid id,
        Guid ticketId,
        Guid messageId,
        string dedupeKey,
        string recipientEmail,
        string template,
        string payloadJson,
        DateTimeOffset createdAt)
    {
        Id = id;
        TicketId = ticketId;
        MessageId = messageId;
        DedupeKey = dedupeKey;
        RecipientEmail = recipientEmail;
        Template = template;
        PayloadJson = payloadJson;
        Status = SupportNotificationOutboxStatus.Pending;
        NotBeforeUtc = createdAt.UtcDateTime;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid MessageId { get; private set; }
    public string DedupeKey { get; private set; } = string.Empty;
    public string Channel { get; private set; } = "Email";
    public string RecipientEmail { get; private set; } = string.Empty;
    public string Template { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = string.Empty;
    public SupportNotificationOutboxStatus Status { get; private set; }
    public int Attempts { get; private set; }
    internal DateTime NotBeforeUtc { get; private set; }
    public string? LeaseOwner { get; private set; }
    internal DateTime? LeaseExpiresAtUtc { get; private set; }
    public string? DeliveryReference { get; private set; }
    public string? ErrorCode { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    internal static SupportNotificationOutboxItem Create(
        Guid ticketId,
        Guid messageId,
        string recipientEmail,
        string template,
        string payloadJson,
        DateTimeOffset now)
    {
        var keyMaterial = $"{ticketId:N}:{messageId:N}:Email:{template}";
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(keyMaterial)));
        return new SupportNotificationOutboxItem(
            Guid.CreateVersion7(now), ticketId, messageId, key, recipientEmail, template, payloadJson, now);
    }

    internal void MarkSent(string deliveryReference, DateTimeOffset now)
    {
        Status = SupportNotificationOutboxStatus.Sent;
        DeliveryReference = deliveryReference;
        ErrorCode = null;
        LeaseOwner = null;
        LeaseExpiresAtUtc = null;
        UpdatedAt = now;
    }

    internal void Retry(string errorCode, DateTimeOffset notBefore, DateTimeOffset now)
    {
        Status = SupportNotificationOutboxStatus.Pending;
        ErrorCode = errorCode;
        NotBeforeUtc = notBefore.UtcDateTime;
        LeaseOwner = null;
        LeaseExpiresAtUtc = null;
        UpdatedAt = now;
    }

    internal void Fail(string errorCode, DateTimeOffset now)
    {
        Status = SupportNotificationOutboxStatus.Failed;
        ErrorCode = errorCode;
        LeaseOwner = null;
        LeaseExpiresAtUtc = null;
        UpdatedAt = now;
    }

    internal void Requeue(DateTimeOffset now)
    {
        Status = SupportNotificationOutboxStatus.Pending;
        Attempts = 0;
        ErrorCode = null;
        NotBeforeUtc = now.UtcDateTime;
        LeaseOwner = null;
        LeaseExpiresAtUtc = null;
        UpdatedAt = now;
    }
}

public sealed record SupportNotification(
    string DedupeKey,
    string RecipientEmail,
    string Template,
    string PayloadJson);

public sealed record SupportNotificationSendResult(
    bool Success,
    bool Retryable,
    string? DeliveryReference,
    string? ErrorCode)
{
    public static SupportNotificationSendResult Sent(string deliveryReference) => new(true, false, deliveryReference, null);

    public static SupportNotificationSendResult Retry(string errorCode) => new(false, true, null, errorCode);
}

public interface ISupportNotificationSender
{
    Task<SupportNotificationSendResult> SendAsync(SupportNotification notification, CancellationToken cancellationToken = default);
}
