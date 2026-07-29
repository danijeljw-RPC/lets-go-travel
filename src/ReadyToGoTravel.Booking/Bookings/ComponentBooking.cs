using ReadyToGoTravel.Booking.Checkout;

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

    internal static ComponentBooking Create(CheckoutRevisionComponent component, DateTimeOffset now) => new(
        Guid.CreateVersion7(now),
        component.Product,
        component.OfferId,
        component.ProviderBinding,
        now);

    internal void MarkPaymentPending(DateTimeOffset now)
    {
        Status = ComponentBookingStatus.PaymentPending;
        UpdatedAt = now;
    }

    internal void BeginBooking(DateTimeOffset now)
    {
        Status = ComponentBookingStatus.BookingPending;
        UpdatedAt = now;
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

        UpdatedAt = now;
        return null;
    }

    internal void RequireRefund(DateTimeOffset now)
    {
        if (Status == ComponentBookingStatus.Failed)
        {
            Status = ComponentBookingStatus.RefundRequired;
            UpdatedAt = now;
        }
    }
}
