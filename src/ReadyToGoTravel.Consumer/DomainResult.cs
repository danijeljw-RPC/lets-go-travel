namespace ReadyToGoTravel.Consumer;

internal sealed record DomainResult<T>(T? Value, string? ErrorCode)
    where T : class
{
    public bool IsSuccess => ErrorCode is null;

    public static DomainResult<T> Success(T value) => new(value, null);

    public static DomainResult<T> Failure(string errorCode) => new(null, errorCode);
}
