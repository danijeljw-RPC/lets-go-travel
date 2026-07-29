using ReadyToGoTravel.Search.Capabilities;
using ReadyToGoTravel.Search.Contracts;

namespace ReadyToGoTravel.Search.Providers;

public interface IHotelSearchProvider
{
    Task<SearchResponse<HotelSearchOffer>> SearchAsync(
        HotelSearchRequest request,
        SearchEnvironment environment,
        CancellationToken cancellationToken = default);
}

public interface IFlightSearchProvider
{
    Task<SearchResponse<FlightSearchOffer>> SearchAsync(
        FlightSearchRequest request,
        SearchEnvironment environment,
        CancellationToken cancellationToken = default);
}
