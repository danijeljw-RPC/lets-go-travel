namespace ReadyToGoTravel.Support.Domain;

public sealed class SupportGuestAccessToken
{
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(30);

    private SupportGuestAccessToken()
    {
    }

    internal SupportGuestAccessToken(
        Guid id,
        Guid ticketId,
        string tokenHash,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        Guid? rotatedFromTokenId,
        Guid? issuedForOutboxItemId = null)
    {
        Id = id;
        TicketId = ticketId;
        TokenHash = tokenHash;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        RotatedFromTokenId = rotatedFromTokenId;
        IssuedForOutboxItemId = issuedForOutboxItemId;
    }

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset IssuedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public DateTimeOffset? LastUsedAt { get; private set; }

    public Guid? RotatedFromTokenId { get; private set; }

    // Set only when a token is minted on behalf of a specific durable outbox notification (the
    // guest acknowledgement email). It lets the outbox processor tell "a token that is still mine
    // to deliver" apart from "guest-link state a staff action has since superseded" without ever
    // needing to persist the raw token value itself. Staff-initiated rotate/revoke never set this.
    public Guid? IssuedForOutboxItemId { get; private set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;

    internal void Revoke(DateTimeOffset now) => RevokedAt ??= now;

    internal void RecordUse(DateTimeOffset now) => LastUsedAt = now;
}
