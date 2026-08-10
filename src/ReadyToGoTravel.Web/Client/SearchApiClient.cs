using System.Net.Http.Json;

namespace ReadyToGoTravel.Web.Client;

public sealed class SearchApiClient(HttpClient client)
{
    public async Task<IReadOnlyList<SearchCapabilitySummary>> GetCapabilitiesAsync(
        string pointOfSale = "AU",
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await client.GetFromJsonAsync<SearchCapabilitySummary[]>(
                $"/api/v1/search/capabilities?pointOfSale={Uri.EscapeDataString(pointOfSale)}",
                cancellationToken) ?? [];
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return [];
        }
    }

    public Task<SearchClientResult<HotelOfferSummary>> SearchHotelsAsync(
        HotelSearchInput request,
        CancellationToken cancellationToken = default) =>
        SendAsync<HotelSearchInput, HotelOfferSummary>(
            "/api/v1/search/hotels",
            request,
            cancellationToken);

    public Task<SearchClientResult<FlightOfferSummary>> SearchFlightsAsync(
        FlightSearchInput request,
        CancellationToken cancellationToken = default) =>
        SendAsync<FlightSearchInput, FlightOfferSummary>(
            "/api/v1/search/flights",
            request,
            cancellationToken);

    private async Task<SearchClientResult<TOffer>> SendAsync<TRequest, TOffer>(
        string path,
        TRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.PostAsJsonAsync(path, request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var problem = await response.Content.ReadFromJsonAsync<ApiProblem>(cancellationToken);
                return new SearchClientResult<TOffer>(false, problem?.Code ?? "search_unavailable", false, []);
            }

            var payload = await response.Content.ReadFromJsonAsync<SearchEnvelope<TOffer>>(cancellationToken);
            return payload is null
                ? new SearchClientResult<TOffer>(false, "search_unavailable", false, [])
                : new SearchClientResult<TOffer>(true, null, payload.SandboxObservation, payload.Offers);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return new SearchClientResult<TOffer>(false, "search_unavailable", false, []);
        }
    }
}

public sealed record SearchCapabilitySummary(
    string Provider,
    string Environment,
    string PointOfSale,
    string Product,
    string Operation,
    string? CarrierCode,
    bool Enabled,
    bool ProductionEnabled,
    string EvidenceStatus,
    string EvidenceNote);

public sealed record HotelSearchInput(
    string Destination,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Adults,
    IReadOnlyList<int> ChildAges,
    int Rooms,
    string RequestedCurrency,
    string PointOfSale);

public sealed record FlightLegInput(string Origin, string Destination, DateOnly DepartureDate);

public sealed record FlightSearchInput(
    IReadOnlyList<FlightLegInput> Legs,
    int Adults,
    int Children,
    int Infants,
    string CabinClass,
    string RequestedCurrency,
    string PointOfSale);

public sealed record SearchClientResult<TOffer>(
    bool Available,
    string? ErrorCode,
    bool SandboxObservation,
    IReadOnlyList<TOffer> Offers);

public sealed record SearchPriceSummary(
    decimal MinimumTotal,
    string ReturnedCurrency,
    string RequestedCurrency,
    string CurrencyProvenance,
    decimal BaseAmount,
    decimal IncludedTaxes,
    decimal IncludedFees);

public sealed record HotelOfferSummary(
    string OfferId,
    string PropertyName,
    string Destination,
    string RoomName,
    string RateName,
    bool Refundable,
    SearchPriceSummary Price,
    DateTimeOffset ExpiresAt,
    bool RequiresRevalidation);

public sealed record FlightSegmentSummary(
    string Origin,
    string Destination,
    DateTimeOffset Departure,
    DateTimeOffset Arrival,
    string MarketingCarrier,
    string OperatingCarrier,
    string FlightNumber);

public sealed record FlightOfferSummary(
    string OfferId,
    string MarketingCarrier,
    string CabinClass,
    int Stops,
    string BaggageSummary,
    IReadOnlyList<FlightSegmentSummary> Segments,
    SearchPriceSummary Price,
    DateTimeOffset ExpiresAt,
    bool RequiresRevalidation);

internal sealed record SearchEnvelope<TOffer>(
    string SearchId,
    string Environment,
    DateTimeOffset SearchedAt,
    bool SandboxObservation,
    IReadOnlyList<TOffer> Offers);

internal sealed record ApiProblem(string Code, string CorrelationId);
