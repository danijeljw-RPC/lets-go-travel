namespace ReadyToGoTravel.Booking.Payments;

public enum PaymentStatus
{
    NotStarted,
    ActionRequired,
    Processing,
    Authorised,
    Captured,
    Failed,
    OutcomeUnknown,
    RefundRequired,
}

public enum PaymentProviderOutcome
{
    ActionRequired,
    Processing,
    Authorised,
    Captured,
    Failed,
    Unknown,
}

public sealed record PaymentProviderResult(
    PaymentProviderOutcome Outcome,
    string? ProviderReturnReference,
    string? ErrorCode)
{
    public static PaymentProviderResult ActionRequired(string? providerReturnReference = null) =>
        new(PaymentProviderOutcome.ActionRequired, providerReturnReference, null);

    public static PaymentProviderResult Processing(string? providerReturnReference = null) =>
        new(PaymentProviderOutcome.Processing, providerReturnReference, null);

    public static PaymentProviderResult Authorised(string providerReturnReference) =>
        new(PaymentProviderOutcome.Authorised, providerReturnReference, null);

    public static PaymentProviderResult Captured(string providerReturnReference) =>
        new(PaymentProviderOutcome.Captured, providerReturnReference, null);

    public static PaymentProviderResult Failed(string errorCode) =>
        new(PaymentProviderOutcome.Failed, null, errorCode);

    public static PaymentProviderResult Unknown(string? providerReturnReference = null) =>
        new(PaymentProviderOutcome.Unknown, providerReturnReference, null);
}

public sealed class PaymentAttempt
{
    internal PaymentAttempt(
        Guid id,
        string provider,
        string providerPaymentReference,
        decimal amount,
        string currency,
        DateTimeOffset createdAt)
    {
        Id = id;
        Provider = provider;
        ProviderPaymentReference = providerPaymentReference;
        Amount = amount;
        Currency = currency;
        Status = PaymentStatus.ActionRequired;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; }

    public string Provider { get; }

    public string ProviderPaymentReference { get; }

    public decimal Amount { get; }

    public string Currency { get; }

    public PaymentStatus Status { get; private set; }

    public string? ProviderReturnReference { get; private set; }

    public string? FailureCode { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    internal void Record(PaymentProviderResult result, DateTimeOffset now)
    {
        Status = result.Outcome switch
        {
            PaymentProviderOutcome.ActionRequired => PaymentStatus.ActionRequired,
            PaymentProviderOutcome.Processing => PaymentStatus.Processing,
            PaymentProviderOutcome.Authorised => PaymentStatus.Authorised,
            PaymentProviderOutcome.Captured => PaymentStatus.Captured,
            PaymentProviderOutcome.Failed => PaymentStatus.Failed,
            PaymentProviderOutcome.Unknown => PaymentStatus.OutcomeUnknown,
            _ => throw new ArgumentOutOfRangeException(nameof(result)),
        };
        ProviderReturnReference = result.ProviderReturnReference;
        FailureCode = result.ErrorCode;
        UpdatedAt = now;
    }
}
