using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReadyToGoTravel.Retention;
using ReadyToGoTravel.Retention.Application;
using ReadyToGoTravel.Retention.Domain;
using ReadyToGoTravel.Support.Persistence;
using ReadyToGoTravel.Support.Storage;

namespace ReadyToGoTravel.Support.Retention;

internal sealed class SupportRetentionSweepProcessor(
    SupportDbContext database,
    IObjectStorage storage,
    ILegalHoldGuard legalHoldGuard,
    IRetentionReceiptRecorder receiptRecorder,
    TimeProvider timeProvider,
    IOptions<RetentionSweepOptions> options) : ISupportRetentionSweepProcessor
{
    /// <summary>
    /// Audit events have no other server-side predicate to narrow the candidate set (every row is
    /// a candidate once old enough), and DateTimeOffset ordering/comparison cannot be translated
    /// by the SQLite provider used in tests. This caps how many minimal-column rows one cycle
    /// pulls into memory to filter client-side, bounding memory/work per cycle; a large backlog is
    /// cleared over several cycles rather than in one, which is safe (eventually consistent, never
    /// incorrect) even though it is not strictly oldest-first within a single cycle. internal so
    /// tests can seed exactly this many rows rather than hardcoding the value.
    /// </summary>
    internal const int AuditScanCap = 2000;

    /// <summary>
    /// If every expired row in an AuditScanCap-sized window is held (or the window is entirely
    /// not-yet-expired rows with none held to blame), SweepSecurityAuditRecordsAsync widens the
    /// window up to this many rows before giving up for the cycle, so a permanently-held or
    /// permanently-failing prefix can never starve later, genuinely actionable rows out of ever
    /// being reached (github issue #16 from the Codex review of PR #12).
    /// </summary>
    private const int AuditScanHardCap = AuditScanCap * 8;

    public async Task<bool> ProcessCycleAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            return false;
        }

        var now = timeProvider.GetUtcNow();
        var didWork = false;
        didWork |= await SweepAttachmentsAsync(now, cancellationToken);
        didWork |= await SweepTicketsAsync(now, cancellationToken);
        didWork |= await SweepSecurityAuditRecordsAsync(now, cancellationToken);
        return didWork;
    }

    private async Task<bool> SweepAttachmentsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        const RetentionRecordClass recordClass = RetentionRecordClass.SupportAttachment;
        var policy = RetentionPolicyCatalog.Get(recordClass);

        var candidates = (await (
                from attachment in database.Attachments
                join ticket in database.Tickets on attachment.TicketId equals ticket.Id
                where ticket.ClosedAt != null
                select new { attachment.Id, attachment.TicketId, attachment.MessageId, attachment.StorageKey, attachment.SizeBytes, ticket.ClosedAt })
                .ToListAsync(cancellationToken))
            // Filtered via the policy catalog rather than a manually computed cutoff, so the
            // period-unit (days/years) is never at risk of being silently miscalculated here.
            .Where(value => RetentionPolicyCatalog.IsExpired(recordClass, value.ClosedAt!.Value, now))
            .OrderBy(value => value.ClosedAt)
            .Take(RetentionSweepConstants.BatchSize)
            .ToList();
        if (candidates.Count == 0)
        {
            return false;
        }

        var ticketIds = candidates.Select(value => value.TicketId).Distinct().ToList();
        var held = await legalHoldGuard.ExcludeHeldAsync(recordClass, RetentionSubjectKind.SupportTicket, ticketIds, cancellationToken);

        var successCount = 0;
        var failureCount = 0;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (held.Contains(candidate.TicketId))
            {
                continue;
            }

            try
            {
                if (await legalHoldGuard.IsHeldAsync(recordClass, RetentionSubjectKind.SupportTicket, candidate.TicketId, cancellationToken))
                {
                    continue;
                }

                // Storage delete first: it is idempotent (a repeat delete of an already-gone key
                // is a no-op success), so if the process crashes before the transaction below
                // commits, the next cycle safely retries the whole item. The row delete and both
                // quota-counter decrements are wrapped in one transaction because, unlike the
                // storage delete, deleting the attachment row makes this item permanently
                // non-candidate (it will never be selected again) - so those counter updates must
                // either both happen with the row delete or not at all. Ticket.Reopen can bring a
                // closed ticket's upload quota back into active use, and TicketAttachmentUsage/
                // MessageAttachmentUsage have no reconciliation job, so a crash between these
                // statements would otherwise leave the quota permanently overstated, wrongly
                // blocking future legitimate uploads to a reopened ticket forever.
                await storage.DeleteAsync(candidate.StorageKey, cancellationToken);
                await using (var transaction = await database.Database.BeginTransactionAsync(cancellationToken))
                {
                    var deleted = await database.Attachments.Where(value => value.Id == candidate.Id).ExecuteDeleteAsync(cancellationToken);
                    if (deleted > 0)
                    {
                        await database.TicketAttachmentUsage.Where(value => value.TicketId == candidate.TicketId)
                            .ExecuteUpdateAsync(setters => setters.SetProperty(
                                value => value.BytesUsed, value => value.BytesUsed - candidate.SizeBytes), cancellationToken);
                        await database.MessageAttachmentUsage.Where(value => value.MessageId == candidate.MessageId)
                            .ExecuteUpdateAsync(setters => setters.SetProperty(
                                value => value.FileCount, value => value.FileCount - 1), cancellationToken);
                    }

                    await transaction.CommitAsync(cancellationToken);
                    // Retention sweeps deliberately have no lease, so a concurrent replica may
                    // have already deleted this same attachment; only this replica's own actual
                    // row delete counts as its success, so the receipt does not overstate work.
                    if (deleted > 0)
                    {
                        successCount++;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                failureCount++;
                await receiptRecorder.RecordOperationalFailureAsync(recordClass, "support-attachment", "purge_failed", now, cancellationToken);
            }
        }

        await receiptRecorder.RecordAsync(recordClass, policy.PolicyVersion, "Delete", successCount, failureCount, now, failureCount > 0 ? "purge failures occurred" : null, cancellationToken);
        return successCount > 0;
    }

    private async Task<bool> SweepTicketsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var candidates = (await database.Tickets
                .Where(ticket => ticket.ClosedAt != null)
                .Select(ticket => new { ticket.Id, ticket.ClosedAt, ticket.BookingReference })
                .ToListAsync(cancellationToken))
            .Select(ticket => new
            {
                ticket.Id,
                ClosedAt = ticket.ClosedAt!.Value,
                RecordClass = string.IsNullOrWhiteSpace(ticket.BookingReference)
                    ? RetentionRecordClass.GeneralSupportTicket
                    : RetentionRecordClass.BookingRelatedSupportTicket,
            })
            .Where(ticket => RetentionPolicyCatalog.IsExpired(ticket.RecordClass, ticket.ClosedAt, now))
            .OrderBy(ticket => ticket.ClosedAt)
            .Take(RetentionSweepConstants.BatchSize)
            .ToList();
        if (candidates.Count == 0)
        {
            return false;
        }

        // DeleteTicketAggregateAsync also destroys every attachment and audit event under the
        // ticket, so a hold scoped to SupportAttachment or SecurityAuditRecord for this same
        // ticket ID (both record classes are subject-keyed by SupportTicketId, exactly like the
        // ticket's own class - see LegalHoldScope) must block the whole aggregate teardown just as
        // much as a hold on the ticket's own record class does. A child item's hold does not need
        // to have independently expired to be at risk: the aggregate delete removes it the moment
        // the *ticket* expires, regardless of the child's own age.
        var ticketIds = candidates.Select(value => value.Id).ToList();
        var generalHeld = await legalHoldGuard.ExcludeHeldAsync(RetentionRecordClass.GeneralSupportTicket, RetentionSubjectKind.SupportTicket, ticketIds, cancellationToken);
        var bookingRelatedHeld = await legalHoldGuard.ExcludeHeldAsync(RetentionRecordClass.BookingRelatedSupportTicket, RetentionSubjectKind.SupportTicket, ticketIds, cancellationToken);
        var attachmentHeld = await legalHoldGuard.ExcludeHeldAsync(RetentionRecordClass.SupportAttachment, RetentionSubjectKind.SupportTicket, ticketIds, cancellationToken);
        var auditHeld = await legalHoldGuard.ExcludeHeldAsync(RetentionRecordClass.SecurityAuditRecord, RetentionSubjectKind.SupportTicket, ticketIds, cancellationToken);

        var generalSuccess = 0;
        var generalFailure = 0;
        var bookingRelatedSuccess = 0;
        var bookingRelatedFailure = 0;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var held = candidate.RecordClass == RetentionRecordClass.GeneralSupportTicket ? generalHeld : bookingRelatedHeld;
            if (held.Contains(candidate.Id) || attachmentHeld.Contains(candidate.Id) || auditHeld.Contains(candidate.Id))
            {
                continue;
            }

            try
            {
                if (await legalHoldGuard.IsHeldAsync(candidate.RecordClass, RetentionSubjectKind.SupportTicket, candidate.Id, cancellationToken) ||
                    await legalHoldGuard.IsHeldAsync(RetentionRecordClass.SupportAttachment, RetentionSubjectKind.SupportTicket, candidate.Id, cancellationToken) ||
                    await legalHoldGuard.IsHeldAsync(RetentionRecordClass.SecurityAuditRecord, RetentionSubjectKind.SupportTicket, candidate.Id, cancellationToken))
                {
                    continue;
                }

                // Retention sweeps deliberately have no lease, so a concurrent replica may have
                // already deleted this same ticket; only count it as this replica's success if
                // its own final ticket-row delete actually changed a row.
                var deleted = await DeleteTicketAggregateAsync(candidate.Id, cancellationToken);
                if (deleted)
                {
                    if (candidate.RecordClass == RetentionRecordClass.GeneralSupportTicket)
                    {
                        generalSuccess++;
                    }
                    else
                    {
                        bookingRelatedSuccess++;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                if (candidate.RecordClass == RetentionRecordClass.GeneralSupportTicket)
                {
                    generalFailure++;
                }
                else
                {
                    bookingRelatedFailure++;
                }

                await receiptRecorder.RecordOperationalFailureAsync(candidate.RecordClass, "support-ticket", "delete_failed", now, cancellationToken);
            }
        }

        if (generalSuccess + generalFailure > 0)
        {
            var generalPolicy = RetentionPolicyCatalog.Get(RetentionRecordClass.GeneralSupportTicket);
            await receiptRecorder.RecordAsync(RetentionRecordClass.GeneralSupportTicket, generalPolicy.PolicyVersion, "Delete", generalSuccess, generalFailure, now, generalFailure > 0 ? "deletion failures occurred" : null, cancellationToken);
        }

        if (bookingRelatedSuccess + bookingRelatedFailure > 0)
        {
            var bookingRelatedPolicy = RetentionPolicyCatalog.Get(RetentionRecordClass.BookingRelatedSupportTicket);
            await receiptRecorder.RecordAsync(RetentionRecordClass.BookingRelatedSupportTicket, bookingRelatedPolicy.PolicyVersion, "Delete", bookingRelatedSuccess, bookingRelatedFailure, now, bookingRelatedFailure > 0 ? "deletion failures occurred" : null, cancellationToken);
        }

        return generalSuccess + bookingRelatedSuccess > 0;
    }

    /// <summary>
    /// Deletes a ticket and every row that transitively depends on it, in the order its foreign
    /// keys require (every Support FK is Restrict except attachment_scan_work's cascade from
    /// attachment - see SupportEntityConfigurations.cs).
    ///
    /// This runs in two phases, deliberately not one single transaction spanning both: phase 1
    /// deletes each attachment's storage object then its own row as one atomic pair per
    /// attachment (identical to SweepAttachmentsAsync's own idempotent delete-storage-then-delete-row
    /// order), so a failure partway through phase 1 can never strand a row whose storage object is
    /// already gone - an earlier version of this method deleted every attachment's storage object
    /// up front and only removed the rows inside the phase-2 transaction below, which meant a later
    /// failure in that same transaction (tokens, messages, anything) rolled the row deletions back
    /// while the storage objects stayed deleted, resurrecting attachment rows that pointed at
    /// nothing. Phase 2 is pure Postgres state with no external side effect, so it is safe to wrap
    /// in one all-or-nothing transaction: a rollback there just means nothing changed, and the next
    /// cycle retries cleanly (phase 1 will find zero remaining attachments and skip straight to
    /// phase 2, which is naturally idempotent since every statement in it is a conditional delete).
    /// </summary>
    /// <returns>Whether the ticket row itself was actually deleted by this call.</returns>
    private async Task<bool> DeleteTicketAggregateAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        var attachmentIds = await database.Attachments
            .Where(value => value.TicketId == ticketId)
            .Select(value => new { value.Id, value.StorageKey })
            .ToListAsync(cancellationToken);
        foreach (var attachment in attachmentIds)
        {
            await storage.DeleteAsync(attachment.StorageKey, cancellationToken);
            await database.Attachments.Where(value => value.Id == attachment.Id).ExecuteDeleteAsync(cancellationToken);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await database.TicketAttachmentUsage.Where(value => value.TicketId == ticketId).ExecuteDeleteAsync(cancellationToken);
        var messageIds = await database.TicketMessages.Where(value => value.TicketId == ticketId).Select(value => value.Id).ToListAsync(cancellationToken);
        await database.MessageAttachmentUsage.Where(value => messageIds.Contains(value.MessageId)).ExecuteDeleteAsync(cancellationToken);

        // A token's RotatedFromTokenId (Restrict) points to an earlier token in the same ticket's
        // rotation chain. Clearing every such self-reference first, in one bulk update, removes
        // the ordering constraint entirely so the whole set can then be deleted in one statement
        // rather than needing a per-row newest-first delete loop.
        await database.GuestAccessTokens.Where(value => value.TicketId == ticketId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.RotatedFromTokenId, (Guid?)null), cancellationToken);
        await database.GuestAccessTokens.Where(value => value.TicketId == ticketId).ExecuteDeleteAsync(cancellationToken);

        await database.SupportNotificationOutbox.Where(value => value.TicketId == ticketId).ExecuteDeleteAsync(cancellationToken);
        await database.AuditEvents.Where(value => value.TicketId == ticketId).ExecuteDeleteAsync(cancellationToken);
        await database.TicketMessages.Where(value => value.TicketId == ticketId).ExecuteDeleteAsync(cancellationToken);
        var deletedTickets = await database.Tickets.Where(value => value.Id == ticketId).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return deletedTickets > 0;
    }

    private async Task<bool> SweepSecurityAuditRecordsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        const RetentionRecordClass recordClass = RetentionRecordClass.SecurityAuditRecord;
        var policy = RetentionPolicyCatalog.Get(recordClass);

        List<(Guid Id, Guid? TicketId, DateTimeOffset CreatedAt)> candidates;
        IReadOnlySet<Guid> held;
        var scanSize = AuditScanCap;
        while (true)
        {
            var window = await database.AuditEvents
                .OrderBy(value => value.Id)
                .Take(scanSize)
                .Select(value => new { value.Id, value.TicketId, value.CreatedAt })
                .ToListAsync(cancellationToken);
            var expired = window
                .Where(value => RetentionPolicyCatalog.IsExpired(recordClass, value.CreatedAt, now))
                .Select(value => (value.Id, value.TicketId, value.CreatedAt))
                .ToList();
            if (expired.Count == 0)
            {
                return false;
            }

            var expiredTicketIds = expired.Where(value => value.TicketId.HasValue).Select(value => value.TicketId!.Value).Distinct().ToList();
            held = await legalHoldGuard.ExcludeHeldAsync(recordClass, RetentionSubjectKind.SupportTicket, expiredTicketIds, cancellationToken);
            var hasActionableRow = expired.Any(value => !value.TicketId.HasValue || !held.Contains(value.TicketId.Value));

            // If every expired row in this window is held, the fixed prefix would never shrink
            // and later, unheld, expired rows past it would never be reached. Widen the window
            // and look further before giving up, rather than repeating the exact same stuck
            // prefix every cycle forever. Bounded by AuditScanHardCap so one cycle's worst case
            // is still finite.
            if (hasActionableRow || window.Count < scanSize || scanSize >= AuditScanHardCap)
            {
                // Unheld rows sort before held ones (regardless of age) so a BatchSize-limited
                // selection out of a wide window always prioritises genuine progress over rows
                // that would just be skipped again - otherwise the oldest BatchSize rows could
                // themselves all be held even though unheld ones exist later in the window.
                candidates = [.. expired
                    .OrderBy(value => value.TicketId.HasValue && held.Contains(value.TicketId!.Value))
                    .ThenBy(value => value.CreatedAt)
                    .Take(RetentionSweepConstants.BatchSize)];
                break;
            }

            scanSize *= 2;
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        var successCount = 0;
        var failureCount = 0;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (candidate.TicketId.HasValue && held.Contains(candidate.TicketId.Value))
            {
                continue;
            }

            try
            {
                if (candidate.TicketId.HasValue &&
                    await legalHoldGuard.IsHeldAsync(recordClass, RetentionSubjectKind.SupportTicket, candidate.TicketId.Value, cancellationToken))
                {
                    continue;
                }

                // Retention sweeps deliberately have no lease, so a concurrent replica may have
                // already deleted this same audit event; only count a real change as a success.
                var deleted = await database.AuditEvents.Where(value => value.Id == candidate.Id).ExecuteDeleteAsync(cancellationToken);
                if (deleted > 0)
                {
                    successCount++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                failureCount++;
                await receiptRecorder.RecordOperationalFailureAsync(recordClass, "security-audit-record", "delete_failed", now, cancellationToken);
            }
        }

        await receiptRecorder.RecordAsync(recordClass, policy.PolicyVersion, "Delete", successCount, failureCount, now, failureCount > 0 ? "deletion failures occurred" : null, cancellationToken);
        return successCount > 0;
    }
}
