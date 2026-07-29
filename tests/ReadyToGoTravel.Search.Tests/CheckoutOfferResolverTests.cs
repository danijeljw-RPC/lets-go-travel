using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using ReadyToGoTravel.Search.Capabilities;
using ReadyToGoTravel.Search.Checkout;
using ReadyToGoTravel.Search.Contracts;
using ReadyToGoTravel.Search.SupplierIntegrations.LiteApi;

namespace ReadyToGoTravel.Search.Tests;

public sealed class CheckoutOfferResolverTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ResolverReturnsFreshPlatformRevisionForOpaqueHotelOffer()
    {
        var provider = new LiteApiFixtureSearchProvider(new FixedTimeProvider(Now));
        var resolver = new LiteApiFixtureOfferResolver(new FixedTimeProvider(Now));
        var hotelRequest = new HotelSearchRequest(
            "Melbourne",
            new DateOnly(2026, 10, 10),
            new DateOnly(2026, 10, 12),
            2,
            [],
            1,
            "AUD",
            "AU");

        var search = await provider.SearchAsync(hotelRequest, SearchEnvironment.Sandbox);
        var result = await resolver.ResolveAsync(search.Offers.Single().OfferId, SearchEnvironment.Sandbox);

        Assert.True(result.IsSuccess);
        Assert.Equal(CheckoutOfferProduct.Hotel, result.Value!.Product);
        Assert.Equal(420m, result.Value.MinimumTotal);
        Assert.DoesNotContain("supplier", JsonSerializer.Serialize(result.Value), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProductionResolutionFailsClosed()
    {
        var resolver = new LiteApiFixtureOfferResolver(new FixedTimeProvider(Now));

        var result = await resolver.ResolveAsync("off_example", SearchEnvironment.Production);

        Assert.False(result.IsSuccess);
        Assert.Equal("booking_capability_unavailable", result.ErrorCode);
    }

    [Fact]
    public void ProductionModuleRegistersNoCheckoutOfferResolver()
    {
        var services = new ServiceCollection();
        services.AddSearchModule(SearchEnvironment.Production, enableFixtures: false);
        using var provider = services.BuildServiceProvider();

        Assert.Null(provider.GetService<ICheckoutOfferResolver>());
    }

    [Fact]
    public async Task UnknownOpaqueOfferIsRejected()
    {
        var resolver = new LiteApiFixtureOfferResolver(new FixedTimeProvider(Now));

        var result = await resolver.ResolveAsync("off_unknown", SearchEnvironment.Sandbox);

        Assert.False(result.IsSuccess);
        Assert.Equal("checkout_offer_not_found", result.ErrorCode);
    }

    [Fact]
    public async Task ResolverReturnsCurrentRepricedFlightRevision()
    {
        var provider = new LiteApiFixtureSearchProvider(new FixedTimeProvider(Now));
        var resolver = new LiteApiFixtureOfferResolver(new FixedTimeProvider(Now));
        var request = new FlightSearchRequest(
            [new FlightSearchLeg("SYD", "MEL", new DateOnly(2026, 10, 10))],
            1,
            0,
            0,
            CabinClass.Economy,
            "AUD",
            "AU");

        var search = await provider.SearchAsync(request, SearchEnvironment.Sandbox);
        var selectedOffer = search.Offers.Single(offer => offer.MarketingCarrier == "QF");
        var result = await resolver.ResolveAsync(selectedOffer.OfferId, SearchEnvironment.Sandbox);

        Assert.True(result.IsSuccess);
        Assert.Equal(309.40m, result.Value!.MinimumTotal);
        Assert.Equal("flight-qf-r3", result.Value.Revision);
    }

    [Theory]
    [InlineData("sandbox-hotel-disabled-001", "checkout_market_unavailable")]
    [InlineData("sandbox-hotel-expired-001", "checkout_offer_expired")]
    public async Task ResolverRejectsUnavailableFixtureScenarios(string fixtureReference, string errorCode)
    {
        var resolver = new LiteApiFixtureOfferResolver(new FixedTimeProvider(Now));
        var opaqueOfferId = OpaqueOfferId(fixtureReference);

        var result = await resolver.ResolveAsync(opaqueOfferId, SearchEnvironment.Sandbox);

        Assert.False(result.IsSuccess);
        Assert.Equal(errorCode, result.ErrorCode);
    }

    private static string OpaqueOfferId(string fixtureReference)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes($"LiteAPI\u001fsandbox\u001f{fixtureReference}"));
        return "off_" + Convert.ToHexString(digest.AsSpan(0, 12)).ToLowerInvariant();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
