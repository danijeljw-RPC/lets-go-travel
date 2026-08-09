namespace ReadyToGoTravel.Support.Domain;

public sealed class SupportTicket
{
    private readonly List<SupportTicketMessage> messages = [];

    private SupportTicket()
    {
    }

    private SupportTicket(
        Guid id,
        string? customerSubject,
        string contactName,
        string contactEmail,
        SupportTicketCategory category,
        string? bookingReference,
        DateTimeOffset now)
    {
        Id = id;
        CustomerSubject = customerSubject;
        ContactName = contactName;
        ContactEmail = contactEmail;
        Category = category;
        BookingReference = bookingReference;
        Status = SupportTicketStatus.New;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public string? CustomerSubject { get; private set; }

    public string ContactName { get; private set; } = string.Empty;

    public string ContactEmail { get; private set; } = string.Empty;

    public SupportTicketCategory Category { get; private set; }

    public bool IsUrgent => SupportTicketCategoryUrgency.IsUrgent(Category);

    public string? BookingReference { get; private set; }

    public SupportTicketStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public IReadOnlyList<SupportTicketMessage> Messages => messages;

    internal static SupportTicket Create(
        string? customerSubject,
        string contactName,
        string contactEmail,
        SupportTicketCategory category,
        string? bookingReference,
        string initialMessageBody,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contactName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contactEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(initialMessageBody);

        var ticket = new SupportTicket(
            Guid.CreateVersion7(now),
            customerSubject,
            contactName,
            contactEmail,
            category,
            bookingReference,
            now);
        var authorType = customerSubject is null ? SupportAuthorType.Guest : SupportAuthorType.Customer;
        ticket.AppendMessage(authorType, customerSubject, initialMessageBody, now);
        ticket.Status = SupportTicketStatus.WaitingOnSupport;
        return ticket;
    }

    internal SupportTicketMessage Reply(SupportAuthorType authorType, string? authorSubject, string body, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        if (authorType is SupportAuthorType.System)
        {
            throw new InvalidOperationException("System messages cannot be added through Reply.");
        }

        if (Status == SupportTicketStatus.Closed && authorType == SupportAuthorType.Support)
        {
            throw new InvalidOperationException("A closed ticket cannot receive a support reply; the customer must reopen it first.");
        }

        var reopening = Status == SupportTicketStatus.Closed &&
            authorType is SupportAuthorType.Customer or SupportAuthorType.Guest;

        var message = AppendMessage(authorType, authorSubject, body, now);

        Status = authorType is SupportAuthorType.Customer or SupportAuthorType.Guest
            ? SupportTicketStatus.WaitingOnSupport
            : SupportTicketStatus.WaitingOnCustomer;

        if (reopening)
        {
            AppendMessage(SupportAuthorType.System, null, "Ticket reopened.", now);
        }

        Touch(now);
        return message;
    }

    internal void Close(string staffSubject, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(staffSubject);
        if (Status == SupportTicketStatus.Closed)
        {
            return;
        }

        Status = SupportTicketStatus.Closed;
        ClosedAt = now;
        AppendMessage(SupportAuthorType.System, staffSubject, "Ticket closed by support.", now);
        Touch(now);
    }

    private SupportTicketMessage AppendMessage(
        SupportAuthorType authorType,
        string? authorSubject,
        string body,
        DateTimeOffset now)
    {
        var message = new SupportTicketMessage(
            Guid.CreateVersion7(now),
            Id,
            messages.Count + 1,
            authorType,
            authorSubject,
            body,
            now);
        messages.Add(message);
        return message;
    }

    private void Touch(DateTimeOffset now) =>
        UpdatedAt = now > UpdatedAt ? now : UpdatedAt.AddTicks(1);
}
