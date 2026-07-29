namespace ReadyToGoTravel.Booking.Domain;

#pragma warning disable CA1000
public sealed record DomainResult<T>(T? Value, string? ErrorCode)
{
    public bool IsSuccess => ErrorCode is null;

    public static DomainResult<T> Success(T value) => new(value, null);

    public static DomainResult<T> Failure(string errorCode) => new(default, errorCode);
}
#pragma warning restore CA1000
