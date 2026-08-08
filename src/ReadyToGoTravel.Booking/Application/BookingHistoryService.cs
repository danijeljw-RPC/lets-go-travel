using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Booking.Http;
using ReadyToGoTravel.Booking.Persistence;
using ReadyToGoTravel.Consumer.Application;

namespace ReadyToGoTravel.Booking.Application;

internal sealed class BookingHistoryService(
    BookingDbContext database,
    IConsumerBookingContext consumerContext)
{
    public async Task<BookingHistoryResponse?> GetAsync(
        string subject,
        Guid checkoutId,
        CancellationToken cancellationToken)
    {
        var customerId = await consumerContext.ResolveCustomerIdAsync(subject, cancellationToken);
        if (!customerId.HasValue)
        {
            return null;
        }

        var checkout = await database.Checkouts.AsNoTracking()
            .Include(value => value.Components)
            .SingleOrDefaultAsync(
                value => value.Id == checkoutId && value.CustomerId == customerId.Value,
                cancellationToken);
        if (checkout is null)
        {
            return null;
        }

        var componentIds = checkout.Components.Select(value => value.Id).ToArray();
        var versions = await database.BookingVersions.AsNoTracking()
            .Where(value => componentIds.Contains(value.ComponentBookingId))
            .OrderBy(value => value.ComponentBookingId)
            .ThenBy(value => value.VersionNumber)
            .ToArrayAsync(cancellationToken);
        var byComponent = versions.ToLookup(value => value.ComponentBookingId);
        return new BookingHistoryResponse(
            checkout.Id,
            checkout.Components
                .OrderBy(value => value.Product)
                .Select(component => new BookingComponentHistoryResponse(
                    component.Id,
                    component.Product.ToString(),
                    component.Status.ToString(),
                    component.CurrentVersionNumber,
                    component.LastReconciledAt,
                    byComponent[component.Id]
                        .Select(version => new BookingVersionHistoryResponse(
                            version.VersionNumber,
                            version.ObservedAt,
                            version.EffectiveAt,
                            version.Source,
                            version.CanonicalisationVersion,
                            version.Severity,
                            JsonSerializer.Deserialize<string[]>(version.FlagsJson) ?? [],
                            ParseJson(version.DiffJson)))
                        .ToArray()))
                .ToArray());
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
