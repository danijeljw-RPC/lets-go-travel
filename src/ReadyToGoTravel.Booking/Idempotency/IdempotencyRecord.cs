namespace ReadyToGoTravel.Booking.Idempotency;

internal enum IdempotencyRecordStatus
{
    InProgress,
    Completed,
}

internal sealed class IdempotencyRecord
{
    private IdempotencyRecord(
        Guid id,
        Guid customerId,
        string operation,
        string key,
        string fingerprint,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        CustomerId = customerId;
        Operation = operation;
        Key = key;
        Fingerprint = fingerprint;
        Status = IdempotencyRecordStatus.InProgress;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; }

    public Guid CustomerId { get; }

    public string Operation { get; }

    public string Key { get; }

    public string Fingerprint { get; }

    public IdempotencyRecordStatus Status { get; private set; }

    public int? ResponseStatusCode { get; private set; }

    public string? ResponseBody { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; }

    internal static IdempotencyRecord Begin(
        Guid customerId,
        string operation,
        string key,
        string fingerprint,
        DateTimeOffset now) => new(
        Guid.CreateVersion7(now),
        customerId,
        operation,
        key,
        fingerprint,
        now,
        now.AddDays(7));

    internal void Complete(int responseStatusCode, string responseBody, DateTimeOffset now)
    {
        Status = IdempotencyRecordStatus.Completed;
        ResponseStatusCode = responseStatusCode;
        ResponseBody = responseBody;
        UpdatedAt = now;
    }
}
