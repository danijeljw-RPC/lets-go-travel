using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Support.Domain;
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

internal sealed class SupportTicketService(SupportDbContext database, TimeProvider timeProvider)
{
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

        var template = command.CustomerSubject is null
            ? SupportNotificationTemplates.AcknowledgementGuest
            : SupportNotificationTemplates.AcknowledgementCustomer;
        EnqueueNotification(ticket, ticket.Messages[0].Id, template, now);

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

        var ticket = await database.Tickets
            .Include(value => value.Messages)
            .SingleOrDefaultAsync(value => value.Id == ticketId, cancellationToken)
            ?? throw new TicketNotFoundException(ticketId);

        var now = timeProvider.GetUtcNow();
        var message = ticket.Reply(authorType, authorSubject, body, now);
        EnqueueNotification(ticket, message.Id, SupportNotificationTemplates.MessageAdded, now);
        await database.SaveChangesAsync(cancellationToken);
        return message;
    }

    public async Task CloseAsync(
        Guid ticketId,
        string staffSubject,
        CancellationToken cancellationToken = default)
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
        await database.SaveChangesAsync(cancellationToken);
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

    private void EnqueueNotification(SupportTicket ticket, Guid messageId, string template, DateTimeOffset now)
    {
        var payload = new SupportTicketNotificationPayload(ticket.ContactName, ticket.Id, ticket.Category.ToString());
        database.SupportNotificationOutbox.Add(SupportNotificationOutboxItem.Create(
            ticket.Id, messageId, ticket.ContactEmail, template, payload.ToJson(), now));
    }
}
