using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Support.Domain;
using ReadyToGoTravel.Support.Persistence;

namespace ReadyToGoTravel.Support.Guest;

public interface IGuestAccessTokenService
{
    Task<string> RotateAsync(Guid ticketId, string? actorSubject = null, CancellationToken cancellationToken = default);

    Task RevokeAsync(Guid ticketId, string? actorSubject = null, CancellationToken cancellationToken = default);

    Task<Guid?> ResolveAsync(string rawToken, CancellationToken cancellationToken = default);

    // Stages (without saving) a hash-only placeholder token created atomically with a brand-new
    // guest ticket, owned by the acknowledgement outbox item that will eventually deliver it. This
    // closes the race where staff could revoke/rotate guest access before any token exists: by the
    // time the ticket row is visible to staff, its guest-link lifecycle has already begun, so
    // RevokeAsync/RotateAsync always have a real row to act on. The caller must still call
    // SaveChangesAsync.
    void StageInitialToken(Guid ticketId, Guid outboxItemId, DateTimeOffset now);

    // Used exclusively by the notification outbox processor to mint the raw token it embeds in the
    // guest acknowledgement email. Unlike RotateAsync (staff-authoritative, always mints), this
    // only mints when the currently active token is the one this outbox item already owns (a first
    // attempt claiming its own staged placeholder, or a retry rotating its own prior mint) -
    // otherwise a staff revoke or rotate has superseded this notification and it must not touch
    // guest-link state.
    Task<GuestNotificationTokenResult> IssueForNotificationAsync(
        Guid ticketId, Guid outboxItemId, CancellationToken cancellationToken = default);
}

public readonly record struct GuestNotificationTokenResult(bool Superseded, string? RawToken)
{
    public static GuestNotificationTokenResult SupersededResult { get; } = new(true, null);

    public static GuestNotificationTokenResult Issued(string rawToken) => new(false, rawToken);
}

