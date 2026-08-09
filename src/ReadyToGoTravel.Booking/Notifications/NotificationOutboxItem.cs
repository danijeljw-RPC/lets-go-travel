using System.Security.Cryptography;
using System.Text;

namespace ReadyToGoTravel.Booking.Notifications;

public enum NotificationOutboxStatus
{
    Pending,
    Processing,
    Sent,
    Failed,
}

public sealed class NotificationOutboxItem
{
    private const string TemplateVersion = "booking-change-v1";

    private NotificationOutboxItem()
    {
    }

    private NotificationOutboxItem(
        Guid id,
        Guid customerId,
        Guid checkoutId,
        Guid componentBookingId,
        Guid bookingVersionId,
        string dedupeKey,
        BookingChangeSeverity severity,
        string locale,
        string timeZoneId,
        string payloadJson,
        DateTimeOffset notBefore,
        DateTimeOffset createdAt)
    {
        Id = id;
        CustomerId = customerId;
        CheckoutId = checkoutId;
        ComponentBookingId = componentBookingId;
        BookingVersionId = bookingVersionId;
        DedupeKey = dedupeKey;
        Severity = severity;
        Locale = locale;
        TimeZoneId = timeZoneId;
        PayloadJson = payloadJson;
        NotBeforeUtc = notBefore.UtcDateTime;
        Status = NotificationOutboxStatus.Pending;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid CheckoutId { get; private set; }
    public Guid ComponentBookingId { get; private set; }
    public Guid BookingVersionId { get; private set; }
    public string DedupeKey { get; private set; } = string.Empty;
    public string Channel { get; private set; } = "Email";
    public string Template { get; private set; } = TemplateVersion;
    public BookingChangeSeverity Severity { get; private set; }
    public string Locale { get; private set; } = string.Empty;
    public string TimeZoneId { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = string.Empty;
    public NotificationOutboxStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public DateTimeOffset NotBefore => new(DateTime.SpecifyKind(NotBeforeUtc, DateTimeKind.Utc));
    internal DateTime NotBeforeUtc { get; private set; }
    public string? LeaseOwner { get; private set; }
    internal DateTime? LeaseExpiresAtUtc { get; private set; }
    public string? DeliveryReference { get; private set; }
    public string? ErrorCode { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    internal static NotificationOutboxItem Create(
        Guid customerId,
        Guid checkoutId,
        Guid componentBookingId,
        Guid bookingVersionId,
        BookingChangeClassification classification,
        string locale,
        string timeZoneId,
        string payloadJson,
        DateTimeOffset observedAt)
    {
        ArgumentNullException.ThrowIfNull(classification);
        var keyMaterial = $"{customerId:N}:{componentBookingId:N}:{bookingVersionId:N}:Email:{TemplateVersion}";
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(keyMaterial)));
        var notBefore = CalculateNotBefore(classification.Severity, timeZoneId, observedAt);
        return new NotificationOutboxItem(
            Guid.CreateVersion7(observedAt),
            customerId,
            checkoutId,
            componentBookingId,
            bookingVersionId,
            key,
            classification.Severity,
            locale,
            timeZoneId,
            payloadJson,
            notBefore,
            observedAt);
    }

    internal void MarkSent(string deliveryReference, DateTimeOffset now)
    {
        Status = NotificationOutboxStatus.Sent;
        DeliveryReference = deliveryReference;
        ErrorCode = null;
        LeaseOwner = null;
        LeaseExpiresAtUtc = null;
        UpdatedAt = now;
    }

    internal void Retry(string errorCode, DateTimeOffset notBefore, DateTimeOffset now)
    {
        Status = NotificationOutboxStatus.Pending;
        ErrorCode = errorCode;
        NotBeforeUtc = notBefore.UtcDateTime;
        LeaseOwner = null;
        LeaseExpiresAtUtc = null;
        UpdatedAt = now;
    }

    internal void Fail(string errorCode, DateTimeOffset now)
    {
        Status = NotificationOutboxStatus.Failed;
        ErrorCode = errorCode;
        LeaseOwner = null;
        LeaseExpiresAtUtc = null;
        UpdatedAt = now;
    }

    internal void Requeue(DateTimeOffset now)
    {
        Status = NotificationOutboxStatus.Pending;
        Attempts = 0;
        ErrorCode = null;
        NotBeforeUtc = now.UtcDateTime;
        LeaseOwner = null;
        LeaseExpiresAtUtc = null;
        UpdatedAt = now;
    }

    private static DateTimeOffset CalculateNotBefore(
        BookingChangeSeverity severity,
        string timeZoneId,
        DateTimeOffset observedAt)
    {
        if (severity is BookingChangeSeverity.Material or BookingChangeSeverity.TravelBlocking)
        {
            return observedAt.ToUniversalTime();
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var local = TimeZoneInfo.ConvertTime(observedAt, timeZone);
        if (local.Hour >= 22)
        {
            var next = new DateTimeOffset(
                local.Date.AddDays(1).AddHours(7),
                timeZone.GetUtcOffset(local.Date.AddDays(1).AddHours(7)));
            return next.ToUniversalTime();
        }

        if (local.Hour < 7)
        {
            var next = new DateTimeOffset(
                local.Date.AddHours(7),
                timeZone.GetUtcOffset(local.Date.AddHours(7)));
            return next.ToUniversalTime();
        }

        return observedAt.ToUniversalTime();
    }
}

public sealed record CustomerNotification(
    string IdempotencyKey,
    Guid CustomerId,
    Guid CheckoutId,
    Guid ComponentBookingId,
    Guid BookingVersionId,
    string Template,
    string Locale,
    BookingChangeSeverity Severity,
    string PayloadJson);

public sealed record NotificationSendResult(
    bool Success,
    bool Retryable,
    string? DeliveryReference,
    string? ErrorCode)
{
    public static NotificationSendResult Sent(string deliveryReference) =>
        new(true, false, deliveryReference, null);

    public static NotificationSendResult Retry(string errorCode) =>
        new(false, true, null, errorCode);

    public static NotificationSendResult Failed(string errorCode) =>
        new(false, false, null, errorCode);
}

public interface ICustomerNotificationSender
{
    Task<NotificationSendResult> SendAsync(
        CustomerNotification notification,
        CancellationToken cancellationToken = default);
}
