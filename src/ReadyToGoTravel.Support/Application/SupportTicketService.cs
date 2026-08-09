using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Support.Domain;
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

        var message = ticket.Reply(authorType, authorSubject, body, timeProvider.GetUtcNow());
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

        ticket.Close(staffSubject, timeProvider.GetUtcNow());
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
}
