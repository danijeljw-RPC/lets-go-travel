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

    internal string? Record(PaymentProviderResult result, DateTimeOffset now)
    {
        if (!TryMapStatus(result.Outcome, out var nextStatus))
        {
            return "invalid_payment_provider_outcome";
        }

        if (!CanTransitionTo(nextStatus))
        {
            return "invalid_payment_transition";
        }

        Status = nextStatus;
        ProviderReturnReference = result.ProviderReturnReference;
        FailureCode = result.ErrorCode;
        UpdatedAt = now;
        return null;
    }

    internal void RequireRefund(DateTimeOffset now)
    {
        if (Status is PaymentStatus.Authorised or PaymentStatus.Captured)
        {
            Status = PaymentStatus.RefundRequired;
            UpdatedAt = now;
        }
    }

    private bool CanTransitionTo(PaymentStatus nextStatus) => Status switch
    {
        PaymentStatus.ActionRequired => nextStatus is
            PaymentStatus.Processing or
            PaymentStatus.Authorised or
            PaymentStatus.Captured or
            PaymentStatus.Failed or
            PaymentStatus.OutcomeUnknown,
        PaymentStatus.Processing => nextStatus is
            PaymentStatus.Authorised or
            PaymentStatus.Captured or
            PaymentStatus.Failed or
            PaymentStatus.OutcomeUnknown,
        PaymentStatus.Authorised => nextStatus is PaymentStatus.Captured or PaymentStatus.OutcomeUnknown,
        _ => false,
    };

    private static bool TryMapStatus(PaymentProviderOutcome outcome, out PaymentStatus status)
    {
        status = outcome switch
        {
            PaymentProviderOutcome.ActionRequired => PaymentStatus.ActionRequired,
            PaymentProviderOutcome.Processing => PaymentStatus.Processing,
            PaymentProviderOutcome.Authorised => PaymentStatus.Authorised,
            PaymentProviderOutcome.Captured => PaymentStatus.Captured,
            PaymentProviderOutcome.Failed => PaymentStatus.Failed,
            PaymentProviderOutcome.Unknown => PaymentStatus.OutcomeUnknown,
            _ => default,
        };

        return outcome is >= PaymentProviderOutcome.ActionRequired and <= PaymentProviderOutcome.Unknown;
    }
}
