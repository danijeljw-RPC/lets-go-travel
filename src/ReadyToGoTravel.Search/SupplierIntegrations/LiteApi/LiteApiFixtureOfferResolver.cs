using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReadyToGoTravel.Search.Capabilities;
using ReadyToGoTravel.Search.Checkout;

namespace ReadyToGoTravel.Search.SupplierIntegrations.LiteApi;

public sealed class LiteApiFixtureOfferResolver(
    TimeProvider timeProvider,
    ICapabilityRegistry capabilityRegistry) : ICheckoutOfferResolver
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
            IsOfferIdForFixture(
                offerId,
                OpaqueId("off_", fixture.Provider, fixture.Environment, candidate.FixtureReference),
                fixture.Provider,
                fixture.Environment,
                candidate.FixtureReference));
        if (offer is null)
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

        var fixtureOfferId = OpaqueId("off_", fixture.Provider, fixture.Environment, offer.FixtureReference);
        _ = TryReadIssuedAt(
            offerId,
            fixtureOfferId,
            fixture.Provider,
            fixture.Environment,
            offer.FixtureReference,
            out var issuedAt);
        var expiresAt = issuedAt.AddMinutes(offer.LifetimeMinutes);
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

    private static string OpaqueId(string prefix, params string[] values)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\u001f', values)));
        return prefix + Convert.ToHexString(digest.AsSpan(0, 12)).ToLowerInvariant();
    }

    private static bool IsOfferIdForFixture(
        string offerId,
        string fixtureOfferId,
        string provider,
        string environment,
        string fixtureReference) => TryReadIssuedAt(
            offerId,
            fixtureOfferId,
            provider,
            environment,
            fixtureReference,
            out _);

    private static bool TryReadIssuedAt(
        string offerId,
        string fixtureOfferId,
        string provider,
        string environment,
        string fixtureReference,
        out DateTimeOffset issuedAt)
    {
        issuedAt = default;
        var prefix = fixtureOfferId + "_";
        if (!offerId.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var generationParts = offerId[prefix.Length..].Split('_');
        if (generationParts.Length != 2
            || !long.TryParse(
                generationParts[0],
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out var unixMilliseconds))
        {
            return false;
        }

        var expectedSignature = OpaqueId(
            string.Empty,
            provider,
            environment,
            fixtureReference,
            generationParts[0])[..16];
        if (!string.Equals(generationParts[1], expectedSignature, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            issuedAt = DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
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
