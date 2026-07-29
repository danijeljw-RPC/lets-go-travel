using ReadyToGoTravel.Search.Capabilities;
using ReadyToGoTravel.Search.Contracts;
using ReadyToGoTravel.Search.Pricing;

namespace ReadyToGoTravel.Search.Tests;

public sealed class SearchDomainTests
{
    [Fact]
    public void MinimumTotalRejectsIncludedComponentsGreaterThanTheTotal()
    {
        var result = OfferPrice.Create(
            100m,
            "AUD",
            "AUD",
            CurrencyProvenance.SupplierReturned,
            90m,
            12m,
            0m);

        Assert.False(result.IsSuccess);
        Assert.Equal("price_components_exceed_total", result.ErrorCode);
    }

    [Fact]
    public void MinimumTotalPreservesCurrencyProvenanceAndComponents()
    {
        var result = OfferPrice.Create(
            289.40m,
            "AUD",
            "AUD",
            CurrencyProvenance.SupplierReturned,
            220m,
            54.40m,
            15m);

        Assert.True(result.IsSuccess);
        Assert.Equal(289.40m, result.Value!.MinimumTotal);
        Assert.Equal("AUD", result.Value.ReturnedCurrency);
        Assert.Equal(CurrencyProvenance.SupplierReturned, result.Value.CurrencyProvenance);
        Assert.Equal(54.40m, result.Value.IncludedTaxes);
        Assert.Equal(15m, result.Value.IncludedFees);
    }

    [Fact]
    public void HotelSearchRejectsCheckoutThatIsNotAfterCheckin()
    {
        var request = new HotelSearchRequest(
            "Melbourne",
            new DateOnly(2026, 10, 10),
            new DateOnly(2026, 10, 10),
            2,
            [],
            1,
            "AUD",
            "AU");

        var result = request.Validate();

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_stay_dates", result.ErrorCode);
    }

    [Fact]
    public void FlightSearchRejectsMalformedAirportCodes()
    {
        var request = new FlightSearchRequest(
            [new FlightSearchLeg("Sydney", "MEL", new DateOnly(2026, 10, 10))],
            1,
            0,
            0,
            CabinClass.Economy,
            "AUD",
            "AU");

        var result = request.Validate();

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_airport_code", result.ErrorCode);
    }

    [Fact]
    public void AustralianSandboxRegistryRecordsObservedCarriersWithoutProductionClaims()
    {
        var registry = CapabilityRegistry.CreateDefaults();

        var sandbox = registry.GetSnapshot(SearchEnvironment.Sandbox, "AU")
            .Where(capability => capability.Operation == SearchOperation.FlightSearch)
            .OrderBy(capability => capability.CarrierCode)
            .ToArray();

        Assert.Equal(["JQ", "QF", "VA"], sandbox.Select(capability => capability.CarrierCode));
        Assert.All(sandbox, capability =>
        {
            Assert.True(capability.Enabled);
            Assert.False(capability.ProductionEnabled);
            Assert.Equal(CapabilityEvidenceStatus.ObservedSearchOnly, capability.EvidenceStatus);
        });
    }

    [Fact]
    public void ProductionRegistryFailsClosedForEverySupplierCapability()
    {
        var production = CapabilityRegistry.CreateDefaults()
            .GetSnapshot(SearchEnvironment.Production, "AU");

        Assert.NotEmpty(production);
        Assert.All(production, capability =>
        {
            Assert.False(capability.Enabled);
            Assert.False(capability.ProductionEnabled);
        });
    }
}
