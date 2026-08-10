namespace ReadyToGoTravel.Support.Domain;

// Plain counters claimed via atomic conditional UPDATE in SupportAttachmentService, not through owned mutation methods.
public sealed class SupportTicketAttachmentUsage
{
    private SupportTicketAttachmentUsage()
    {
    }

    internal SupportTicketAttachmentUsage(Guid ticketId)
    {
        TicketId = ticketId;
        BytesUsed = 0;
    }

    public Guid TicketId { get; private set; }

    public long BytesUsed { get; private set; }
}

public sealed class SupportMessageAttachmentUsage
{
    private SupportMessageAttachmentUsage()
    {
    }

    internal SupportMessageAttachmentUsage(Guid messageId)
    {
        MessageId = messageId;
        FileCount = 0;
    }

    public Guid MessageId { get; private set; }

    public int FileCount { get; private set; }
}
