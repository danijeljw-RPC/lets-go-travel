using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Retention.Domain;
using ReadyToGoTravel.Retention.Persistence;

namespace ReadyToGoTravel.Retention.Application;

public sealed class LegalHoldService(RetentionDbContext database, TimeProvider timeProvider)
    : ILegalHoldGuard, IRetentionReceiptRecorder
{
    public async Task<LegalHold> OpenAsync(
        string matterReference,
        string reason,
        string authorizedOwnerSubject,
        DateTimeOffset reviewByUtc,
        IReadOnlyCollection<LegalHoldScopeRequest> scopeRequests,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var hold = LegalHold.Open(matterReference, reason, authorizedOwnerSubject, reviewByUtc, scopeRequests, now);
        database.LegalHolds.Add(hold);
        database.LegalHoldAuditEvents.Add(new LegalHoldAuditEvent(
            Guid.CreateVersion7(now),
            hold.Id,
            LegalHoldAuditEventType.HoldOpened,
            $"Opened matter '{matterReference}' with {hold.Scopes.Count} scope entries.",
            now,
            authorizedOwnerSubject));
        await database.SaveChangesAsync(cancellationToken);
        return hold;
    }

    /// <summary>Returns null when no hold exists with that ID. Idempotent when already released.</summary>
    public async Task<LegalHold?> ReleaseAsync(
        Guid legalHoldId,
        string releasedBySubject,
        string releaseReason,
        CancellationToken cancellationToken = default)
    {
        var hold = await database.LegalHolds.SingleOrDefaultAsync(value => value.Id == legalHoldId, cancellationToken);
        if (hold is null)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var wasActive = hold.IsActive;
        hold.Release(releasedBySubject, releaseReason, now);
        if (wasActive)
        {
            database.LegalHoldAuditEvents.Add(new LegalHoldAuditEvent(
                Guid.CreateVersion7(now),
                hold.Id,
                LegalHoldAuditEventType.HoldReleased,
                $"Released: {releaseReason}",
                now,
                releasedBySubject));
        }

        await database.SaveChangesAsync(cancellationToken);
        return hold;
    }

    public async Task<IReadOnlyList<LegalHold>> ListAsync(bool? active, CancellationToken cancellationToken = default)
    {
        var query = database.LegalHolds.AsNoTracking().Include(hold => hold.Scopes).AsQueryable();
        query = active switch
        {
            true => query.Where(hold => hold.ReleasedAtUtc == null),
            false => query.Where(hold => hold.ReleasedAtUtc != null),
            null => query,
        };

        // Ordering by DateTimeOffset in SQL is unsupported by the SQLite provider used in tests
        // (PostgreSQL handles it natively); the legal-hold list is small and staff-facing, so
        // ordering the already-materialized page in memory is the correct, portable choice here.
        var holds = await query.ToListAsync(cancellationToken);
        return [.. holds.OrderByDescending(hold => hold.CreatedAtUtc)];
    }

    public Task<LegalHold?> GetAsync(Guid legalHoldId, CancellationToken cancellationToken = default) =>
        database.LegalHolds.AsNoTracking().Include(hold => hold.Scopes)
            .SingleOrDefaultAsync(hold => hold.Id == legalHoldId, cancellationToken);

    public async Task<IReadOnlySet<Guid>> ExcludeHeldAsync(
        RetentionRecordClass recordClass,
        RetentionSubjectKind subjectKind,
        IReadOnlyCollection<Guid> candidateSubjectIds,
        CancellationToken cancellationToken = default)
    {
        if (candidateSubjectIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var scopes = ActiveMatchingScopes(recordClass, subjectKind, candidateSubjectIds);
        var matches = subjectKind switch
        {
            RetentionSubjectKind.Customer => await scopes.Select(scope => new { SubjectId = scope.CustomerId!.Value, scope.LegalHoldId }).Distinct().ToListAsync(cancellationToken),
            RetentionSubjectKind.ComponentBooking => await scopes.Select(scope => new { SubjectId = scope.ComponentBookingId!.Value, scope.LegalHoldId }).Distinct().ToListAsync(cancellationToken),
            RetentionSubjectKind.SupportTicket => await scopes.Select(scope => new { SubjectId = scope.SupportTicketId!.Value, scope.LegalHoldId }).Distinct().ToListAsync(cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(subjectKind), subjectKind, "Unsupported retention subject kind."),
        };
        if (matches.Count == 0)
        {
            return new HashSet<Guid>();
        }

        // Every suppression is audited, not only the rarer fine-grained IsHeldAsync recheck path:
        // this batch-level exclusion is what actually protects the overwhelming majority of held
        // items in practice (IsHeldAsync only re-fires for the narrow mid-batch race window), so
        // skipping the audit write here would silently under-report "held records are audited".
        var now = timeProvider.GetUtcNow();
        foreach (var match in matches)
        {
            database.LegalHoldAuditEvents.Add(new LegalHoldAuditEvent(
                Guid.CreateVersion7(now),
                match.LegalHoldId,
                LegalHoldAuditEventType.GuardCheckHeld,
                $"Guard check found a held {recordClass} record ({subjectKind}={match.SubjectId}).",
                now,
                null));
        }

        await database.SaveChangesAsync(cancellationToken);
        return matches.Select(match => match.SubjectId).ToHashSet();
    }

    public async Task<bool> IsHeldAsync(
        RetentionRecordClass recordClass,
        RetentionSubjectKind subjectKind,
        Guid subjectId,
        CancellationToken cancellationToken = default)
    {
        var scopes = ActiveMatchingScopes(recordClass, subjectKind, [subjectId]);
        var holdIds = await scopes.Select(scope => scope.LegalHoldId).Distinct().ToListAsync(cancellationToken);
        if (holdIds.Count == 0)
        {
            return false;
        }

        var now = timeProvider.GetUtcNow();
        foreach (var holdId in holdIds)
        {
            database.LegalHoldAuditEvents.Add(new LegalHoldAuditEvent(
                Guid.CreateVersion7(now),
                holdId,
                LegalHoldAuditEventType.GuardCheckHeld,
                $"Guard check found a held {recordClass} record ({subjectKind}={subjectId}).",
                now,
                null));
        }

        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task RecordAsync(
        RetentionRecordClass recordClass,
        int policyVersion,
        string action,
        int successCount,
        int failureCount,
        DateTimeOffset completedAtUtc,
        string? failureSummary,
        CancellationToken cancellationToken = default)
    {
        database.RetentionDeletionReceipts.Add(new RetentionDeletionReceipt(
            Guid.CreateVersion7(completedAtUtc),
            recordClass,
            policyVersion,
            action,
            successCount,
            failureCount,
            completedAtUtc,
            failureSummary));
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordOperationalFailureAsync(
        RetentionRecordClass recordClass,
        string scope,
        string reason,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var dedupeKey = RetentionOperationalCase.CreateDedupeKey(scope, recordClass.ToString(), reason);
        if (await database.RetentionOperationalCases.AnyAsync(value => value.DedupeKey == dedupeKey, cancellationToken))
        {
            return;
        }

        database.RetentionOperationalCases.Add(new RetentionOperationalCase(
            Guid.CreateVersion7(nowUtc), recordClass, scope, dedupeKey, reason, nowUtc));
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another worker inserted the same dedupe key first - the fast-path check above raced
            // and lost, which is the expected, harmless outcome under the unique index backstop.
        }
    }

    private IQueryable<LegalHoldScope> ActiveMatchingScopes(
        RetentionRecordClass recordClass,
        RetentionSubjectKind subjectKind,
        IReadOnlyCollection<Guid> candidateSubjectIds)
    {
        var activeHoldIds = database.LegalHolds.Where(hold => hold.ReleasedAtUtc == null).Select(hold => hold.Id);
        var scopes = database.LegalHoldScopes.AsNoTracking()
            .Where(scope => scope.RecordClass == recordClass && activeHoldIds.Contains(scope.LegalHoldId));
        return subjectKind switch
        {
            RetentionSubjectKind.Customer => scopes.Where(scope =>
                scope.CustomerId != null && candidateSubjectIds.Contains(scope.CustomerId.Value)),
            RetentionSubjectKind.ComponentBooking => scopes.Where(scope =>
                scope.ComponentBookingId != null && candidateSubjectIds.Contains(scope.ComponentBookingId.Value)),
            RetentionSubjectKind.SupportTicket => scopes.Where(scope =>
                scope.SupportTicketId != null && candidateSubjectIds.Contains(scope.SupportTicketId.Value)),
            _ => throw new ArgumentOutOfRangeException(nameof(subjectKind), subjectKind, "Unsupported retention subject kind."),
        };
    }
}
