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
    /// incorrect) even though it is not strictly oldest-first within a single cycle.
    /// </summary>
    private const int AuditScanCap = 2000;

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
                // is a no-op success), so if the process crashes or the DB delete below fails, the
                // next cycle safely retries both steps without ever leaving an orphaned object with
                // a still-existing row pointing nowhere, or a row surviving with no object behind it.
                await storage.DeleteAsync(candidate.StorageKey, cancellationToken);
                await database.Attachments.Where(value => value.Id == candidate.Id).ExecuteDeleteAsync(cancellationToken);
                await database.TicketAttachmentUsage.Where(value => value.TicketId == candidate.TicketId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(
                        value => value.BytesUsed, value => value.BytesUsed - candidate.SizeBytes), cancellationToken);
                await database.MessageAttachmentUsage.Where(value => value.MessageId == candidate.MessageId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(
                        value => value.FileCount, value => value.FileCount - 1), cancellationToken);
                successCount++;
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

        var ticketIds = candidates.Select(value => value.Id).ToList();
        var generalHeld = await legalHoldGuard.ExcludeHeldAsync(RetentionRecordClass.GeneralSupportTicket, RetentionSubjectKind.SupportTicket, ticketIds, cancellationToken);
        var bookingRelatedHeld = await legalHoldGuard.ExcludeHeldAsync(RetentionRecordClass.BookingRelatedSupportTicket, RetentionSubjectKind.SupportTicket, ticketIds, cancellationToken);

        var generalSuccess = 0;
        var generalFailure = 0;
        var bookingRelatedSuccess = 0;
        var bookingRelatedFailure = 0;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var held = candidate.RecordClass == RetentionRecordClass.GeneralSupportTicket ? generalHeld : bookingRelatedHeld;
            if (held.Contains(candidate.Id))
            {
                continue;
            }

            try
            {
                if (await legalHoldGuard.IsHeldAsync(candidate.RecordClass, RetentionSubjectKind.SupportTicket, candidate.Id, cancellationToken))
                {
                    continue;
                }

                await DeleteTicketAggregateAsync(candidate.Id, cancellationToken);
                if (candidate.RecordClass == RetentionRecordClass.GeneralSupportTicket)
                {
                    generalSuccess++;
                }
                else
                {
                    bookingRelatedSuccess++;
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
    private async Task DeleteTicketAggregateAsync(Guid ticketId, CancellationToken cancellationToken)
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
        await database.Tickets.Where(value => value.Id == ticketId).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<bool> SweepSecurityAuditRecordsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        const RetentionRecordClass recordClass = RetentionRecordClass.SecurityAuditRecord;
        var policy = RetentionPolicyCatalog.Get(recordClass);

        var candidates = (await database.AuditEvents
                .OrderBy(value => value.Id)
                .Take(AuditScanCap)
                .Select(value => new { value.Id, value.TicketId, value.CreatedAt })
                .ToListAsync(cancellationToken))
            .Where(value => RetentionPolicyCatalog.IsExpired(recordClass, value.CreatedAt, now))
            .OrderBy(value => value.CreatedAt)
            .Take(RetentionSweepConstants.BatchSize)
            .ToList();
        if (candidates.Count == 0)
        {
            return false;
        }

        var ticketIds = candidates.Where(value => value.TicketId.HasValue).Select(value => value.TicketId!.Value).Distinct().ToList();
        var held = await legalHoldGuard.ExcludeHeldAsync(recordClass, RetentionSubjectKind.SupportTicket, ticketIds, cancellationToken);

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

                await database.AuditEvents.Where(value => value.Id == candidate.Id).ExecuteDeleteAsync(cancellationToken);
                successCount++;
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
