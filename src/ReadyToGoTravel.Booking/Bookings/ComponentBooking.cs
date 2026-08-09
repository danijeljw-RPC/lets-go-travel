using ReadyToGoTravel.Booking.Checkout;
using ReadyToGoTravel.Booking.Reconciliation;

namespace ReadyToGoTravel.Booking.Bookings;

public enum ComponentBookingStatus
{
    OfferSelected,
    PaymentPending,
    BookingPending,
    Confirmed,
    Failed,
    RefundRequired,
    RequiresSupport,
    Cancelled,
    Completed,
}

public enum BookingProviderOutcome
{
    Confirmed,
    Pending,
    Failed,
    Unknown,
}

public sealed record BookingProviderResult(
    BookingProviderOutcome Outcome,
    string? ExternalReference,
    string? ErrorCode)
{
    public static BookingProviderResult Confirmed(string externalReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalReference);
        return new BookingProviderResult(BookingProviderOutcome.Confirmed, externalReference, null);
    }

    public static BookingProviderResult Pending(string? externalReference = null) =>
        new(BookingProviderOutcome.Pending, externalReference, null);

    public static BookingProviderResult Failed(string errorCode) =>
        new(BookingProviderOutcome.Failed, null, errorCode);

    public static BookingProviderResult Unknown(string? externalReference = null) =>
        new(BookingProviderOutcome.Unknown, externalReference, null);
}

public sealed class ComponentBooking
{
    private ComponentBooking(
        Guid id,
        CheckoutProduct product,
        string offerId,
        string providerBinding,
        DateTimeOffset createdAt)
    {
        Id = id;
        Product = product;
        OfferId = offerId;
        ProviderBinding = providerBinding;
        Status = ComponentBookingStatus.OfferSelected;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; }

    public CheckoutProduct Product { get; }

    public string OfferId { get; }

    public string ProviderBinding { get; }

    public ComponentBookingStatus Status { get; private set; }

    public string? ProviderBookingReference { get; private set; }

    public string? FailureCode { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public string? CurrentCanonicalHash { get; private set; }

    public int CurrentVersionNumber { get; private set; }

    public DateTimeOffset? LastReconciledAt { get; private set; }

    public DateTimeOffset? NextDepartureAt { get; private set; }

    internal static ComponentBooking Create(CheckoutRevisionComponent component, DateTimeOffset now) => new(
        Guid.CreateVersion7(now),
        component.Product,
        component.OfferId,
        component.ProviderBinding,
        now);

    internal void MarkPaymentPending(DateTimeOffset now)
    {
        Status = ComponentBookingStatus.PaymentPending;
        Touch(now);
    }

    internal void BeginBooking(DateTimeOffset now)
    {
        Status = ComponentBookingStatus.BookingPending;
        Touch(now);
    }

    internal string? Record(BookingProviderResult result, DateTimeOffset now)
    {
        if (Status != ComponentBookingStatus.BookingPending)
        {
            return "component_not_ready_for_booking";
        }

        switch (result.Outcome)
        {
            case BookingProviderOutcome.Confirmed when !string.IsNullOrWhiteSpace(result.ExternalReference):
                Status = ComponentBookingStatus.Confirmed;
                ProviderBookingReference = result.ExternalReference;
                FailureCode = null;
                break;
            case BookingProviderOutcome.Confirmed:
                return "booking_confirmation_reference_required";
            case BookingProviderOutcome.Pending:
                ProviderBookingReference = result.ExternalReference;
                break;
            case BookingProviderOutcome.Failed:
                Status = ComponentBookingStatus.Failed;
                FailureCode = result.ErrorCode ?? "booking_failed";
                break;
            case BookingProviderOutcome.Unknown:
                Status = ComponentBookingStatus.RequiresSupport;
                ProviderBookingReference = result.ExternalReference;
                FailureCode = "booking_outcome_unknown";
                break;
            default:
                return "invalid_booking_provider_result";
        }

        Touch(now);
        return null;
    }

    internal void RequireRefund(DateTimeOffset now)
    {
        if (Status == ComponentBookingStatus.Failed)
        {
            Status = ComponentBookingStatus.RefundRequired;
            Touch(now);
        }
    }

    internal void RequireSupport(string reason, DateTimeOffset now)
    {
        if (Status != ComponentBookingStatus.Confirmed)
        {
            Status = ComponentBookingStatus.RequiresSupport;
            FailureCode = reason;
            Touch(now);
        }
    }

    internal bool ApplyReconciliationVersion(BookingVersion version, DateTimeOffset reconciledAt)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (version.ComponentBookingId != Id)
        {
            throw new InvalidOperationException("A booking version cannot be applied to another component.");
        }

        LastReconciledAt = reconciledAt.ToUniversalTime();
        if (string.Equals(CurrentCanonicalHash, version.CanonicalHash, StringComparison.Ordinal))
        {
            return false;
        }

        if (version.VersionNumber != CurrentVersionNumber + 1)
        {
            throw new InvalidOperationException("Booking versions must be applied in sequence.");
        }

        CurrentCanonicalHash = version.CanonicalHash;
        CurrentVersionNumber = version.VersionNumber;
        NextDepartureAt = version.NextDepartureAt;
        Touch(reconciledAt.ToUniversalTime());
        return true;
    }

    internal void ApplyRetrievedStatus(RetrievedBookingStatus status, DateTimeOffset now)
    {
        Status = status switch
        {
            RetrievedBookingStatus.Pending when Status is ComponentBookingStatus.Confirmed or ComponentBookingStatus.Completed => Status,
            RetrievedBookingStatus.Pending => ComponentBookingStatus.BookingPending,
            RetrievedBookingStatus.Confirmed => ComponentBookingStatus.Confirmed,
            RetrievedBookingStatus.Cancelled => ComponentBookingStatus.Cancelled,
            RetrievedBookingStatus.Failed => ComponentBookingStatus.Failed,
            RetrievedBookingStatus.Completed => ComponentBookingStatus.Completed,
            _ => throw new InvalidOperationException("Unsupported retrieved booking status."),
        };
        Touch(now);
    }

    private void Touch(DateTimeOffset now) =>
        UpdatedAt = now > UpdatedAt ? now : UpdatedAt.AddTicks(1);
}
