namespace ReadyToGoTravel.Booking.Reconciliation;

using System.Security.Cryptography;
using System.Text;

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

    internal static string CreateDedupeKey(string scope, params string[] values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        var material = string.Join('\u001f', values);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
        return $"{scope}:{hash}";
    }
}
