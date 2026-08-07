using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace ReadyToGoTravel.Search.SupplierIntegrations.LiteApi;

public sealed class LiteApiFixtureIssuedOfferRegistry
{
    private static readonly TimeSpan ExpiredOfferRetention = TimeSpan.FromHours(1);
    private readonly ConcurrentDictionary<string, IssuedFixtureOffer> issuedOffers = new(StringComparer.Ordinal);

    internal string Issue(
        string provider,
        string environment,
        string fixtureReference,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt)
    {
        var evictionCutoff = issuedAt - ExpiredOfferRetention;
        foreach (var issuedOffer in issuedOffers.Where(value => value.Value.ExpiresAt <= evictionCutoff))
        {
            issuedOffers.TryRemove(issuedOffer.Key, out _);
        }

        while (true)
        {
            var offerId = "off_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
            var issuedOffer = new IssuedFixtureOffer(
                provider,
                environment,
                fixtureReference,
                issuedAt,
                expiresAt);
            if (issuedOffers.TryAdd(offerId, issuedOffer))
            {
                return offerId;
            }
        }
    }

    internal bool TryGet(string offerId, out IssuedFixtureOffer? issuedOffer) =>
        issuedOffers.TryGetValue(offerId, out issuedOffer);
}

internal sealed record IssuedFixtureOffer(
    string Provider,
    string Environment,
    string FixtureReference,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);
