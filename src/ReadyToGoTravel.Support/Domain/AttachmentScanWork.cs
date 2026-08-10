namespace ReadyToGoTravel.Support.Domain;

public enum AttachmentScanWorkStatus
{
    Pending,
    Processing,
    Completed,
    Retrying,
    Failed,
}

public sealed class AttachmentScanWork
{
    private AttachmentScanWork()
    {
    }

    internal AttachmentScanWork(Guid id, Guid attachmentId, DateTimeOffset createdAt)
    {
        Id = id;
        AttachmentId = attachmentId;
        Status = AttachmentScanWorkStatus.Pending;
        NextAttemptAtUtc = createdAt.UtcDateTime;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid AttachmentId { get; private set; }

    public AttachmentScanWorkStatus Status { get; private set; }

    public int Attempts { get; private set; }

    internal DateTime NextAttemptAtUtc { get; private set; }

    public string? LeaseOwner { get; private set; }

    internal DateTime? LeaseExpiresAtUtc { get; private set; }

    public string? ErrorCode { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    internal void Complete(DateTimeOffset now)
    {
        Status = AttachmentScanWorkStatus.Completed;
        ErrorCode = null;
        LeaseOwner = null;
        LeaseExpiresAtUtc = null;
        CompletedAt = now;
    }

    internal void Fail(string errorCode, DateTimeOffset now)
    {
        Status = AttachmentScanWorkStatus.Failed;
        ErrorCode = errorCode;
        LeaseOwner = null;
        LeaseExpiresAtUtc = null;
        CompletedAt = now;
    }

    internal void Retry(string errorCode, DateTimeOffset nextAttemptAt)
    {
        Status = AttachmentScanWorkStatus.Retrying;
        ErrorCode = errorCode;
        NextAttemptAtUtc = nextAttemptAt.UtcDateTime;
        LeaseOwner = null;
        LeaseExpiresAtUtc = null;
    }

    internal void Requeue(DateTimeOffset now)
    {
        Status = AttachmentScanWorkStatus.Pending;
        Attempts = 0;
        ErrorCode = null;
        NextAttemptAtUtc = now.UtcDateTime;
        LeaseOwner = null;
        LeaseExpiresAtUtc = null;
        CompletedAt = null;
    }
}
