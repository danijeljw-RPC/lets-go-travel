using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReadyToGoTravel.Search.Capabilities;
using ReadyToGoTravel.Search.Contracts;
using ReadyToGoTravel.Search.Pricing;
using ReadyToGoTravel.Search.Providers;

namespace ReadyToGoTravel.Search.SupplierIntegrations.LiteApi;

public sealed class LiteApiFixtureSearchProvider(
    TimeProvider timeProvider,
    LiteApiFixtureIssuedOfferRegistry issuedOffers)
    : IHotelSearchProvider, IFlightSearchProvider
{
    private static readonly JsonSerializerOptions FixtureJsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public async Task<SearchResponse<HotelSearchOffer>> SearchAsync(
        HotelSearchRequest request,
        SearchEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        var searchedAt = timeProvider.GetUtcNow();
        if (environment != SearchEnvironment.Sandbox)
        {
            return Empty<HotelSearchOffer>(request.ToString(), environment, searchedAt);
        }

        var fixture = await LoadFixtureAsync<HotelFixture>("hotel-search.json", cancellationToken);
        if (!MatchesMarket(fixture.Environment, fixture.PointOfSale, fixture.Currency, request.PointOfSale, request.RequestedCurrency))
        {
            return Empty<HotelSearchOffer>(request.ToString(), environment, searchedAt);
        }

        var offers = fixture.Offers
            .Where(offer => string.Equals(offer.Destination, request.Destination, StringComparison.OrdinalIgnoreCase)
                && offer.CheckIn == request.CheckIn
                && offer.CheckOut == request.CheckOut
                && offer.Adults == request.Adults
                && offer.Children == request.ChildAges.Count
                && offer.Rooms == request.Rooms)
            .Select(offer => new HotelSearchOffer(
                issuedOffers.Issue(
                    fixture.Provider,
                    fixture.Environment,
                    offer.SupplierReference,
                    searchedAt,
                    searchedAt.AddMinutes(offer.LifetimeMinutes)),
                offer.PropertyName,
                offer.Destination,
                offer.RoomName,
                offer.RateName,
                offer.Refundable,
                MapPrice(
                    offer.MinimumTotal,
                    offer.BaseAmount,
                    offer.IncludedTaxes,
                    offer.IncludedFees,
                    fixture.Currency,
                    request.RequestedCurrency),
                searchedAt.AddMinutes(offer.LifetimeMinutes),
                true))
            .ToArray();

        return new SearchResponse<HotelSearchOffer>(
            OpaqueId("src_", request.ToString(), searchedAt.ToString("O", CultureInfo.InvariantCulture)),
            environment,
            searchedAt,
            true,
            offers);
    }

    public async Task<SearchResponse<FlightSearchOffer>> SearchAsync(
        FlightSearchRequest request,
        SearchEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        var searchedAt = timeProvider.GetUtcNow();
        if (environment != SearchEnvironment.Sandbox || request.Legs.Count != 1)
        {
            return Empty<FlightSearchOffer>(request.ToString(), environment, searchedAt);
        }

        var fixture = await LoadFixtureAsync<FlightFixture>("flight-search.json", cancellationToken);
        if (!MatchesMarket(fixture.Environment, fixture.PointOfSale, fixture.Currency, request.PointOfSale, request.RequestedCurrency))
        {
            return Empty<FlightSearchOffer>(request.ToString(), environment, searchedAt);
        }

        var leg = request.Legs[0];
        var offers = fixture.Offers
            .Where(offer => string.Equals(offer.Origin, leg.Origin, StringComparison.OrdinalIgnoreCase)
                && string.Equals(offer.Destination, leg.Destination, StringComparison.OrdinalIgnoreCase)
                && offer.DepartureDate == leg.DepartureDate
                && offer.Adults == request.Adults
                && offer.Children == request.Children
                && offer.Infants == request.Infants
                && string.Equals(offer.CabinClass, request.CabinClass.ToString(), StringComparison.OrdinalIgnoreCase))
            .Select(offer => new FlightSearchOffer(
                issuedOffers.Issue(
                    fixture.Provider,
                    fixture.Environment,
                    offer.SupplierReference,
                    searchedAt,
                    searchedAt.AddMinutes(offer.LifetimeMinutes)),
                offer.MarketingCarrier,
                offer.CabinClass,
                offer.Stops,
                offer.BaggageSummary,
                [new FlightSegment(
                    offer.Origin,
                    offer.Destination,
                    offer.Departure,
                    offer.Arrival,
                    offer.MarketingCarrier,
                    offer.OperatingCarrier,
                    offer.FlightNumber)],
                MapPrice(
                    offer.MinimumTotal,
                    offer.BaseAmount,
                    offer.IncludedTaxes,
                    offer.IncludedFees,
                    fixture.Currency,
                    request.RequestedCurrency),
                searchedAt.AddMinutes(offer.LifetimeMinutes),
                true))
            .ToArray();

        return new SearchResponse<FlightSearchOffer>(
            OpaqueId("src_", request.ToString(), searchedAt.ToString("O", CultureInfo.InvariantCulture)),
            environment,
            searchedAt,
            true,
            offers);
    }

    private static SearchResponse<TOffer> Empty<TOffer>(
        string? request,
        SearchEnvironment environment,
        DateTimeOffset searchedAt) =>
        new(
            OpaqueId("src_", request ?? string.Empty, searchedAt.ToString("O", CultureInfo.InvariantCulture)),
            environment,
            searchedAt,
            environment == SearchEnvironment.Sandbox,
            []);

    private static bool MatchesMarket(
        string fixtureEnvironment,
        string fixturePointOfSale,
        string fixtureCurrency,
        string requestedPointOfSale,
        string requestedCurrency) =>
        string.Equals(fixtureEnvironment, "sandbox", StringComparison.OrdinalIgnoreCase)
        && string.Equals(fixturePointOfSale, requestedPointOfSale, StringComparison.OrdinalIgnoreCase)
        && string.Equals(fixtureCurrency, requestedCurrency, StringComparison.OrdinalIgnoreCase);

    private static OfferPrice MapPrice(
        decimal minimumTotal,
        decimal baseAmount,
        decimal includedTaxes,
        decimal includedFees,
        string returnedCurrency,
        string requestedCurrency)
    {
        var result = OfferPrice.Create(
            minimumTotal,
            returnedCurrency,
            requestedCurrency,
            CurrencyProvenance.SupplierReturned,
            baseAmount,
            includedTaxes,
            includedFees);

        return result.Value ?? throw new InvalidDataException(
            $"Sanitized search fixture contains invalid pricing: {result.ErrorCode}.");
    }

    private static async Task<TFixture> LoadFixtureAsync<TFixture>(
        string filename,
        CancellationToken cancellationToken)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith(filename, StringComparison.Ordinal));
        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidDataException($"Embedded fixture {filename} was not found.");

        return await JsonSerializer.DeserializeAsync<TFixture>(stream, FixtureJsonOptions, cancellationToken)
            ?? throw new InvalidDataException($"Embedded fixture {filename} was empty.");
    }

    private static string OpaqueId(string prefix, params string[] values)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\u001f', values)));
        return prefix + Convert.ToHexString(digest.AsSpan(0, 12)).ToLowerInvariant();
    }

    private sealed record HotelFixture(
        string Provider,
        string Environment,
        string PointOfSale,
        string Currency,
        IReadOnlyList<HotelFixtureOffer> Offers);

    private sealed record HotelFixtureOffer(
        string SupplierReference,
        string Destination,
        string PropertyName,
        string RoomName,
        string RateName,
        bool Refundable,
        DateOnly CheckIn,
        DateOnly CheckOut,
        int Adults,
        int Children,
        int Rooms,
        decimal MinimumTotal,
        decimal BaseAmount,
        decimal IncludedTaxes,
        decimal IncludedFees,
        int LifetimeMinutes);

    private sealed record FlightFixture(
        string Provider,
        string Environment,
        string PointOfSale,
        string Currency,
        IReadOnlyList<FlightFixtureOffer> Offers);

    private sealed record FlightFixtureOffer(
        string SupplierReference,
        string Origin,
        string Destination,
        DateOnly DepartureDate,
        DateTimeOffset Departure,
        DateTimeOffset Arrival,
        string MarketingCarrier,
        string OperatingCarrier,
        string FlightNumber,
        string CabinClass,
        int Stops,
        string BaggageSummary,
        int Adults,
        int Children,
        int Infants,
        decimal MinimumTotal,
        decimal BaseAmount,
        decimal IncludedTaxes,
        decimal IncludedFees,
        int LifetimeMinutes);
}
