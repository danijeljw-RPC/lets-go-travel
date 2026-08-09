using ReadyToGoTravel.Booking.Checkout;

namespace ReadyToGoTravel.Booking.Reconciliation;

public enum RetrievedBookingStatus
{
    Pending,
    Confirmed,
    Cancelled,
    Failed,
    Completed,
}

public sealed record BookingFlightSegment(
    string Identity,
    string MarketingCarrier,
    string FlightNumber,
    string Origin,
    string Destination,
    DateTimeOffset ScheduledDeparture,
    DateTimeOffset ScheduledArrival);

public sealed record RetrievedHotelStay(
    string PropertyName,
    DateOnly CheckIn,
    DateOnly CheckOut,
    string Room,
    IReadOnlyList<string> Inclusions,
    string CancellationPolicy);

public sealed record RetrievedBookingState(
    CheckoutProduct Product,
    RetrievedBookingStatus Status,
    string? CustomerConfirmation,
    RetrievedHotelStay? Hotel,
    IReadOnlyList<BookingFlightSegment> FlightSegments,
    decimal? Total,
    string? Currency,
    DateTimeOffset? SupplierObservedAt);
