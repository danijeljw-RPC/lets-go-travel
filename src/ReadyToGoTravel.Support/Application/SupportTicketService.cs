using System.Text.Json;
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

internal sealed record SupportTicketNotificationPayload(
    string ContactName,
    Guid TicketId,
    string Category,
    string? GuestToken);

internal sealed class SupportTicketService(SupportDbContext database, TimeProvider timeProvider)
{
    public async Task<SupportTicket> CreateTicketAsync(
        CreateSupportTicketCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
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

        string? guestToken = null;
        if (command.CustomerSubject is null)
        {
            var (rawToken, tokenHash) = GuestAccessTokenGenerator.Generate();
            guestToken = rawToken;
            database.GuestAccessTokens.Add(new SupportGuestAccessToken(
                Guid.CreateVersion7(now),
                ticket.Id,
                tokenHash,
                now,
                now.Add(SupportGuestAccessToken.TokenLifetime),
                null));
            database.AuditEvents.Add(new SupportAuditEvent(
                Guid.CreateVersion7(now), ticket.Id, SupportAuditEventType.GuestLinkIssued, "Guest link issued.", now));
        }

        EnqueueNotification(
            ticket,
            ticket.Messages[0].Id,
            "SupportTicketAcknowledgement",
            guestToken,
            now);

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
        var ticket = await database.Tickets
            .Include(value => value.Messages)
            .SingleOrDefaultAsync(value => value.Id == ticketId, cancellationToken)
            ?? throw new TicketNotFoundException(ticketId);

        var now = timeProvider.GetUtcNow();
        var message = ticket.Reply(authorType, authorSubject, body, now);
        EnqueueNotification(ticket, message.Id, "SupportTicketMessageAdded", null, now);
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

        var now = timeProvider.GetUtcNow();
        ticket.Close(staffSubject, now);
        EnqueueNotification(ticket, ticket.Messages[^1].Id, "SupportTicketClosed", null, now);
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

    private void EnqueueNotification(
        SupportTicket ticket,
        Guid messageId,
        string template,
        string? guestToken,
        DateTimeOffset now)
    {
        var payload = JsonSerializer.Serialize(new SupportTicketNotificationPayload(
            ticket.ContactName, ticket.Id, ticket.Category.ToString(), guestToken));
        database.SupportNotificationOutbox.Add(SupportNotificationOutboxItem.Create(
            ticket.Id, messageId, ticket.ContactEmail, template, payload, now));
    }
}
