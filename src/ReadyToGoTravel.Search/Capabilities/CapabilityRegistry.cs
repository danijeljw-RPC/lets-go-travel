namespace ReadyToGoTravel.Search.Capabilities;

public enum SearchEnvironment
{
    Sandbox,
    Production,
}

public enum SearchProduct
{
    Accommodation,
    Flight,
}

public enum SearchOperation
{
    HotelSearch,
    FlightSearch,
    PriceVerification,
    Booking,
}

public enum CapabilityEvidenceStatus
{
    Fixture,
    ObservedSearchOnly,
    ProductionEvidenceRequired,
}

public sealed record SearchCapability(
    string Provider,
    SearchEnvironment Environment,
    string PointOfSale,
    SearchProduct Product,
    SearchOperation Operation,
    string? CarrierCode,
    bool Enabled,
    bool ProductionEnabled,
    CapabilityEvidenceStatus EvidenceStatus,
    string EvidenceNote);

public interface ICapabilityRegistry
{
    IReadOnlyList<SearchCapability> GetSnapshot(SearchEnvironment environment, string pointOfSale);

    bool IsEnabled(
        SearchEnvironment environment,
        string pointOfSale,
        SearchOperation operation,
        string? carrierCode = null);

    bool IsEnabled(
        string provider,
        SearchEnvironment environment,
        string pointOfSale,
        SearchProduct product,
        SearchOperation operation,
        string? carrierCode = null);
}

public sealed class CapabilityRegistry : ICapabilityRegistry
{
    private readonly IReadOnlyList<SearchCapability> capabilities;

    private CapabilityRegistry(IReadOnlyList<SearchCapability> capabilities)
    {
        this.capabilities = capabilities;
    }

    public static CapabilityRegistry CreateDefaults()
    {
        var capabilities = new List<SearchCapability>
        {
            new(
                "LiteAPI",
                SearchEnvironment.Sandbox,
                "AU",
                SearchProduct.Accommodation,
                SearchOperation.HotelSearch,
                null,
                true,
                false,
                CapabilityEvidenceStatus.Fixture,
                "Sanitized deterministic sandbox fixture; not production inventory."),
            new(
                "LiteAPI",
                SearchEnvironment.Production,
                "AU",
                SearchProduct.Accommodation,
                SearchOperation.HotelSearch,
                null,
                false,
                false,
                CapabilityEvidenceStatus.ProductionEvidenceRequired,
                "Production access, booking and settlement evidence remain required."),
            new(
                "LiteAPI",
                SearchEnvironment.Sandbox,
                "AU",
                SearchProduct.Accommodation,
                SearchOperation.PriceVerification,
                null,
                true,
                false,
                CapabilityEvidenceStatus.Fixture,
                "Sanitized deterministic sandbox fixture supports hotel price verification only."),
            new(
                "LiteAPI",
                SearchEnvironment.Sandbox,
                "AU",
                SearchProduct.Accommodation,
                SearchOperation.Booking,
                null,
                true,
                false,
                CapabilityEvidenceStatus.Fixture,
                "Sanitized deterministic sandbox fixture supports hotel booking only."),
        };

        foreach (var carrier in new[] { "QF", "JQ", "VA" })
        {
            capabilities.Add(new SearchCapability(
                "LiteAPI",
                SearchEnvironment.Sandbox,
                "AU",
                SearchProduct.Flight,
                SearchOperation.FlightSearch,
                carrier,
                true,
                false,
                CapabilityEvidenceStatus.ObservedSearchOnly,
                "Carrier offers were observed for dated sandbox/account searches; booking, ticketing and servicing are not proven."));
            capabilities.Add(new SearchCapability(
                "LiteAPI",
                SearchEnvironment.Production,
                "AU",
                SearchProduct.Flight,
                SearchOperation.FlightSearch,
                carrier,
                false,
                false,
                CapabilityEvidenceStatus.ProductionEvidenceRequired,
                "Production entitlement and end-to-end carrier evidence remain required."));

            if (carrier == "QF")
            {
                capabilities.Add(new SearchCapability(
                    "LiteAPI",
                    SearchEnvironment.Sandbox,
                    "AU",
                    SearchProduct.Flight,
                    SearchOperation.PriceVerification,
                    carrier,
                    true,
                    false,
                    CapabilityEvidenceStatus.Fixture,
                    "Sanitized deterministic sandbox fixture supports QF flight price verification only."));
                capabilities.Add(new SearchCapability(
                    "LiteAPI",
                    SearchEnvironment.Sandbox,
                    "AU",
                    SearchProduct.Flight,
                    SearchOperation.Booking,
                    carrier,
                    true,
                    false,
                    CapabilityEvidenceStatus.Fixture,
                    "Sanitized deterministic sandbox fixture supports QF flight booking only."));
            }
        }

        return new CapabilityRegistry(capabilities);
    }

    public static CapabilityRegistry Create(IReadOnlyList<SearchCapability> capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        return new CapabilityRegistry(capabilities.ToArray());
    }

    public IReadOnlyList<SearchCapability> GetSnapshot(SearchEnvironment environment, string pointOfSale) =>
        capabilities
            .Where(capability => capability.Environment == environment
                && string.Equals(capability.PointOfSale, pointOfSale, StringComparison.OrdinalIgnoreCase))
            .ToArray();

    public bool IsEnabled(
        SearchEnvironment environment,
        string pointOfSale,
        SearchOperation operation,
        string? carrierCode = null) =>
        GetSnapshot(environment, pointOfSale).Any(capability =>
            capability.Operation == operation
            && capability.Enabled
            && (carrierCode is null
                || string.Equals(capability.CarrierCode, carrierCode, StringComparison.OrdinalIgnoreCase)));

    public bool IsEnabled(
        string provider,
        SearchEnvironment environment,
        string pointOfSale,
        SearchProduct product,
        SearchOperation operation,
        string? carrierCode = null) =>
        capabilities.Any(capability =>
            string.Equals(capability.Provider, provider, StringComparison.OrdinalIgnoreCase)
            && capability.Environment == environment
            && string.Equals(capability.PointOfSale, pointOfSale, StringComparison.OrdinalIgnoreCase)
            && capability.Product == product
            && capability.Operation == operation
            && capability.Enabled
            && (carrierCode is null
                ? capability.CarrierCode is null
                : string.Equals(capability.CarrierCode, carrierCode, StringComparison.OrdinalIgnoreCase)));
}
