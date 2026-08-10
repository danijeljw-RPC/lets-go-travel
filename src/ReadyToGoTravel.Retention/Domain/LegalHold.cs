namespace ReadyToGoTravel.Retention.Domain;

/// <summary>
/// A matter-specific suspension of ordinary destruction for records within its explicit scope
/// (docs/security/data-retention-and-legal-hold.md "Legal Hold"). A hold with no scope entries
/// cannot be constructed - there is no supported "any hold exists, block everything" shortcut.
/// </summary>
public sealed class LegalHold
{
    private readonly List<LegalHoldScope> scopes = [];

    private LegalHold(
        Guid id,
        string matterReference,
        string reason,
        string authorizedOwnerSubject,
        DateTimeOffset createdAtUtc,
        DateTimeOffset reviewByUtc)
    {
        Id = id;
        MatterReference = matterReference;
        Reason = reason;
        AuthorizedOwnerSubject = authorizedOwnerSubject;
        CreatedAtUtc = createdAtUtc;
        ReviewByUtc = reviewByUtc;
    }

    public Guid Id { get; private set; }

    public string MatterReference { get; private set; } = string.Empty;

    public string Reason { get; private set; } = string.Empty;

    public string AuthorizedOwnerSubject { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>
    /// Advisory next-review date. Per the approved baseline the legal/compliance owner reviews an
    /// active hold at least every 90 days - review does not itself expire or release the hold.
    /// </summary>
    public DateTimeOffset ReviewByUtc { get; private set; }

    public DateTimeOffset? ReleasedAtUtc { get; private set; }

    public string? ReleasedBySubject { get; private set; }

    public string? ReleaseReason { get; private set; }

    public IReadOnlyList<LegalHoldScope> Scopes => scopes;

    public bool IsActive => ReleasedAtUtc is null;

    public static LegalHold Open(
        string matterReference,
        string reason,
        string authorizedOwnerSubject,
        DateTimeOffset reviewByUtc,
        IReadOnlyCollection<LegalHoldScopeRequest> scopeRequests,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(matterReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizedOwnerSubject);
        ArgumentNullException.ThrowIfNull(scopeRequests);
        if (scopeRequests.Count == 0)
        {
            throw new ArgumentException("A legal hold must name at least one scope entry.", nameof(scopeRequests));
        }

        var id = Guid.CreateVersion7(now);
        var hold = new LegalHold(id, matterReference, reason, authorizedOwnerSubject, now, reviewByUtc);
        foreach (var request in scopeRequests)
        {
            hold.scopes.Add(LegalHoldScope.Create(
                id, request.RecordClass, request.CustomerId, request.ComponentBookingId, request.SupportTicketId, now));
        }

        return hold;
    }

    /// <summary>Idempotent: releasing an already-released hold does not change its release record.</summary>
    public void Release(string releasedBySubject, string releaseReason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(releasedBySubject);
        ArgumentException.ThrowIfNullOrWhiteSpace(releaseReason);
        if (ReleasedAtUtc is not null)
        {
            return;
        }

        ReleasedAtUtc = now;
        ReleasedBySubject = releasedBySubject;
        ReleaseReason = releaseReason;
    }

    public bool MatchesActive(RetentionRecordClass recordClass, RetentionSubjectKind subjectKind, Guid subjectId) =>
        IsActive && scopes.Any(scope => scope.Matches(recordClass, subjectKind, subjectId));
}
