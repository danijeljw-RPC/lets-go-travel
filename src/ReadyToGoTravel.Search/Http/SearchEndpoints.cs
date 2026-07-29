using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ReadyToGoTravel.Search.Capabilities;
using ReadyToGoTravel.Search.Contracts;
using ReadyToGoTravel.Search.Providers;

namespace ReadyToGoTravel.Search.Http;

public static class SearchEndpoints
{
    public static RouteGroupBuilder MapSearchEndpoints(this RouteGroupBuilder api)
    {
        var search = api.MapGroup("/search")
            .RequireRateLimiting("search");

        search.MapGet("/capabilities", GetCapabilities);
        search.MapPost("/hotels", SearchHotelsAsync);
        search.MapPost("/flights", SearchFlightsAsync);

        return api;
    }

    private static IResult GetCapabilities(
        string? pointOfSale,
        ICapabilityRegistry registry,
        SearchRuntime runtime,
        HttpContext context)
    {
        var market = pointOfSale ?? "AU";
        if (!SearchCodeValidation.IsCountry(market))
        {
            return SearchHttpResults.Problem(
                context,
                StatusCodes.Status400BadRequest,
                "invalid_point_of_sale",
                "Point of sale must be a two-letter uppercase country code.");
        }

        var response = registry.GetSnapshot(runtime.Environment, market)
            .Select(CapabilityHttpResponse.FromDomain)
            .ToArray();
        return Results.Ok(response);
    }

    private static async Task<IResult> SearchHotelsAsync(
        HotelSearchHttpRequest request,
        ICapabilityRegistry registry,
        SearchRuntime runtime,
        IServiceProvider services,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var validation = request.ToDomain().Validate();
        if (!validation.IsSuccess)
        {
            return ValidationProblem(context, validation.ErrorCode!);
        }

        var searchRequest = validation.Value!;
        if (!registry.IsEnabled(
                runtime.Environment,
                searchRequest.PointOfSale,
                SearchOperation.HotelSearch))
        {
            return CapabilityUnavailable(context);
        }

        var provider = services.GetService<IHotelSearchProvider>();
        if (provider is null)
        {
            return CapabilityUnavailable(context);
        }

        var result = await provider.SearchAsync(searchRequest, runtime.Environment, cancellationToken);
        return Results.Ok(MapHotels(result));
    }

    private static async Task<IResult> SearchFlightsAsync(
        FlightSearchHttpRequest request,
        ICapabilityRegistry registry,
        SearchRuntime runtime,
        IServiceProvider services,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var conversion = request.ToDomain();
        if (!conversion.IsSuccess)
        {
            return ValidationProblem(context, conversion.ErrorCode!);
        }

        var validation = conversion.Value!.Validate();
        if (!validation.IsSuccess)
        {
            return ValidationProblem(context, validation.ErrorCode!);
        }

        var searchRequest = validation.Value!;
        if (!registry.IsEnabled(
                runtime.Environment,
                searchRequest.PointOfSale,
                SearchOperation.FlightSearch))
        {
            return CapabilityUnavailable(context);
        }

        var provider = services.GetService<IFlightSearchProvider>();
        if (provider is null)
        {
            return CapabilityUnavailable(context);
        }

        var result = await provider.SearchAsync(searchRequest, runtime.Environment, cancellationToken);
        var allowedOffers = result.Offers
            .Where(offer => registry.IsEnabled(
                runtime.Environment,
                searchRequest.PointOfSale,
                SearchOperation.FlightSearch,
                offer.MarketingCarrier))
            .ToArray();
        return Results.Ok(MapFlights(result with { Offers = allowedOffers }));
    }

    private static SearchHttpResponse<HotelOfferHttpResponse> MapHotels(
        SearchResponse<HotelSearchOffer> result) =>
        new(
            result.SearchId,
            EnvironmentText(result.Environment),
            result.SearchedAt,
            result.SandboxObservation,
            result.Offers.Select(offer => new HotelOfferHttpResponse(
                offer.OfferId,
                offer.PropertyName,
                offer.Destination,
                offer.RoomName,
                offer.RateName,
                offer.Refundable,
                PriceHttpResponse.FromDomain(offer.Price),
                offer.ExpiresAt,
                offer.RequiresRevalidation)).ToArray());

    private static SearchHttpResponse<FlightOfferHttpResponse> MapFlights(
        SearchResponse<FlightSearchOffer> result) =>
        new(
            result.SearchId,
            EnvironmentText(result.Environment),
            result.SearchedAt,
            result.SandboxObservation,
            result.Offers.Select(offer => new FlightOfferHttpResponse(
                offer.OfferId,
                offer.MarketingCarrier,
                offer.CabinClass,
                offer.Stops,
                offer.BaggageSummary,
                offer.Segments.Select(segment => new FlightSegmentHttpResponse(
                    segment.Origin,
                    segment.Destination,
                    segment.Departure,
                    segment.Arrival,
                    segment.MarketingCarrier,
                    segment.OperatingCarrier,
                    segment.FlightNumber)).ToArray(),
                PriceHttpResponse.FromDomain(offer.Price),
                offer.ExpiresAt,
                offer.RequiresRevalidation)).ToArray());

    private static IResult ValidationProblem(HttpContext context, string code) =>
        SearchHttpResults.Problem(
            context,
            StatusCodes.Status400BadRequest,
            code,
            "The search request is invalid.");

    private static IResult CapabilityUnavailable(HttpContext context) =>
        SearchHttpResults.Problem(
            context,
            StatusCodes.Status503ServiceUnavailable,
            "search_capability_unavailable",
            "Search is not enabled for this environment and market.");

    private static string EnvironmentText(SearchEnvironment environment) =>
        environment == SearchEnvironment.Sandbox ? "sandbox" : "production";
}
