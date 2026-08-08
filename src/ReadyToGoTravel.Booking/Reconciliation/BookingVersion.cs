namespace ReadyToGoTravel.Booking.Reconciliation;

public sealed class BookingVersion
{
    private BookingVersion()
    {
    }

    internal BookingVersion(
        Guid id,
        Guid componentBookingId,
        int versionNumber,
        DateTimeOffset observedAt,
        DateTimeOffset? effectiveAt,
        string source,
        string canonicalisationVersion,
        string canonicalSnapshotJson,
        string canonicalHash,
        string metadataJson,
        string flagsJson,
        string diffJson,
        string severity,
        string correlationId,
        DateTimeOffset? nextDepartureAt)
    {
        Id = id;
        ComponentBookingId = componentBookingId;
        VersionNumber = versionNumber;
        ObservedAt = observedAt;
        EffectiveAt = effectiveAt;
        Source = source;
        CanonicalisationVersion = canonicalisationVersion;
        CanonicalSnapshotJson = canonicalSnapshotJson;
        CanonicalHash = canonicalHash;
        MetadataJson = metadataJson;
        FlagsJson = flagsJson;
        DiffJson = diffJson;
        Severity = severity;
        CorrelationId = correlationId;
        NextDepartureAt = nextDepartureAt;
    }

    public Guid Id { get; private set; }

    public Guid ComponentBookingId { get; private set; }

    public int VersionNumber { get; private set; }

    public DateTimeOffset ObservedAt { get; private set; }

    public DateTimeOffset? EffectiveAt { get; private set; }

    public string Source { get; private set; } = string.Empty;

    public string CanonicalisationVersion { get; private set; } = string.Empty;

    public string CanonicalSnapshotJson { get; private set; } = string.Empty;

    public string CanonicalHash { get; private set; } = string.Empty;

    public string MetadataJson { get; private set; } = string.Empty;

    public string FlagsJson { get; private set; } = string.Empty;

    public string DiffJson { get; private set; } = string.Empty;

    public string Severity { get; private set; } = string.Empty;

    public string CorrelationId { get; private set; } = string.Empty;

    public DateTimeOffset? NextDepartureAt { get; private set; }

    internal void SetSeverity(string severity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(severity);
        Severity = severity;
    }
}
