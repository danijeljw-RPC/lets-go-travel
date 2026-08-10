using System.Text.Json;
using ReadyToGoTravel.Search.Capabilities;
using ReadyToGoTravel.Search.Contracts;
using ReadyToGoTravel.Search.Pricing;
using ReadyToGoTravel.Search.SupplierIntegrations.LiteApi;

namespace ReadyToGoTravel.Search.Tests;

public sealed class LiteApiFixtureContractTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HotelFixtureMapsToPlatformPriceAndExpiry()
    {
        var provider = new LiteApiFixtureSearchProvider(
            new FixedTimeProvider(Now),
            new LiteApiFixtureIssuedOfferRegistry());
        var request = new HotelSearchRequest(
            "Melbourne",
            new DateOnly(2026, 10, 10),
            new DateOnly(2026, 10, 12),
            2,
            [],
            1,
            "AUD",
            "AU");

        var response = await provider.SearchAsync(request, SearchEnvironment.Sandbox);

        var offer = Assert.Single(response.Offers);
        Assert.StartsWith("off_", offer.OfferId, StringComparison.Ordinal);
        Assert.Equal("Harbour Lane Hotel", offer.PropertyName);
        Assert.Equal(420m, offer.Price.MinimumTotal);
        Assert.Equal(48m, offer.Price.IncludedTaxes);
        Assert.Equal(12m, offer.Price.IncludedFees);
        Assert.Equal(CurrencyProvenance.SupplierReturned, offer.Price.CurrencyProvenance);
        Assert.Equal(Now.AddMinutes(20), offer.ExpiresAt);
        Assert.True(offer.RequiresRevalidation);
        Assert.True(response.SandboxObservation);
    }

    [Fact]
    public async Task FlightFixtureMapsObservedAustralianCarriersWithoutSupplierReferences()
    {
        var provider = new LiteApiFixtureSearchProvider(
            new FixedTimeProvider(Now),
            new LiteApiFixtureIssuedOfferRegistry());
        var request = new FlightSearchRequest(
            [new FlightSearchLeg("SYD", "MEL", new DateOnly(2026, 10, 10))],
            1,
            0,
            0,
            CabinClass.Economy,
            "AUD",
            "AU");

        var response = await provider.SearchAsync(request, SearchEnvironment.Sandbox);

        Assert.Equal(["JQ", "QF", "VA"], response.Offers.Select(offer => offer.MarketingCarrier).Order());
        Assert.All(response.Offers, offer =>
        {
            Assert.StartsWith("off_", offer.OfferId, StringComparison.Ordinal);
            Assert.Equal("AUD", offer.Price.ReturnedCurrency);
            Assert.True(offer.RequiresRevalidation);
            Assert.True(offer.ExpiresAt > Now);
        });
        Assert.DoesNotContain(
            typeof(FlightSearchOffer).GetProperties(),
            property => property.Name.Contains("Supplier", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Provider", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UnsupportedPointOfSaleReturnsNoFixtureOffers()
    {
        var provider = new LiteApiFixtureSearchProvider(
            new FixedTimeProvider(Now),
            new LiteApiFixtureIssuedOfferRegistry());
        var request = new HotelSearchRequest(
            "Melbourne",
            new DateOnly(2026, 10, 10),
            new DateOnly(2026, 10, 12),
            2,
            [],
            1,
            "AUD",
            "NZ");

        var response = await provider.SearchAsync(request, SearchEnvironment.Sandbox);

        Assert.Empty(response.Offers);
    }

    [Theory]
    [InlineData("hotel-search.json")]
    [InlineData("flight-search.json")]
    public void SanitizedFixturesContainNoCredentialOrTravellerFields(string fixtureName)
    {
        var path = Path.Combine(
            RepositoryRoot(),
            "src",
            "ReadyToGoTravel.Search",
            "SupplierIntegrations",
            "LiteApi",
            "Fixtures",
            fixtureName);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var prohibited = new[]
        {
            "apiKey", "password", "token", "email", "givenName", "familyName", "dateOfBirth", "passport"
        };

        var propertyNames = EnumeratePropertyNames(document.RootElement).ToArray();

        Assert.DoesNotContain(propertyNames, name =>
            prohibited.Any(value => string.Equals(value, name, StringComparison.OrdinalIgnoreCase)));
    }

    private static IEnumerable<string> EnumeratePropertyNames(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                yield return property.Name;
                foreach (var nested in EnumeratePropertyNames(property.Value))
                {
                    yield return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var nested in EnumeratePropertyNames(item))
                {
                    yield return nested;
                }
            }
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ReadyToGoTravel.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
