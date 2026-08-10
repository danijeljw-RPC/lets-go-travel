using System.Text.Json;

namespace ReadyToGoTravel.Support.Notifications;

internal static class SupportNotificationTemplates
{
    public const string AcknowledgementGuest = "SupportTicketAcknowledgementGuest";
    public const string AcknowledgementCustomer = "SupportTicketAcknowledgementCustomer";
    public const string MessageAdded = "SupportTicketMessageAdded";
    public const string TicketClosed = "SupportTicketClosed";
    public const string GuestLinkRotated = "SupportGuestLinkRotated";

    // Only the guest acknowledgement is delivered through the durable outbox and needs a
    // freshly minted token at send time. Guest-link rotation is staff-initiated and delivered
    // synchronously (see SupportStaffEndpoints), never through this outbox.
    public static bool RequiresFreshGuestToken(string template) => template == AcknowledgementGuest;
}

internal sealed record SupportTicketNotificationPayload(
    string ContactName,
    Guid TicketId,
    string Category)
{
    public string ToJson() => JsonSerializer.Serialize(this);

    public static SupportTicketNotificationPayload FromJson(string json) =>
        JsonSerializer.Deserialize<SupportTicketNotificationPayload>(json)!;

    /// <summary>
    /// Builds a payload that carries a freshly minted raw guest token. This value is only ever
    /// constructed in memory immediately before a send attempt and must never be persisted -
    /// the durable outbox row's PayloadJson never contains a raw token.
    /// </summary>
    public string ToJsonWithGuestToken(string rawGuestToken) =>
        JsonSerializer.Serialize(new SupportTicketNotificationPayloadWithToken(ContactName, TicketId, Category, rawGuestToken));

    private sealed record SupportTicketNotificationPayloadWithToken(
        string ContactName,
        Guid TicketId,
        string Category,
        string GuestToken);
}
