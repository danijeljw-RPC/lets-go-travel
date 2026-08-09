using System.Text.Json;
using ReadyToGoTravel.Booking.Reconciliation;

namespace ReadyToGoTravel.Booking.Notifications;

public enum BookingChangeSeverity
{
    Informational,
    Minor,
    Material,
    TravelBlocking,
}

public sealed record BookingChangeClassification(
    BookingChangeSeverity Severity,
    bool ShouldEmail,
    IReadOnlyList<string> Flags);

internal static class BookingChangeClassifier
{
    internal static BookingChangeClassification Classify(
        BookingVersion? previous,
        BookingVersion current)
    {
        ArgumentNullException.ThrowIfNull(current);
        var flags = JsonSerializer.Deserialize<string[]>(current.FlagsJson) ?? [];
        if (previous is null)
        {
            return new BookingChangeClassification(
                BookingChangeSeverity.Informational,
                false,
                flags);
        }

        using var diff = JsonDocument.Parse(current.DiffJson);
        var entries = diff.RootElement.EnumerateArray().ToArray();
        if (entries.Any(entry =>
                Path(entry).EndsWith("status", StringComparison.Ordinal) &&
                NewString(entry) is "Cancelled" or "Failed"))
        {
            return new BookingChangeClassification(
                BookingChangeSeverity.TravelBlocking,
                true,
                flags);
        }

        if (entries.Any(entry => Path(entry).Contains("propertyName", StringComparison.Ordinal)))
        {
            return new BookingChangeClassification(
                BookingChangeSeverity.TravelBlocking,
                true,
                flags);
        }

        if (entries.Any(entry =>
                Path(entry).Equals("flightSegments", StringComparison.Ordinal) ||
                Path(entry).Contains("origin", StringComparison.Ordinal) ||
                Path(entry).Contains("destination", StringComparison.Ordinal) ||
                Path(entry).Contains("flightNumber", StringComparison.Ordinal) ||
                Path(entry).Contains("room", StringComparison.Ordinal) ||
                Path(entry).Contains("inclusions", StringComparison.Ordinal) ||
                Path(entry).Contains("cancellationPolicy", StringComparison.Ordinal)))
        {
            return new BookingChangeClassification(
                BookingChangeSeverity.Material,
                true,
                flags);
        }

        var scheduleChanges = entries
            .Where(entry =>
                Path(entry).Contains("scheduledDeparture", StringComparison.Ordinal) ||
                Path(entry).Contains("scheduledArrival", StringComparison.Ordinal))
            .Select(ScheduleMovement)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();
        if (scheduleChanges.Length > 0)
        {
            var severity = scheduleChanges.Max() >= TimeSpan.FromMinutes(30)
                ? BookingChangeSeverity.Material
                : BookingChangeSeverity.Minor;
            return new BookingChangeClassification(severity, true, flags);
        }

        return new BookingChangeClassification(
            BookingChangeSeverity.Informational,
            false,
            flags);
    }

    private static string Path(JsonElement entry) =>
        entry.GetProperty("path").GetString() ?? string.Empty;

    private static string? NewString(JsonElement entry) =>
        entry.TryGetProperty("newValue", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static TimeSpan? ScheduleMovement(JsonElement entry)
    {
        if (!entry.TryGetProperty("oldValue", out var oldValue) ||
            !entry.TryGetProperty("newValue", out var newValue) ||
            oldValue.ValueKind != JsonValueKind.String ||
            newValue.ValueKind != JsonValueKind.String ||
            !DateTimeOffset.TryParse(oldValue.GetString(), out var oldTime) ||
            !DateTimeOffset.TryParse(newValue.GetString(), out var newTime))
        {
            return null;
        }

        return (newTime - oldTime).Duration();
    }
}
