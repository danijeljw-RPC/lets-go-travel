using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Support.Domain;
using ReadyToGoTravel.Support.Persistence;

namespace ReadyToGoTravel.Support.Guest;

public interface IGuestAccessTokenService
{
    Task<string> RotateAsync(Guid ticketId, CancellationToken cancellationToken = default);

    Task RevokeAsync(Guid ticketId, CancellationToken cancellationToken = default);

    Task<Guid?> ResolveAsync(string rawToken, CancellationToken cancellationToken = default);
}

internal sealed class GuestAccessTokenService(SupportDbContext database, TimeProvider timeProvider)
    : IGuestAccessTokenService
{
    private const int MaxRotationAttempts = 5;

    // The unique partial index on (ticket_id) WHERE revoked_at IS NULL is the real correctness boundary for concurrent rotations; a losing SaveChangesAsync throws DbUpdateException and is retried against the now-current state.
    public async Task<string> RotateAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= MaxRotationAttempts; attempt++)
        {
            try
            {
                return await RotateOnceAsync(ticketId, cancellationToken);
            }
            catch (DbUpdateException) when (attempt < MaxRotationAttempts)
            {
                database.ChangeTracker.Clear();
            }
        }

        throw new InvalidOperationException(
            $"Unable to rotate the guest link for ticket {ticketId} after {MaxRotationAttempts} attempts due to concurrent rotations.");
    }

    private async Task<string> RotateOnceAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var active = await FindActiveTokenAsync(ticketId, now, cancellationToken);
        active?.Revoke(now);
        var rawToken = await IssueInternalAsync(ticketId, active?.Id, cancellationToken);
        await AddAuditEventAsync(ticketId, SupportAuditEventType.GuestLinkRotated, "Guest link rotated.", cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        return rawToken;
    }

    public async Task RevokeAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var active = await FindActiveTokenAsync(ticketId, now, cancellationToken);
        if (active is null)
        {
            return;
        }

        active.Revoke(now);
        await AddAuditEventAsync(ticketId, SupportAuditEventType.GuestLinkRevoked, "Guest link revoked.", cancellationToken);
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

    private async Task<SupportGuestAccessToken?> FindActiveTokenAsync(
        Guid ticketId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var candidates = await database.GuestAccessTokens
            .Where(value => value.TicketId == ticketId && value.RevokedAt == null)
            .ToListAsync(cancellationToken);
        return candidates.SingleOrDefault(value => value.ExpiresAt > now);
    }

    private async Task<string> IssueInternalAsync(Guid ticketId, Guid? rotatedFromTokenId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var (rawToken, tokenHash) = GuestAccessTokenGenerator.Generate();
        var token = new SupportGuestAccessToken(
            Guid.CreateVersion7(now),
            ticketId,
            tokenHash,
            now,
            now.Add(SupportGuestAccessToken.TokenLifetime),
            rotatedFromTokenId);
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
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        database.AuditEvents.Add(new SupportAuditEvent(
            Guid.CreateVersion7(timeProvider.GetUtcNow()),
            ticketId,
            eventType,
            detail,
            timeProvider.GetUtcNow()));
        await Task.CompletedTask;
    }
}
