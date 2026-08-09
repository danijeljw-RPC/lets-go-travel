namespace ReadyToGoTravel.Support.Domain;

public sealed class SupportGuestAccessToken
{
    private SupportGuestAccessToken()
    {
    }

    internal SupportGuestAccessToken(
        Guid id,
        Guid ticketId,
        string tokenHash,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        Guid? rotatedFromTokenId)
    {
        Id = id;
        TicketId = ticketId;
        TokenHash = tokenHash;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        RotatedFromTokenId = rotatedFromTokenId;
    }

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset IssuedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public DateTimeOffset? LastUsedAt { get; private set; }

    public Guid? RotatedFromTokenId { get; private set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;

    internal void Revoke(DateTimeOffset now) => RevokedAt ??= now;

    internal void RecordUse(DateTimeOffset now) => LastUsedAt = now;
}
