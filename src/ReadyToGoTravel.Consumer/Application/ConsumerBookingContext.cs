using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Consumer.Customers;
using ReadyToGoTravel.Consumer.Persistence;
using ReadyToGoTravel.Consumer.Trips;

namespace ReadyToGoTravel.Consumer.Application;

public interface IConsumerBookingContext
{
    Task<ConsumerBookingContextResult> ResolveAsync(
        string subject,
        Guid tripId,
        IReadOnlyCollection<Guid> travellerIds,
        CancellationToken cancellationToken = default);
}

public sealed record ConsumerBookingContext(
    Guid CustomerId,
    Guid TripId,
    IReadOnlyList<ConsumerTraveller> Travellers);

public sealed record ConsumerBookingContextResult(
    ConsumerBookingContext? Value,
    string? ErrorCode)
{
    public bool IsSuccess => ErrorCode is null;
}

public sealed record ConsumerTraveller(
    Guid TravellerId,
    string GivenName,
    string FamilyName,
    bool IsMinor,
    DateTimeOffset? GuardianAuthorityConfirmedAt);

internal sealed class ConsumerBookingContextResolver(ConsumerDbContext context) : IConsumerBookingContext
{
    public async Task<ConsumerBookingContextResult> ResolveAsync(
        string subject,
        Guid tripId,
        IReadOnlyCollection<Guid> travellerIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(travellerIds);

        var customer = await context.Customers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.Subject == subject && value.Status == CustomerStatus.Active,
                cancellationToken);
        if (customer is null)
        {
            return new ConsumerBookingContextResult(null, "profile_required");
        }

        var tripExists = await context.Trips
            .AsNoTracking()
            .AnyAsync(
                value => value.Id == tripId &&
                         value.CustomerId == customer.Id &&
                         value.Status != TripStatus.Archived,
                cancellationToken);
        if (!tripExists)
        {
            return new ConsumerBookingContextResult(null, "trip_not_found");
        }

        var requestedTravellerIds = travellerIds.Distinct().ToArray();
        var travellers = await context.Travellers
            .AsNoTracking()
            .Where(value => value.CustomerId == customer.Id && requestedTravellerIds.Contains(value.Id))
            .Select(value => new ConsumerTraveller(
                value.Id,
                value.GivenName,
                value.FamilyName,
                value.IsMinor,
                value.GuardianAuthorityConfirmedAt))
            .ToArrayAsync(cancellationToken);
        if (travellers.Length != requestedTravellerIds.Length)
        {
            return new ConsumerBookingContextResult(null, "traveller_not_found");
        }

        var byId = travellers.ToDictionary(value => value.TravellerId);
        var orderedTravellers = requestedTravellerIds.Select(value => byId[value]).ToArray();
        return new ConsumerBookingContextResult(
            new ConsumerBookingContext(customer.Id, tripId, orderedTravellers),
            null);
    }
}
