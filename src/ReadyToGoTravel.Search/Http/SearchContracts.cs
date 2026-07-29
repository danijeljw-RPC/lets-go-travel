using ReadyToGoTravel.Search.Capabilities;
using ReadyToGoTravel.Search.Contracts;
using ReadyToGoTravel.Search.Pricing;

namespace ReadyToGoTravel.Search.Http;

public sealed record HotelSearchHttpRequest(
    string Destination,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Adults,
    IReadOnlyList<int> ChildAges,
    int Rooms,
    string RequestedCurrency,
    string PointOfSale)
{
    public HotelSearchRequest ToDomain() => new(
        Destination,
        CheckIn,
        CheckOut,
        Adults,
        ChildAges,
        Rooms,
        RequestedCurrency,
        PointOfSale);
}

public sealed record FlightSearchLegHttpRequest(string Origin, string Destination, DateOnly DepartureDate);

public sealed record FlightSearchHttpRequest(
    IReadOnlyList<FlightSearchLegHttpRequest> Legs,
    int Adults,
    int Children,
    int Infants,
    string CabinClass,
    string RequestedCurrency,
    string PointOfSale)
{
    public SearchResult<FlightSearchRequest> ToDomain()
    {
        if (!Enum.TryParse<Contracts.CabinClass>(CabinClass, true, out var cabinClass))
        {
            return SearchResult.Failure<FlightSearchRequest>("invalid_cabin_class");
        }

        return SearchResult.Success(new FlightSearchRequest(
            Legs.Select(leg => new FlightSearchLeg(leg.Origin, leg.Destination, leg.DepartureDate)).ToArray(),
            Adults,
            Children,
            Infants,
            cabinClass,
            RequestedCurrency,
            PointOfSale));
    }
}

public sealed record CapabilityHttpResponse(
    string Provider,
    string Environment,
    string PointOfSale,
    string Product,
    string Operation,
    string? CarrierCode,
    bool Enabled,
    bool ProductionEnabled,
    string EvidenceStatus,
    string EvidenceNote)
{
    internal static CapabilityHttpResponse FromDomain(SearchCapability capability) => new(
        capability.Provider,
        EnumText(capability.Environment),
        capability.PointOfSale,
        EnumText(capability.Product),
        EnumText(capability.Operation),
        capability.CarrierCode,
        capability.Enabled,
        capability.ProductionEnabled,
        EnumText(capability.EvidenceStatus),
        capability.EvidenceNote);

    private static string EnumText<T>(T value) where T : struct, Enum
    {
        var text = value.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }
}

public sealed record PriceHttpResponse(
    decimal MinimumTotal,
    string ReturnedCurrency,
    string RequestedCurrency,
    string CurrencyProvenance,
    decimal BaseAmount,
    decimal IncludedTaxes,
    decimal IncludedFees)
{
    internal static PriceHttpResponse FromDomain(OfferPrice price) => new(
        price.MinimumTotal,
        price.ReturnedCurrency,
        price.RequestedCurrency,
        price.CurrencyProvenance == Pricing.CurrencyProvenance.SupplierReturned
            ? "supplierReturned"
            : "requestedPointOfSale",
        price.BaseAmount,
        price.IncludedTaxes,
        price.IncludedFees);
}

public sealed record HotelOfferHttpResponse(
    string OfferId,
    string PropertyName,
    string Destination,
    string RoomName,
    string RateName,
    bool Refundable,
    PriceHttpResponse Price,
    DateTimeOffset ExpiresAt,
    bool RequiresRevalidation);

public sealed record FlightSegmentHttpResponse(
    string Origin,
    string Destination,
    DateTimeOffset Departure,
    DateTimeOffset Arrival,
    string MarketingCarrier,
    string OperatingCarrier,
    string FlightNumber);

public sealed record FlightOfferHttpResponse(
    string OfferId,
    string MarketingCarrier,
    string CabinClass,
    int Stops,
    string BaggageSummary,
    IReadOnlyList<FlightSegmentHttpResponse> Segments,
    PriceHttpResponse Price,
    DateTimeOffset ExpiresAt,
    bool RequiresRevalidation);

public sealed record SearchHttpResponse<TOffer>(
    string SearchId,
    string Environment,
    DateTimeOffset SearchedAt,
    bool SandboxObservation,
    IReadOnlyList<TOffer> Offers);
