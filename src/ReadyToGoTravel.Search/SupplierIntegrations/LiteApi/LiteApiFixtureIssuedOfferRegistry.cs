using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace ReadyToGoTravel.Search.SupplierIntegrations.LiteApi;

public sealed class LiteApiFixtureIssuedOfferRegistry
{
    private readonly ConcurrentDictionary<string, IssuedFixtureOffer> issuedOffers = new(StringComparer.Ordinal);

    internal string Issue(
        string provider,
        string environment,
        string fixtureReference,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt)
    {
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
