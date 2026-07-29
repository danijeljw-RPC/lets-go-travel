using System.Net.Http.Json;

namespace ReadyToGoTravel.Web.Client;

public sealed class ConsumerApiClient(HttpClient client)
{
    public async Task<CustomerProfile?> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await client.GetFromJsonAsync<CustomerProfile>("/api/v1/me", cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    public async Task<CustomerProfile?> SaveProfileAsync(
        string preferredLocale,
        bool adultConfirmed,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.PutAsJsonAsync(
                "/api/v1/me",
                new { preferredLocale, adultConfirmed },
                cancellationToken);

            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<CustomerProfile>(cancellationToken)
                : null;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<TripSummary>> GetTripsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await client.GetFromJsonAsync<TripSummary[]>("/api/v1/trips", cancellationToken)
                ?? [];
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return [];
        }
    }

    public async Task<TripSummary?> CreateTripAsync(
        string title,
        string? destination,
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.PostAsJsonAsync(
                "/api/v1/trips",
                new { title, primaryDestination = destination, startDate, endDate },
                cancellationToken);

            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<TripSummary>(cancellationToken)
                : null;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<TravellerSummary>> GetTravellersAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await client.GetFromJsonAsync<TravellerSummary[]>(
                "/api/v1/travellers",
                cancellationToken) ?? [];
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return [];
        }
    }

    public async Task<TravellerSummary?> CreateTravellerAsync(
        string givenName,
        string familyName,
        string? relationshipLabel,
        bool isMinor,
        bool guardianAuthorityConfirmed,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.PostAsJsonAsync(
                "/api/v1/travellers",
                new
                {
                    givenName,
                    familyName,
                    relationshipLabel,
                    isMinor,
                    guardianAuthorityConfirmed
                },
                cancellationToken);

            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<TravellerSummary>(cancellationToken)
                : null;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }
}

public sealed record CustomerProfile(
    Guid Id,
    string Status,
    string PreferredLocale,
    string DisplayCurrency,
    DateTimeOffset AdultConfirmedAt,
    string? Email);

public sealed record TripSummary(
    Guid Id,
    string Title,
    string? PrimaryDestination,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record TravellerSummary(
    Guid Id,
    string GivenName,
    string FamilyName,
    string? RelationshipLabel,
    bool IsMinor,
    DateTimeOffset? GuardianAuthorityConfirmedAt,
    DateTimeOffset CreatedAt);
