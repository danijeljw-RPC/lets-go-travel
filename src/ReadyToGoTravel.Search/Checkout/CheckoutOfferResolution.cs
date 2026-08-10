using System.Text.Json.Serialization;
using ReadyToGoTravel.Search.Capabilities;

namespace ReadyToGoTravel.Search.Checkout;

public enum CheckoutOfferProduct
{
    Hotel,
    Flight,
}

public sealed record CheckoutOffer(
    CheckoutOfferProduct Product,
    string OfferId,
    string ProductDetail,
    decimal MinimumTotal,
    string Currency,
    string TermsHash,
    string Revision,
    DateTimeOffset ExpiresAt,
    DateTimeOffset ResolvedAt);

public sealed record CheckoutOfferResolutionResult(
    bool IsSuccess,
    CheckoutOffer? Value,
    string? ErrorCode)
{
    [JsonIgnore]
    public string? ProviderBinding { get; init; }

    public static CheckoutOfferResolutionResult Success(CheckoutOffer value, string providerBinding) => new(true, value, null)
    {
        ProviderBinding = providerBinding,
    };

    public static CheckoutOfferResolutionResult Failure(string errorCode) => new(false, null, errorCode);
}

public interface ICheckoutOfferResolver
{
    Task<CheckoutOfferResolutionResult> ResolveAsync(
        string offerId,
        SearchEnvironment environment,
        CancellationToken cancellationToken = default);
}
