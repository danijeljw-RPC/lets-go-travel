using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Support.Domain;
using ReadyToGoTravel.Support.Guest;
using ReadyToGoTravel.Support.Notifications;
using ReadyToGoTravel.Support.Persistence;

namespace ReadyToGoTravel.Support.Application;

public sealed record CreateSupportTicketCommand(
    string? CustomerSubject,
    string ContactName,
    string ContactEmail,
    SupportTicketCategory Category,
    string? BookingReference,
    string InitialMessageBody);

public sealed class TicketNotFoundException(Guid ticketId)
    : InvalidOperationException($"Support ticket {ticketId} was not found.");

public sealed class SupportMessageTooLongException()
    : InvalidOperationException($"Support message body exceeds {SupportTicketMessage.MaxBodyLength} characters.");

// Deliberately does not derive from InvalidOperationException: SupportTicket.Reply also throws
// that (for a closed ticket refusing a support reply), and callers must never confuse "lost every
// retry against genuine concurrent writers" with "the ticket is closed".
public sealed class SupportReplyConflictException(Guid ticketId)
    : Exception($"Unable to save a change to support ticket {ticketId} after repeated concurrent updates.");

internal sealed class SupportTicketService(
    SupportDbContext database, TimeProvider timeProvider, IGuestAccessTokenService guestTokens)
{
    // A customer/staff/guest reply and a close race on the same optimistic-concurrency token
    // (SupportTicket.UpdatedAt) and the same unique (TicketId, SequenceNumber) index. Both are
    // read-modify-write operations against the same row; the loser must reload the now-current
    // ticket and retry its own change rather than surface a raw 500, mirroring
    // IGuestAccessTokenService.RotateAsync's established retry-on-DbUpdateException pattern. A
    // small random backoff before each retry desynchronizes competing writers that would otherwise
    // tend to re-collide in lockstep under heavy concurrency.
    private const int MaxReplyAttempts = 8;
    public async Task<SupportTicket> CreateTicketAsync(
        CreateSupportTicketCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.InitialMessageBody.Length > SupportTicketMessage.MaxBodyLength)
        {
            throw new SupportMessageTooLongException();
        }

        var now = timeProvider.GetUtcNow();
        var ticket = SupportTicket.Create(
            command.CustomerSubject,
            command.ContactName,
            command.ContactEmail,
            command.Category,
            command.BookingReference,
            command.InitialMessageBody,
            now);
        database.Tickets.Add(ticket);
        database.TicketAttachmentUsage.Add(new SupportTicketAttachmentUsage(ticket.Id));
        database.MessageAttachmentUsage.Add(new SupportMessageAttachmentUsage(ticket.Messages[0].Id));

        var template = command.CustomerSubject is null
            ? SupportNotificationTemplates.AcknowledgementGuest
            : SupportNotificationTemplates.AcknowledgementCustomer;
        var notification = EnqueueNotification(ticket, ticket.Messages[0].Id, template, now);

        if (command.CustomerSubject is null)
        {
            // Mint the guest link's first (placeholder) generation in the same transaction as the
            // ticket itself, owned by the acknowledgement notification that will deliver it. See
            // IGuestAccessTokenService.StageInitialToken: this closes the race where staff could
            // revoke/rotate guest access before any token exists to act on.
            guestTokens.StageInitialToken(ticket.Id, notification.Id, now);
        }

        await database.SaveChangesAsync(cancellationToken);
        return ticket;
    }

    public async Task<SupportTicketMessage> AddMessageAsync(
        Guid ticketId,
        SupportAuthorType authorType,
        string? authorSubject,
        string body,
        CancellationToken cancellationToken = default)
    {
        if (body.Length > SupportTicketMessage.MaxBodyLength)
        {
            throw new SupportMessageTooLongException();
        }

        for (var attempt = 1; attempt <= MaxReplyAttempts; attempt++)
        {
            var ticket = await database.Tickets
                .Include(value => value.Messages)
                .SingleOrDefaultAsync(value => value.Id == ticketId, cancellationToken)
                ?? throw new TicketNotFoundException(ticketId);

            var now = timeProvider.GetUtcNow();
            var message = ticket.Reply(authorType, authorSubject, body, now);
            database.MessageAttachmentUsage.Add(new SupportMessageAttachmentUsage(message.Id));
            EnqueueNotification(ticket, message.Id, SupportNotificationTemplates.MessageAdded, now);
            try
            {
                await database.SaveChangesAsync(cancellationToken);
                return message;
            }
            catch (DbUpdateException) when (attempt < MaxReplyAttempts)
            {
                database.ChangeTracker.Clear();
                await Task.Delay(Random.Shared.Next(1, 15) * attempt, cancellationToken);
            }
        }

        throw new SupportReplyConflictException(ticketId);
    }

    public async Task CloseAsync(
        Guid ticketId,
        string staffSubject,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= MaxReplyAttempts; attempt++)
        {
            var ticket = await database.Tickets
                .Include(value => value.Messages)
                .SingleOrDefaultAsync(value => value.Id == ticketId, cancellationToken)
                ?? throw new TicketNotFoundException(ticketId);

            if (ticket.Status == SupportTicketStatus.Closed)
            {
                return;
            }

            var now = timeProvider.GetUtcNow();
            ticket.Close(staffSubject, now);
            EnqueueNotification(ticket, ticket.Messages[^1].Id, SupportNotificationTemplates.TicketClosed, now);
            try
            {
                await database.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException) when (attempt < MaxReplyAttempts)
            {
                database.ChangeTracker.Clear();
                await Task.Delay(Random.Shared.Next(1, 15) * attempt, cancellationToken);
            }
        }

        throw new SupportReplyConflictException(ticketId);
    }

    public Task<SupportTicket?> GetForCustomerAsync(
        string customerSubject,
        Guid ticketId,
        CancellationToken cancellationToken = default) =>
        database.Tickets
            .Include(value => value.Messages)
            .AsSplitQuery()
            .SingleOrDefaultAsync(
                value => value.Id == ticketId && value.CustomerSubject == customerSubject,
                cancellationToken);

    public async Task<List<SupportTicket>> GetForCustomerAsync(
        string customerSubject,
        CancellationToken cancellationToken = default)
    {
        var tickets = await database.Tickets
            .Where(value => value.CustomerSubject == customerSubject)
            .ToListAsync(cancellationToken);
        return [.. tickets.OrderByDescending(value => value.UpdatedAt)];
    }

    public Task<SupportTicket?> GetForStaffAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default) =>
        database.Tickets
            .Include(value => value.Messages)
            .AsSplitQuery()
            .SingleOrDefaultAsync(value => value.Id == ticketId, cancellationToken);

    public async Task<List<SupportTicket>> ListForStaffAsync(CancellationToken cancellationToken = default)
    {
        var tickets = await database.Tickets.ToListAsync(cancellationToken);
        return [.. tickets.OrderByDescending(value => value.UpdatedAt)];
    }

    private SupportNotificationOutboxItem EnqueueNotification(
        SupportTicket ticket, Guid messageId, string template, DateTimeOffset now)
    {
        var payload = new SupportTicketNotificationPayload(ticket.ContactName, ticket.Id, ticket.Category.ToString());
        var item = SupportNotificationOutboxItem.Create(
            ticket.Id, messageId, ticket.ContactEmail, template, payload.ToJson(), now);
        database.SupportNotificationOutbox.Add(item);
        return item;
    }
}
