using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReadyToGoTravel.Search.Capabilities;
using ReadyToGoTravel.Search.Checkout;

namespace ReadyToGoTravel.Search.SupplierIntegrations.LiteApi;

public sealed class LiteApiFixtureOfferResolver(TimeProvider timeProvider) : ICheckoutOfferResolver
{
    private static readonly JsonSerializerOptions FixtureJsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public async Task<CheckoutOfferResolutionResult> ResolveAsync(
        string offerId,
        SearchEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        if (environment != SearchEnvironment.Sandbox)
        {
            return CheckoutOfferResolutionResult.Failure("booking_capability_unavailable");
        }

        var fixture = await LoadFixtureAsync(cancellationToken);
        var offer = fixture.Offers.SingleOrDefault(candidate =>
            string.Equals(
                offerId,
                OpaqueId("off_", fixture.Provider, fixture.Environment, candidate.FixtureReference),
                StringComparison.Ordinal));
        if (offer is null)
        {
            return CheckoutOfferResolutionResult.Failure("checkout_offer_not_found");
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        if (!offer.MarketEnabled || offer.Scenario == "disabled-market")
        {
            return CheckoutOfferResolutionResult.Failure("checkout_market_unavailable");
        }

        var expiresAt = now.AddMinutes(offer.LifetimeMinutes);
        if (offer.Scenario == "expired" || expiresAt <= now)
        {
            return CheckoutOfferResolutionResult.Failure("checkout_offer_expired");
        }

        var product = new CheckoutOffer(
            ParseProduct(offer.Product),
            offerId,
            offer.ProductDetail,
            offer.MinimumTotal,
            offer.Currency,
            offer.TermsHash,
            offer.Revision,
            expiresAt,
            now);
        return CheckoutOfferResolutionResult.Success(product, offer.ProviderBinding);
    }

    private static CheckoutOfferProduct ParseProduct(string value) => value switch
    {
        "hotel" => CheckoutOfferProduct.Hotel,
        "flight" => CheckoutOfferProduct.Flight,
        _ => throw new InvalidDataException($"Unsupported checkout fixture product '{value}'."),
    };

    private static async Task<CheckoutOffersFixture> LoadFixtureAsync(CancellationToken cancellationToken)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("checkout-offers.json", StringComparison.Ordinal));
        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidDataException("Embedded checkout offer fixture was not found.");

        return await JsonSerializer.DeserializeAsync<CheckoutOffersFixture>(stream, FixtureJsonOptions, cancellationToken)
            ?? throw new InvalidDataException("Embedded checkout offer fixture was empty.");
    }

    private static string OpaqueId(string prefix, params string[] values)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\u001f', values)));
        return prefix + Convert.ToHexString(digest.AsSpan(0, 12)).ToLowerInvariant();
    }

    private sealed record CheckoutOffersFixture(string Provider, string Environment, IReadOnlyList<CheckoutFixtureOffer> Offers);

    private sealed record CheckoutFixtureOffer(
        string FixtureReference,
        string ProviderBinding,
        string Product,
        string ProductDetail,
        decimal MinimumTotal,
        string Currency,
        string TermsHash,
        string Revision,
        int LifetimeMinutes,
        bool MarketEnabled,
        string Scenario);
}
