using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReadyToGoTravel.Booking.Providers;

namespace ReadyToGoTravel.Booking.SupplierIntegrations.LiteApi;

public sealed class LiteApiFixtureBookingProvider : IBookingProvider
{
    private static readonly JsonSerializerOptions FixtureJsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public async Task<BookingProviderExecutionResult> BookAsync(
        BookingCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.IdempotencyKey);
        var fixture = await LoadFixtureAsync(cancellationToken);
        var scenario = fixture.Bookings.SingleOrDefault(value =>
            string.Equals(value.Scenario, command.ProviderBinding, StringComparison.Ordinal)
            && string.Equals(value.Product, command.Product.ToString(), StringComparison.Ordinal));
        return scenario is null
            ? new BookingProviderExecutionResult(BookingProviderStatus.Unknown, null, "booking_scenario_not_found")
            : Map(scenario);
    }

    public async Task<BookingProviderExecutionResult> RetrieveAsync(
        string externalReference,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalReference);
        var fixture = await LoadFixtureAsync(cancellationToken);
        var scenario = fixture.Bookings.SingleOrDefault(value =>
            string.Equals(value.ExternalReference, externalReference, StringComparison.Ordinal));
        return scenario is null
            ? new BookingProviderExecutionResult(BookingProviderStatus.Unknown, externalReference, "booking_not_found")
            : Map(scenario);
    }

    private static BookingProviderExecutionResult Map(BookingFixtureScenario scenario) => new(
        scenario.Status switch
        {
            "Confirmed" => BookingProviderStatus.Confirmed,
            "Pending" => BookingProviderStatus.Pending,
            "Failed" => BookingProviderStatus.Failed,
            "Unknown" => BookingProviderStatus.Unknown,
            _ => throw new InvalidDataException($"Unsupported booking fixture status '{scenario.Status}'."),
        },
        scenario.ExternalReference,
        scenario.ErrorCode);

    private static async Task<BookingFixture> LoadFixtureAsync(CancellationToken cancellationToken)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("booking-scenarios.json", StringComparison.Ordinal));
        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidDataException("Embedded booking fixture was not found.");
        return await JsonSerializer.DeserializeAsync<BookingFixture>(stream, FixtureJsonOptions, cancellationToken)
            ?? throw new InvalidDataException("Embedded booking fixture was empty.");
    }

    private sealed record BookingFixture(string Provider, string Environment, IReadOnlyList<BookingFixtureScenario> Bookings);

    private sealed record BookingFixtureScenario(
        string Scenario,
        string Product,
        string Status,
        string? ExternalReference,
        string? ErrorCode);
}
