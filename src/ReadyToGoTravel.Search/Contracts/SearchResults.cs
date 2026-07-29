using ReadyToGoTravel.Search.Capabilities;
using ReadyToGoTravel.Search.Pricing;

namespace ReadyToGoTravel.Search.Contracts;

public sealed record HotelSearchOffer(
    string OfferId,
    string PropertyName,
    string Destination,
    string RoomName,
    string RateName,
    bool Refundable,
    OfferPrice Price,
    DateTimeOffset ExpiresAt,
    bool RequiresRevalidation);

public sealed record FlightSegment(
    string Origin,
    string Destination,
    DateTimeOffset Departure,
    DateTimeOffset Arrival,
    string MarketingCarrier,
    string OperatingCarrier,
    string FlightNumber);

public sealed record FlightSearchOffer(
    string OfferId,
    string MarketingCarrier,
    string CabinClass,
    int Stops,
    string BaggageSummary,
    IReadOnlyList<FlightSegment> Segments,
    OfferPrice Price,
    DateTimeOffset ExpiresAt,
    bool RequiresRevalidation);

public sealed record SearchResponse<TOffer>(
    string SearchId,
    SearchEnvironment Environment,
    DateTimeOffset SearchedAt,
    bool SandboxObservation,
    IReadOnlyList<TOffer> Offers);
