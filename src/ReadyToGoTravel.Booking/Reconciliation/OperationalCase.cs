namespace ReadyToGoTravel.Booking.Reconciliation;

public sealed class OperationalCase
{
    private OperationalCase()
    {
    }

    internal OperationalCase(
        Guid id,
        Guid? componentBookingId,
        string dedupeKey,
        string category,
        string reason,
        DateTimeOffset createdAt)
    {
        Id = id;
        ComponentBookingId = componentBookingId;
        DedupeKey = dedupeKey;
        Category = category;
        Reason = reason;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid? ComponentBookingId { get; private set; }
    public string DedupeKey { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
}