internal sealed class GuestAccessTokenService(SupportDbContext database, TimeProvider timeProvider)
    : IGuestAccessTokenService
{
    private const int MaxRotationAttempts = 5;

    // The unique partial index on (ticket_id) WHERE revoked_at IS NULL is the real correctness boundary for concurrent rotations; a losing SaveChangesAsync throws DbUpdateException and is retried against the now-current state.
    public async Task<string> RotateAsync(Guid ticketId, string? actorSubject = null, CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= MaxRotationAttempts; attempt++)
        {
            try
            {
                return await RotateOnceAsync(ticketId, actorSubject, cancellationToken);
            }
            catch (DbUpdateException) when (attempt < MaxRotationAttempts)
            {
                database.ChangeTracker.Clear();
            }
        }

        throw new InvalidOperationException(
            $"Unable to rotate the guest link for ticket {ticketId} after {MaxRotationAttempts} attempts due to concurrent rotations.");
    }

    private async Task<string> RotateOnceAsync(Guid ticketId, string? actorSubject, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var unrevoked = await FindUnrevokedTokenAsync(ticketId, cancellationToken);
        unrevoked?.Revoke(now);
        var rawToken = await IssueInternalAsync(ticketId, unrevoked?.Id, issuedForOutboxItemId: null, cancellationToken);
        await AddAuditEventAsync(ticketId, SupportAuditEventType.GuestLinkRotated, "Guest link rotated.", cancellationToken, actorSubject);
        await database.SaveChangesAsync(cancellationToken);
        return rawToken;
    }

    public void StageInitialToken(Guid ticketId, Guid outboxItemId, DateTimeOffset now)
    {
        var (_, tokenHash) = GuestAccessTokenGenerator.Generate();
        database.GuestAccessTokens.Add(new SupportGuestAccessToken(
            Guid.CreateVersion7(now),
            ticketId,
            tokenHash,
            now,
            now.Add(SupportGuestAccessToken.TokenLifetime),
            rotatedFromTokenId: null,
            issuedForOutboxItemId: outboxItemId));
        database.AuditEvents.Add(new SupportAuditEvent(
            Guid.CreateVersion7(now), ticketId, SupportAuditEventType.GuestLinkIssued, "Guest link issued.", now));
    }

    public async Task<GuestNotificationTokenResult> IssueForNotificationAsync(
        Guid ticketId, Guid outboxItemId, CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= MaxRotationAttempts; attempt++)
        {
            try
            {
                return await IssueForNotificationOnceAsync(ticketId, outboxItemId, cancellationToken);
            }
            catch (DbUpdateException) when (attempt < MaxRotationAttempts)
            {
                database.ChangeTracker.Clear();
            }
        }

        throw new InvalidOperationException(
            $"Unable to issue a guest link for ticket {ticketId} after {MaxRotationAttempts} attempts due to concurrent changes.");
    }

    private async Task<GuestNotificationTokenResult> IssueForNotificationOnceAsync(
        Guid ticketId, Guid outboxItemId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var current = await FindUnrevokedTokenAsync(ticketId, cancellationToken);

        // The active token is only ours to mint against if it was staged/minted for this exact
        // outbox item. A null owner (staff-issued), a different owner, or no active token at all
        // (staff revoked with nothing replacing it) all mean guest-link state has moved on without
        // us; the notification is stale and must not touch it.
        if (current is null || current.IssuedForOutboxItemId != outboxItemId)
        {
            return GuestNotificationTokenResult.SupersededResult;
        }

        current.Revoke(now);
        var rawToken = await IssueInternalAsync(ticketId, current.Id, outboxItemId, cancellationToken);
        await AddAuditEventAsync(ticketId, SupportAuditEventType.GuestLinkRotated, "Guest link rotated for notification delivery.", cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        return GuestNotificationTokenResult.Issued(rawToken);
    }

    public async Task RevokeAsync(Guid ticketId, string? actorSubject = null, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var unrevoked = await FindUnrevokedTokenAsync(ticketId, cancellationToken);
        if (unrevoked is null)
        {
            return;
        }

        unrevoked.Revoke(now);
        await AddAuditEventAsync(ticketId, SupportAuditEventType.GuestLinkRevoked, "Guest link revoked.", cancellationToken, actorSubject);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid?> ResolveAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var tokenHash = GuestAccessTokenGenerator.Hash(rawToken);
        var token = await database.GuestAccessTokens
            .SingleOrDefaultAsync(value => value.TokenHash == tokenHash, cancellationToken);
        if (token is null || !token.IsActive(now))
        {
            await AddAuditEventAsync(null, SupportAuditEventType.GuestLinkAuthenticationFailed, "Guest link authentication failed.", cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            return null;
        }

        token.RecordUse(now);
        await AddAuditEventAsync(token.TicketId, SupportAuditEventType.GuestLinkAuthenticated, "Guest link authenticated.", cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        return token.TicketId;
    }

    // Deliberately not filtered by expiry: the unique partial index that makes rotation safe
    // under concurrency (ix_support_guest_access_tokens_ticket_id_active) is keyed on
    // "revoked_at IS NULL" alone, so an expired-but-never-revoked row must be found and revoked
    // here too, or the next insert collides with it. Time-based validity for authentication is a
    // separate concern, checked only by SupportGuestAccessToken.IsActive in ResolveAsync.
    private Task<SupportGuestAccessToken?> FindUnrevokedTokenAsync(Guid ticketId, CancellationToken cancellationToken) =>
        database.GuestAccessTokens
            .SingleOrDefaultAsync(value => value.TicketId == ticketId && value.RevokedAt == null, cancellationToken);

    private async Task<string> IssueInternalAsync(
        Guid ticketId, Guid? rotatedFromTokenId, Guid? issuedForOutboxItemId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var (rawToken, tokenHash) = GuestAccessTokenGenerator.Generate();
        var token = new SupportGuestAccessToken(
            Guid.CreateVersion7(now),
            ticketId,
            tokenHash,
            now,
            now.Add(SupportGuestAccessToken.TokenLifetime),
            rotatedFromTokenId,
            issuedForOutboxItemId);
        database.GuestAccessTokens.Add(token);
        if (rotatedFromTokenId is null)
        {
            await AddAuditEventAsync(ticketId, SupportAuditEventType.GuestLinkIssued, "Guest link issued.", cancellationToken);
        }

        await database.SaveChangesAsync(cancellationToken);
        return rawToken;
    }

    private async Task AddAuditEventAsync(
        Guid? ticketId,
        SupportAuditEventType eventType,
        string detail,
        CancellationToken cancellationToken,
        string? actorSubject = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        database.AuditEvents.Add(new SupportAuditEvent(
            Guid.CreateVersion7(timeProvider.GetUtcNow()),
            ticketId,
            eventType,
            detail,
            timeProvider.GetUtcNow(),
            actorSubject));
        await Task.CompletedTask;
    }
}
