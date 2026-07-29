namespace ReadyToGoTravel.Search.Contracts;

public enum CabinClass
{
    Economy,
    PremiumEconomy,
    Business,
    First,
}

public sealed record HotelSearchRequest(
    string Destination,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Adults,
    IReadOnlyList<int> ChildAges,
    int Rooms,
    string RequestedCurrency,
    string PointOfSale)
{
    public SearchResult<HotelSearchRequest> Validate()
    {
        if (string.IsNullOrWhiteSpace(Destination) || Destination.Trim().Length > 120)
        {
            return SearchResult.Failure<HotelSearchRequest>("invalid_destination");
        }

        if (CheckOut <= CheckIn)
        {
            return SearchResult.Failure<HotelSearchRequest>("invalid_stay_dates");
        }

        if (Adults is < 1 or > 16 || Rooms is < 1 or > 8 || ChildAges.Count > 8
            || ChildAges.Any(age => age is < 0 or > 17))
        {
            return SearchResult.Failure<HotelSearchRequest>("invalid_occupancy");
        }

        if (!SearchCodeValidation.IsCurrency(RequestedCurrency))
        {
            return SearchResult.Failure<HotelSearchRequest>("invalid_currency");
        }

        if (!SearchCodeValidation.IsCountry(PointOfSale))
        {
            return SearchResult.Failure<HotelSearchRequest>("invalid_point_of_sale");
        }

        return SearchResult.Success(this with
        {
            Destination = Destination.Trim(),
            RequestedCurrency = RequestedCurrency.ToUpperInvariant(),
            PointOfSale = PointOfSale.ToUpperInvariant(),
        });
    }
}

public sealed record FlightSearchLeg(string Origin, string Destination, DateOnly DepartureDate);

public sealed record FlightSearchRequest(
    IReadOnlyList<FlightSearchLeg> Legs,
    int Adults,
    int Children,
    int Infants,
    CabinClass CabinClass,
    string RequestedCurrency,
    string PointOfSale)
{
    public SearchResult<FlightSearchRequest> Validate()
    {
        if (Legs.Count is < 1 or > 4)
        {
            return SearchResult.Failure<FlightSearchRequest>("invalid_flight_legs");
        }

        if (Legs.Any(leg => !SearchCodeValidation.IsIata(leg.Origin)
                || !SearchCodeValidation.IsIata(leg.Destination)))
        {
            return SearchResult.Failure<FlightSearchRequest>("invalid_airport_code");
        }

        if (Legs.Any(leg => string.Equals(leg.Origin, leg.Destination, StringComparison.OrdinalIgnoreCase)))
        {
            return SearchResult.Failure<FlightSearchRequest>("invalid_flight_legs");
        }

        if (Adults is < 1 or > 9 || Children is < 0 or > 8 || Infants is < 0 or > 8
            || Infants > Adults || Adults + Children + Infants > 9)
        {
            return SearchResult.Failure<FlightSearchRequest>("invalid_passengers");
        }

        if (!Enum.IsDefined(CabinClass))
        {
            return SearchResult.Failure<FlightSearchRequest>("invalid_cabin_class");
        }

        if (!SearchCodeValidation.IsCurrency(RequestedCurrency))
        {
            return SearchResult.Failure<FlightSearchRequest>("invalid_currency");
        }

        if (!SearchCodeValidation.IsCountry(PointOfSale))
        {
            return SearchResult.Failure<FlightSearchRequest>("invalid_point_of_sale");
        }

        return SearchResult.Success(this with
        {
            Legs = Legs.Select(leg => leg with
            {
                Origin = leg.Origin.ToUpperInvariant(),
                Destination = leg.Destination.ToUpperInvariant(),
            }).ToArray(),
            RequestedCurrency = RequestedCurrency.ToUpperInvariant(),
            PointOfSale = PointOfSale.ToUpperInvariant(),
        });
    }
}

public sealed record SearchResult<T>(bool IsSuccess, T? Value, string? ErrorCode);

public static class SearchResult
{
    public static SearchResult<T> Success<T>(T value) => new(true, value, null);

    public static SearchResult<T> Failure<T>(string errorCode) => new(false, default, errorCode);
}

internal static class SearchCodeValidation
{
    public static bool IsIata(string value) => IsUppercaseCode(value, 3);

    public static bool IsCurrency(string value) => IsUppercaseCode(value, 3);

    public static bool IsCountry(string value) => IsUppercaseCode(value, 2);

    private static bool IsUppercaseCode(string value, int length) =>
        value.Length == length && value.All(character => character is >= 'A' and <= 'Z');
}
