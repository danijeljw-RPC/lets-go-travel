namespace ReadyToGoTravel.Booking.Webhooks;

public enum WebhookInboxStatus
{
    Pending,
    Processing,
    Completed,
    Retrying,
    Quarantined,
}

public sealed class WebhookInboxItem
{
    private WebhookInboxItem()
    {
    }

    internal WebhookInboxItem(
        Guid id,
        string environment,
        string eventId,
        string eventName,
        string rawBody,
        string payloadHash,
        bool sandbox,
        string correlationId,
        DateTimeOffset receivedAt)
    {
        Id = id;
        Provider = "LiteAPI";
        Environment = environment;
        EventId = eventId;
        EventName = eventName;
        RawBody = rawBody;
        PayloadHash = payloadHash;
        Sandbox = sandbox;
        CorrelationId = correlationId;
        Status = WebhookInboxStatus.Pending;
        ReceivedAt = receivedAt;
        NextAttemptAtUtc = receivedAt.UtcDateTime;
    }

    public Guid Id { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string Environment { get; private set; } = string.Empty;
    public string EventId { get; private set; } = string.Empty;
    public string EventName { get; private set; } = string.Empty;
    public string RawBody { get; private set; } = string.Empty;
    public string PayloadHash { get; private set; } = string.Empty;
    public bool Sandbox { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public WebhookInboxStatus Status { get; private set; }
    public int Attempts { get; private set; }
    internal DateTime? NextAttemptAtUtc { get; private set; }
    public string? LeaseOwner { get; private set; }
    internal DateTime? LeaseExpiresAtUtc { get; private set; }
    public string? ErrorCode { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>
    /// The component booking this event was successfully correlated to, set synchronously by
    /// WebhookInboxProcessor.ProcessNextAsync before the item ever reaches Completed - never set
    /// for an unsupported event, an unparseable payload, or a booking reference that cannot be
    /// resolved, since those genuinely have no booking to correlate to. Retention's legal-hold
    /// guard (SweepWebhookPayloadBodiesAsync) relies on this ordering: a null value on a
    /// Completed row is only ever a real "nothing to protect" case, never a lost link, which is
    /// why the sweep safely skips the hold check for it (see WebhookInboxProcessorTests'
    /// KnownEventEnqueuesRetrievalWithoutMutatingBookingFromPayload and
    /// BookingRetentionSweepTests' UncorrelatedCompletedWebhookBodyIsSweptSafelyWithNoPossibleHoldToBypass,
    /// which both pin this down directly - see also github issue #15 from the Codex review of
    /// PR #12, and this codebase has never been deployed, so no pre-existing row can predate this
    /// column's introduction in the Slice 7 migration).
    /// </summary>
    public Guid? ComponentBookingId { get; private set; }

    internal void LinkToComponentBooking(Guid componentBookingId) => ComponentBookingId = componentBookingId;

    internal void Quarantine(string errorCode, DateTimeOffset now)
    {
        Status = WebhookInboxStatus.Quarantined;
        ErrorCode = errorCode;
        NextAttemptAtUtc = null;
        LeaseOwner = null;
        LeaseExpiresAtUtc = null;
        CompletedAt = now;
    }

    internal void Complete(DateTimeOffset now)
    {
        Status = WebhookInboxStatus.Completed;
        ErrorCode = null;
        NextAttemptAtUtc = null;
        LeaseOwner = null;
        LeaseExpiresAtUtc = null;
        CompletedAt = now;
    }

    internal void Retry(string errorCode, DateTimeOffset nextAttemptAt)
    {
        Status = WebhookInboxStatus.Retrying;
        ErrorCode = errorCode;
        NextAttemptAtUtc = nextAttemptAt.UtcDateTime;
        LeaseOwner = null;
        LeaseExpiresAtUtc = null;
    }

    internal void Requeue(DateTimeOffset now)
    {
        Status = WebhookInboxStatus.Pending;
        Attempts = 0;
        ErrorCode = null;
        NextAttemptAtUtc = now.UtcDateTime;
        LeaseOwner = null;
        LeaseExpiresAtUtc = null;
        CompletedAt = null;
    }
}
