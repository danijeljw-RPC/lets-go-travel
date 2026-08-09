using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Support.Domain;
using ReadyToGoTravel.Support.Persistence;
using ReadyToGoTravel.Support.Storage;

namespace ReadyToGoTravel.Support.Application;

public abstract record SupportAttachmentUploadResult
{
    public sealed record Accepted(SupportAttachment Attachment) : SupportAttachmentUploadResult;

    public sealed record Rejected(string Reason) : SupportAttachmentUploadResult;
}

internal sealed class SupportAttachmentService(
    SupportDbContext database,
    IObjectStorage storage,
    TimeProvider timeProvider)
{
    public const long MaxFileBytes = 10 * 1024 * 1024;
    public const int MaxFilesPerMessage = 5;
    public const long MaxTicketBytes = 50 * 1024 * 1024;

    public async Task<SupportAttachmentUploadResult> UploadAsync(
        Guid ticketId,
        Guid messageId,
        string? uploaderCustomerSubject,
        Guid? uploaderGuestTokenId,
        string originalFileName,
        string declaredContentType,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var buffer = await ReadBoundedAsync(content, MaxFileBytes, cancellationToken);
        if (buffer is null)
        {
            return new SupportAttachmentUploadResult.Rejected("attachment_too_large");
        }

        var validation = AttachmentTypeValidator.Validate(declaredContentType, buffer);
        if (validation is not AttachmentTypeValidationResult.Accepted accepted)
        {
            var reason = validation is AttachmentTypeValidationResult.Rejected rejected
                ? rejected.Reason
                : "attachment_rejected";
            return new SupportAttachmentUploadResult.Rejected(reason);
        }

        var messageFileCount = await database.MessageAttachmentUsage
            .Where(value => value.MessageId == messageId)
            .Select(value => value.FileCount)
            .SingleOrDefaultAsync(cancellationToken);
        if (messageFileCount >= MaxFilesPerMessage)
        {
            return new SupportAttachmentUploadResult.Rejected("attachment_message_limit_exceeded");
        }

        var ticketBytesUsed = await database.TicketAttachmentUsage
            .Where(value => value.TicketId == ticketId)
            .Select(value => value.BytesUsed)
            .SingleOrDefaultAsync(cancellationToken);
        if (ticketBytesUsed + buffer.Length > MaxTicketBytes)
        {
            return new SupportAttachmentUploadResult.Rejected("attachment_ticket_limit_exceeded");
        }

        var now = timeProvider.GetUtcNow();
        var attachmentId = Guid.CreateVersion7(now);
        var storageKey = $"support/{ticketId:D}/{attachmentId:D}{accepted.Extension}";
        var checksum = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(buffer));

        try
        {
            await using var uploadStream = new MemoryStream(buffer, writable: false);
            await storage.PutAsync(storageKey, uploadStream, declaredContentType, cancellationToken);
        }
        catch (ObjectStorageUnavailableException)
        {
            return new SupportAttachmentUploadResult.Rejected("attachment_storage_unavailable");
        }

        // Everything from here on is wrapped in one try/catch: any failure after the object was
        // written to storage — including the usage-row lookups below, a failed claim,
        // SaveChangesAsync, or CommitAsync — must delete the now-orphaned object before
        // propagating. An ambient explicit transaction is rolled back by its own disposal since
        // it is never committed on this path; only the storage object needs explicit cleanup.
        try
        {
            // Ensuring the usage rows exist happens outside the transaction below: a caught
            // DbUpdateException here (another request created the row first) is a normal,
            // isolated failed statement. Inside an explicit transaction, Postgres would instead
            // abort the whole transaction on that error, and the claim right after it would fail too.
            await EnsureMessageUsageRowAsync(messageId, cancellationToken);
            await EnsureTicketUsageRowAsync(ticketId, cancellationToken);

            // The pre-checks above are a fast-path only; these atomic conditional UPDATEs are the
            // real correctness boundary, closing the TOCTOU window between the pre-check and the
            // insert under concurrent uploads to the same message or ticket.
            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

            if (!await ClaimMessageAttachmentSlotAsync(messageId, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                await TryDeleteOrphanedObjectAsync(storageKey, cancellationToken);
                return new SupportAttachmentUploadResult.Rejected("attachment_message_limit_exceeded");
            }

            if (!await ClaimTicketByteBudgetAsync(ticketId, buffer.Length, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                await TryDeleteOrphanedObjectAsync(storageKey, cancellationToken);
                return new SupportAttachmentUploadResult.Rejected("attachment_ticket_limit_exceeded");
            }

            var attachment = new SupportAttachment(
                attachmentId,
                ticketId,
                messageId,
                SanitizeFileName(originalFileName),
                declaredContentType,
                buffer.Length,
                storageKey,
                checksum,
                uploaderCustomerSubject,
                uploaderGuestTokenId,
                now);
            database.Attachments.Add(attachment);
            database.AttachmentScanWork.Add(new AttachmentScanWork(Guid.CreateVersion7(now), attachmentId, now));
            database.AuditEvents.Add(new SupportAuditEvent(
                Guid.CreateVersion7(now), ticketId, SupportAuditEventType.AttachmentUploaded, "Attachment uploaded.", now));
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new SupportAttachmentUploadResult.Accepted(attachment);
        }
        catch
        {
            // Best-effort: a cleanup failure must never replace or hide the original exception.
            await TryDeleteOrphanedObjectAsync(storageKey, cancellationToken);
            throw;
        }
    }

    private async Task TryDeleteOrphanedObjectAsync(string storageKey, CancellationToken cancellationToken)
    {
        try
        {
            await storage.DeleteAsync(storageKey, cancellationToken);
        }
        catch
        {
        }
    }

    private async Task<bool> ClaimMessageAttachmentSlotAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var claimed = await database.MessageAttachmentUsage
            .Where(value => value.MessageId == messageId && value.FileCount < MaxFilesPerMessage)
            .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.FileCount, value => value.FileCount + 1), cancellationToken);
        return claimed > 0;
    }

    private async Task<bool> ClaimTicketByteBudgetAsync(Guid ticketId, long bytes, CancellationToken cancellationToken)
    {
        var claimed = await database.TicketAttachmentUsage
            .Where(value => value.TicketId == ticketId && value.BytesUsed + bytes <= MaxTicketBytes)
            .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.BytesUsed, value => value.BytesUsed + bytes), cancellationToken);
        return claimed > 0;
    }

    // Production tickets always have a usage row created alongside the ticket/message (see
    // SupportTicketService); this lazily backfills one for any other caller (e.g. tests seeding
    // the domain directly) so the atomic claim above always has a row to condition on.
    private async Task EnsureMessageUsageRowAsync(Guid messageId, CancellationToken cancellationToken)
    {
        if (await database.MessageAttachmentUsage.AnyAsync(value => value.MessageId == messageId, cancellationToken))
        {
            return;
        }

        try
        {
            database.MessageAttachmentUsage.Add(new SupportMessageAttachmentUsage(messageId));
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            database.ChangeTracker.Clear();
        }
    }

    private async Task EnsureTicketUsageRowAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        if (await database.TicketAttachmentUsage.AnyAsync(value => value.TicketId == ticketId, cancellationToken))
        {
            return;
        }

        try
        {
            database.TicketAttachmentUsage.Add(new SupportTicketAttachmentUsage(ticketId));
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            database.ChangeTracker.Clear();
        }
    }

    public async Task<Uri?> CreateDownloadUrlAsync(Guid ticketId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var attachment = await database.Attachments.SingleOrDefaultAsync(value => value.Id == attachmentId, cancellationToken);
        if (attachment is null || attachment.TicketId != ticketId)
        {
            await AddAuditEventAsync(ticketId, SupportAuditEventType.AttachmentDownloadDenied, "Attachment not found for ticket.", now, cancellationToken);
            return null;
        }

        if (attachment.ScanStatus != AttachmentScanStatus.Clean)
        {
            await AddAuditEventAsync(ticketId, SupportAuditEventType.AttachmentDownloadDenied, "Attachment is not clean.", now, cancellationToken);
            return null;
        }

        await AddAuditEventAsync(ticketId, SupportAuditEventType.AttachmentDownloadAuthorized, "Attachment download authorized.", now, cancellationToken);
        return await storage.CreateDownloadUrlAsync(attachment.StorageKey, TimeSpan.FromMinutes(5), cancellationToken);
    }

    public Task<List<SupportAttachment>> ListForTicketAsync(Guid ticketId, CancellationToken cancellationToken = default) =>
        database.Attachments.Where(value => value.TicketId == ticketId).ToListAsync(cancellationToken);

    private async Task AddAuditEventAsync(
        Guid ticketId,
        SupportAuditEventType eventType,
        string detail,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        database.AuditEvents.Add(new SupportAuditEvent(Guid.CreateVersion7(now), ticketId, eventType, detail, now));
        await database.SaveChangesAsync(cancellationToken);
    }

    private static async Task<byte[]?> ReadBoundedAsync(Stream content, long maxBytes, CancellationToken cancellationToken)
    {
        await using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (output.Length + read > maxBytes)
            {
                return null;
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return output.ToArray();
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new System.Text.StringBuilder(name.Length);
        foreach (var character in name)
        {
            builder.Append(invalid.Contains(character) || char.IsControl(character) ? '_' : character);
        }

        var sanitized = builder.ToString();
        return sanitized.Length > 200 ? sanitized[..200] : sanitized;
    }
}
