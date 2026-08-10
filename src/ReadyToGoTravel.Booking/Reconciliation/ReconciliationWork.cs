using ReadyToGoTravel.Booking.Checkout;

namespace ReadyToGoTravel.Booking.Reconciliation;

public enum ReconciliationWorkStatus
{
    Pending,
    Processing,
    Completed,
}

public sealed class ReconciliationWork
{
    private ReconciliationWork()
    {
    }

    internal ReconciliationWork(
        Guid id,
        Guid componentBookingId,
        CheckoutProduct product,
        DateTimeOffset dueAt,
        string source,
        string correlationId,
        DateTimeOffset now)
    {
        Id = id;
        ComponentBookingId = componentBookingId;
        Product = product;
        Status = ReconciliationWorkStatus.Pending;
        DueAtUtc = dueAt.UtcDateTime;
        Source = source;
        CorrelationId = correlationId;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid ComponentBookingId { get; private set; }

    public CheckoutProduct Product { get; private set; }

    public ReconciliationWorkStatus Status { get; private set; }

    public DateTimeOffset? DueAt => DueAtUtc.HasValue
        ? new DateTimeOffset(DateTime.SpecifyKind(DueAtUtc.Value, DateTimeKind.Utc))
        : null;

    internal DateTime? DueAtUtc { get; private set; }

    public string? LeaseOwner { get; private set; }

    public DateTimeOffset? LeaseExpiresAt => LeaseExpiresAtUtc.HasValue
        ? new DateTimeOffset(DateTime.SpecifyKind(LeaseExpiresAtUtc.Value, DateTimeKind.Utc))
        : null;

    internal DateTime? LeaseExpiresAtUtc { get; private set; }

    public int Attempts { get; private set; }

    public int ConsecutiveFailures { get; private set; }

    public string Source { get; private set; } = string.Empty;

    public string CorrelationId { get; private set; } = string.Empty;

    public string? LastErrorCode { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    internal void BringForward(DateTimeOffset dueAt, string source, string correlationId, DateTimeOffset now)
    {
        if (Status == ReconciliationWorkStatus.Completed || DueAtUtc is null || dueAt.UtcDateTime < DueAtUtc)
        {
            DueAtUtc = dueAt.UtcDateTime;
        }

        Status = ReconciliationWorkStatus.Pending;
        Source = source;
        CorrelationId = correlationId;
        LeaseOwner = null;
        LeaseExpiresAtUtc = null;
        LastErrorCode = null;
        UpdatedAt = now;
    }

    internal void Reschedule(DateTimeOffset dueAt, DateTimeOffset now)
    {
        Status = ReconciliationWorkStatus.Pending;
        DueAtUtc = dueAt.UtcDateTime;
        LeaseOwner = null;
        LeaseExpiresAtUtc = null;
        LastErrorCode = null;
        ConsecutiveFailures = 0;
        UpdatedAt = now;
    }

    internal void Complete(DateTimeOffset now)
    {
        Status = ReconciliationWorkStatus.Completed;
        DueAtUtc = null;
        LeaseOwner = null;
        LeaseExpiresAtUtc = null;
        LastErrorCode = null;
        ConsecutiveFailures = 0;
        UpdatedAt = now;
    }

    internal void Retry(string errorCode, DateTimeOffset dueAt, DateTimeOffset now)
    {
        Status = ReconciliationWorkStatus.Pending;
        DueAtUtc = dueAt.UtcDateTime;
        LeaseOwner = null;
        LeaseExpiresAtUtc = null;
        LastErrorCode = errorCode;
        ConsecutiveFailures++;
        UpdatedAt = now;
    }
}

public enum ReconciliationAttemptOutcome
{
    Succeeded,
    IgnoredStale,
    Retrying,
    Failed,
}

public sealed class ReconciliationAttempt
{
    private ReconciliationAttempt()
    {
    }

    internal ReconciliationAttempt(
        Guid id,
        Guid workId,
        Guid componentBookingId,
        int attemptNumber,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        ReconciliationAttemptOutcome outcome,
        string? errorCode,
        string correlationId)
    {
        Id = id;
        WorkId = workId;
        ComponentBookingId = componentBookingId;
        AttemptNumber = attemptNumber;
        StartedAt = startedAt;
        CompletedAt = completedAt;
        Outcome = outcome;
        ErrorCode = errorCode;
        CorrelationId = correlationId;
    }

    public Guid Id { get; private set; }
    public Guid WorkId { get; private set; }
    public Guid ComponentBookingId { get; private set; }
    public int AttemptNumber { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset CompletedAt { get; private set; }
    public ReconciliationAttemptOutcome Outcome { get; private set; }
    public string? ErrorCode { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
}
