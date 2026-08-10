using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReadyToGoTravel.Search.Capabilities;
using ReadyToGoTravel.Search.Checkout;

namespace ReadyToGoTravel.Search.SupplierIntegrations.LiteApi;

public sealed class LiteApiFixtureOfferResolver(
    TimeProvider timeProvider,
    ICapabilityRegistry capabilityRegistry,
    LiteApiFixtureIssuedOfferRegistry issuedOffers) : ICheckoutOfferResolver
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

        if (!issuedOffers.TryGet(offerId, out var issuedOffer))
        {
            return CheckoutOfferResolutionResult.Failure("checkout_offer_not_found");
        }

        var fixture = await LoadFixtureAsync(cancellationToken);
        var offer = fixture.Offers.SingleOrDefault(candidate =>
            string.Equals(candidate.FixtureReference, issuedOffer!.FixtureReference, StringComparison.Ordinal));
        if (offer is null
            || !string.Equals(fixture.Provider, issuedOffer!.Provider, StringComparison.Ordinal)
            || !string.Equals(fixture.Environment, issuedOffer.Environment, StringComparison.OrdinalIgnoreCase))
        {
            return CheckoutOfferResolutionResult.Failure("checkout_offer_not_found");
        }

        if (!Enum.TryParse<SearchEnvironment>(fixture.Environment, true, out var fixtureEnvironment)
            || fixtureEnvironment != environment)
        {
            return CheckoutOfferResolutionResult.Failure("booking_capability_unavailable");
        }

        var checkoutProduct = ParseProduct(offer.Product);
        var searchProduct = ToSearchProduct(checkoutProduct);
        var carrierCode = checkoutProduct == CheckoutOfferProduct.Flight ? offer.CarrierCode : null;
        if ((checkoutProduct == CheckoutOfferProduct.Flight && string.IsNullOrWhiteSpace(carrierCode))
            || !capabilityRegistry.IsEnabled(
                fixture.Provider,
                environment,
                fixture.PointOfSale,
                searchProduct,
                SearchOperation.PriceVerification,
                carrierCode)
            || !capabilityRegistry.IsEnabled(
                fixture.Provider,
                environment,
                fixture.PointOfSale,
                searchProduct,
                SearchOperation.Booking,
                carrierCode))
        {
            return CheckoutOfferResolutionResult.Failure("booking_capability_unavailable");
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        if (!offer.MarketEnabled || offer.Scenario == "disabled-market")
        {
            return CheckoutOfferResolutionResult.Failure("checkout_market_unavailable");
        }

        var expiresAt = issuedOffer.ExpiresAt;
        if (offer.Scenario == "expired" || expiresAt <= now)
        {
            return CheckoutOfferResolutionResult.Failure("checkout_offer_expired");
        }

        var product = new CheckoutOffer(
            checkoutProduct,
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

    private static SearchProduct ToSearchProduct(CheckoutOfferProduct product) => product switch
    {
        CheckoutOfferProduct.Hotel => SearchProduct.Accommodation,
        CheckoutOfferProduct.Flight => SearchProduct.Flight,
        _ => throw new InvalidDataException("Unsupported checkout offer product."),
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

    private sealed record CheckoutOffersFixture(
        string Provider,
        string Environment,
        string PointOfSale,
        IReadOnlyList<CheckoutFixtureOffer> Offers);

    private sealed record CheckoutFixtureOffer(
        string FixtureReference,
        string ProviderBinding,
        string Product,
        string? CarrierCode,
        string ProductDetail,
        decimal MinimumTotal,
        string Currency,
        string TermsHash,
        string Revision,
        int LifetimeMinutes,
        bool MarketEnabled,
        string Scenario);
}
