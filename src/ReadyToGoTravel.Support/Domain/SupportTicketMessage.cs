namespace ReadyToGoTravel.Support.Domain;

public sealed class SupportTicketMessage
{
    public const int MaxBodyLength = 4000;

    private SupportTicketMessage()
    {
    }

    internal SupportTicketMessage(
        Guid id,
        Guid ticketId,
        int sequenceNumber,
        SupportAuthorType authorType,
        string? authorSubject,
        string body,
        DateTimeOffset createdAt)
    {
        Id = id;
        TicketId = ticketId;
        SequenceNumber = sequenceNumber;
        AuthorType = authorType;
        AuthorSubject = authorSubject;
        Body = body;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public int SequenceNumber { get; private set; }

    public SupportAuthorType AuthorType { get; private set; }

    public string? AuthorSubject { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }
}
