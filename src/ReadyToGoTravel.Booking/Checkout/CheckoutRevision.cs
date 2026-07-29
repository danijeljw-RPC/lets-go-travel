using System.Security.Cryptography;
using System.Text;

namespace ReadyToGoTravel.Booking.Checkout;

public sealed class CheckoutRevision
{
    private readonly List<CheckoutRevisionComponent> components;

    private CheckoutRevision(
        Guid id,
        int number,
        IReadOnlyList<CheckoutRevisionComponent> components,
        string transactionCurrency,
        string termsHash,
        DateTimeOffset expiresAt,
        DateTimeOffset resolvedAt)
    {
        Id = id;
        Number = number;
        this.components = components.ToList();
        TransactionCurrency = transactionCurrency;
        TermsHash = termsHash;
        ExpiresAt = expiresAt;
        ResolvedAt = resolvedAt;
        Total = components.Sum(value => value.MinimumTotal);
        PriceComponents = components
            .Select(value => new CheckoutPriceComponent(value.Product, value.MinimumTotal, transactionCurrency))
            .ToArray();
    }

    private CheckoutRevision(
        Guid id,
        int number,
        string transactionCurrency,
        string termsHash,
        DateTimeOffset expiresAt,
        DateTimeOffset resolvedAt)
        : this(id, number, [], transactionCurrency, termsHash, expiresAt, resolvedAt)
    {
    }

    public Guid Id { get; }

    public int Number { get; }

    public IReadOnlyList<CheckoutRevisionComponent> Components => components;

    public IReadOnlyList<CheckoutPriceComponent> PriceComponents { get; }

    public decimal Total { get; }

    public string TransactionCurrency { get; }

    public string TermsHash { get; }

    public DateTimeOffset ExpiresAt { get; }

    public DateTimeOffset ResolvedAt { get; }

    internal static CheckoutRevision Create(
        int number,
        IReadOnlyCollection<ResolvedCheckoutOffer> offers,
        TimeProvider timeProvider)
    {
        var resolvedAt = timeProvider.GetUtcNow().ToUniversalTime();
        var components = offers
            .OrderBy(value => value.Product)
            .Select(value => new CheckoutRevisionComponent(
                Guid.CreateVersion7(resolvedAt),
                value.Product,
                value.OfferId,
                value.ProviderBinding,
                value.ProductDetail,
                value.MinimumTotal,
                value.Revision,
                value.TermsHash,
                value.ExpiresAt.ToUniversalTime(),
                value.ResolvedAt.ToUniversalTime()))
            .ToArray();
        var terms = string.Join('|', components.Select(value => value.TermsHash));
        var termsHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(terms)));
        var expiresAt = components.Min(value => value.ExpiresAt);
        return new CheckoutRevision(
            Guid.CreateVersion7(resolvedAt),
            number,
            components,
            offers.First().Currency,
            termsHash,
            expiresAt,
            resolvedAt);
    }
}

public sealed record CheckoutRevisionComponent(
    Guid Id,
    CheckoutProduct Product,
    string OfferId,
    string ProviderBinding,
    string ProductDetail,
    decimal MinimumTotal,
    string ProviderRevision,
    string TermsHash,
    DateTimeOffset ExpiresAt,
    DateTimeOffset ResolvedAt);

public sealed record CheckoutPriceComponent(
    CheckoutProduct Product,
    decimal Amount,
    string Currency);
