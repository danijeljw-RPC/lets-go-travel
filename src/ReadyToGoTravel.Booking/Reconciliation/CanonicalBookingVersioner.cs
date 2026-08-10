using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReadyToGoTravel.Booking.Bookings;

namespace ReadyToGoTravel.Booking.Reconciliation;

internal static class CanonicalBookingVersioner
{
    internal const string CurrentCanonicalisationVersion = "booking-v1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false,
    };

    internal static BookingVersion Create(
        ComponentBooking component,
        RetrievedBookingState state,
        BookingVersion? previous,
        DateTimeOffset observedAt,
        string source,
        string correlationId)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        if (component.Product != state.Product)
        {
            throw new InvalidOperationException("Retrieved booking product does not match the component booking.");
        }

        var canonical = ToCanonical(state);
        var snapshot = JsonSerializer.Serialize(canonical, JsonOptions);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot)));
        var diff = previous is null ? "[]" : CreateDiff(previous.CanonicalSnapshotJson, snapshot);
        var flags = previous is null ? new[] { "initial" } : ExtractFlags(diff);
        var metadata = JsonSerializer.Serialize(
            new CanonicalMetadata(
                state.Status.ToString(),
                state.FlightSegments.Count,
                state.Hotel is not null,
                flags.Length > 0),
            JsonOptions);
        DateTimeOffset? nextDepartureAt = state.FlightSegments.Count == 0
            ? null
            : state.FlightSegments.Min(value => value.ScheduledDeparture.ToUniversalTime());

        return new BookingVersion(
            Guid.CreateVersion7(observedAt),
            component.Id,
            component.CurrentVersionNumber + 1,
            observedAt.ToUniversalTime(),
            state.SupplierObservedAt?.ToUniversalTime(),
            source,
            CurrentCanonicalisationVersion,
            snapshot,
            hash,
            metadata,
            JsonSerializer.Serialize(flags, JsonOptions),
            diff,
            "Informational",
            correlationId,
            nextDepartureAt);
    }

    private static CanonicalBooking ToCanonical(RetrievedBookingState state)
    {
        var segments = state.FlightSegments
            .OrderBy(value => value.Identity, StringComparer.Ordinal)
            .Select(value => new CanonicalFlightSegment(
                value.Identity.Trim(),
                value.MarketingCarrier.Trim().ToUpperInvariant(),
                value.FlightNumber.Trim().ToUpperInvariant(),
                value.Origin.Trim().ToUpperInvariant(),
                value.Destination.Trim().ToUpperInvariant(),
                value.ScheduledDeparture.ToUniversalTime(),
                value.ScheduledArrival.ToUniversalTime()))
            .ToArray();
        var hotel = state.Hotel is null
            ? null
            : new CanonicalHotelStay(
                state.Hotel.PropertyName.Trim(),
                state.Hotel.CheckIn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                state.Hotel.CheckOut.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                state.Hotel.Room.Trim(),
                state.Hotel.Inclusions
                    .Select(value => value.Trim())
                    .Where(value => value.Length > 0)
                    .Order(StringComparer.Ordinal)
                    .ToArray(),
                state.Hotel.CancellationPolicy.Trim());

        return new CanonicalBooking(
            state.Product.ToString(),
            state.Status.ToString(),
            NormalizeOptional(state.CustomerConfirmation),
            hotel,
            segments,
            state.Total,
            NormalizeOptional(state.Currency)?.ToUpperInvariant());
    }

    private static string CreateDiff(string oldJson, string newJson)
    {
        using var oldDocument = JsonDocument.Parse(oldJson);
        using var newDocument = JsonDocument.Parse(newJson);
        var entries = new List<CanonicalDiffEntry>();
        Compare(oldDocument.RootElement, newDocument.RootElement, string.Empty, entries);
        return JsonSerializer.Serialize(entries, JsonOptions);
    }

    private static void Compare(
        JsonElement oldValue,
        JsonElement newValue,
        string path,
        ICollection<CanonicalDiffEntry> entries)
    {
        if (oldValue.ValueKind != newValue.ValueKind)
        {
            entries.Add(new CanonicalDiffEntry(path, "replaced", oldValue.Clone(), newValue.Clone()));
            return;
        }

        if (oldValue.ValueKind == JsonValueKind.Object)
        {
            var oldProperties = oldValue.EnumerateObject().ToDictionary(value => value.Name, StringComparer.Ordinal);
            var newProperties = newValue.EnumerateObject().ToDictionary(value => value.Name, StringComparer.Ordinal);
            foreach (var name in oldProperties.Keys.Union(newProperties.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                var childPath = string.IsNullOrEmpty(path) ? name : $"{path}.{name}";
                if (!oldProperties.TryGetValue(name, out var oldProperty))
                {
                    entries.Add(new CanonicalDiffEntry(childPath, "added", null, newProperties[name].Value.Clone()));
                }
                else if (!newProperties.TryGetValue(name, out var newProperty))
                {
                    entries.Add(new CanonicalDiffEntry(childPath, "removed", oldProperty.Value.Clone(), null));
                }
                else
                {
                    Compare(oldProperty.Value, newProperty.Value, childPath, entries);
                }
            }

            return;
        }

        if (oldValue.ValueKind == JsonValueKind.Array)
        {
            var oldItems = oldValue.EnumerateArray().ToArray();
            var newItems = newValue.EnumerateArray().ToArray();
            if (oldItems.Length != newItems.Length)
            {
                entries.Add(new CanonicalDiffEntry(path, "replaced", oldValue.Clone(), newValue.Clone()));
                return;
            }

            for (var index = 0; index < oldItems.Length; index++)
            {
                Compare(oldItems[index], newItems[index], $"{path}[{index}]", entries);
            }

            return;
        }

        if (oldValue.GetRawText() != newValue.GetRawText())
        {
            entries.Add(new CanonicalDiffEntry(path, "replaced", oldValue.Clone(), newValue.Clone()));
        }
    }

    private static string[] ExtractFlags(string diffJson)
    {
        var entries = JsonSerializer.Deserialize<CanonicalDiffEntry[]>(diffJson, JsonOptions) ?? [];
        return entries
            .Select(value => value.Path switch
            {
                var path when path.Contains("scheduledDeparture", StringComparison.Ordinal) ||
                                  path.Contains("scheduledArrival", StringComparison.Ordinal) => "schedule",
                "flightSegments" => "itinerary",
                var path when path.Contains("origin", StringComparison.Ordinal) || path.Contains("destination", StringComparison.Ordinal) => "airport",
                var path when path.Contains("flightNumber", StringComparison.Ordinal) => "flight-number",
                var path when path.Contains("room", StringComparison.Ordinal) => "room",
                var path when path.Contains("inclusions", StringComparison.Ordinal) => "inclusion",
                var path when path.Contains("cancellationPolicy", StringComparison.Ordinal) => "policy",
                var path when path.Contains("propertyName", StringComparison.Ordinal) => "relocation",
                var path when path.Contains("status", StringComparison.Ordinal) => "status",
                _ => "details",
            })
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record CanonicalBooking(
        string Product,
        string Status,
        string? CustomerConfirmation,
        CanonicalHotelStay? Hotel,
        IReadOnlyList<CanonicalFlightSegment> FlightSegments,
        decimal? Total,
        string? Currency);

    private sealed record CanonicalHotelStay(
        string PropertyName,
        string CheckIn,
        string CheckOut,
        string Room,
        IReadOnlyList<string> Inclusions,
        string CancellationPolicy);

    private sealed record CanonicalFlightSegment(
        string Identity,
        string MarketingCarrier,
        string FlightNumber,
        string Origin,
        string Destination,
        DateTimeOffset ScheduledDeparture,
        DateTimeOffset ScheduledArrival);

    private sealed record CanonicalMetadata(
        string Status,
        int FlightSegmentCount,
        bool HasHotel,
        bool HasMeaningfulChange);

    private sealed record CanonicalDiffEntry(
        string Path,
        string ChangeType,
        JsonElement? OldValue,
        JsonElement? NewValue);
}
