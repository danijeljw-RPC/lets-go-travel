using System.Security.Cryptography;
using System.Text;

namespace ReadyToGoTravel.Retention.Domain;

/// <summary>
/// Idempotent, dedupe-keyed visibility record for a retention sweep failure, following the exact
/// pattern of ReadyToGoTravel.Booking.Reconciliation.OperationalCase. A unique index on
/// <see cref="DedupeKey"/> is the actual concurrency backstop - the existence check before insert
/// is only a fast path.
/// </summary>
public sealed class RetentionOperationalCase
{
    internal RetentionOperationalCase(
        Guid id,
        RetentionRecordClass recordClass,
        string scope,
        string dedupeKey,
        string reason,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        RecordClass = recordClass;
        Scope = scope;
        DedupeKey = dedupeKey;
        Reason = reason;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public RetentionRecordClass RecordClass { get; private set; }

    public string Scope { get; private set; } = string.Empty;

    public string DedupeKey { get; private set; } = string.Empty;

    public string Reason { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    internal static string CreateDedupeKey(string scope, params string[] values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        var material = string.Join('\u001f', values);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
        return $"retention:{scope}:{hash}";
    }
}
