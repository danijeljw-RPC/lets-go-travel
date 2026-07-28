namespace ReadyToGoTravel.Consumer.Http;

internal sealed record UpsertProfileRequest(string PreferredLocale, bool AdultConfirmed);

internal sealed record CustomerResponse(
    Guid Id,
    string Status,
    string PreferredLocale,
    string DisplayCurrency,
    DateTimeOffset AdultConfirmedAt,
    string? Email);

internal sealed record CreateTripRequest(
    string Title,
    string? PrimaryDestination,
    DateOnly? StartDate,
    DateOnly? EndDate);

internal sealed record TripResponse(
    Guid Id,
    string Title,
    string? PrimaryDestination,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

internal sealed record CreateTravellerRequest(
    string GivenName,
    string FamilyName,
    string? RelationshipLabel,
    bool IsMinor,
    bool GuardianAuthorityConfirmed);

internal sealed record TravellerResponse(
    Guid Id,
    string GivenName,
    string FamilyName,
    string? RelationshipLabel,
    bool IsMinor,
    DateTimeOffset? GuardianAuthorityConfirmedAt,
    DateTimeOffset CreatedAt);

internal sealed record LocaleResponse(string Code, string DisplayName, string DefaultCurrency);

internal sealed record SensitiveTravellerStorageResponse(bool Enabled, string[] Categories);
