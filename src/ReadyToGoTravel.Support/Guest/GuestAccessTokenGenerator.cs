using System.Security.Cryptography;
using System.Text;

namespace ReadyToGoTravel.Support.Guest;

internal static class GuestAccessTokenGenerator
{
    private const int TokenBytes = 32;

    public static (string RawToken, string TokenHash) Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenBytes);
        var rawToken = Base64UrlEncode(bytes);
        return (rawToken, Hash(rawToken));
    }

    // Deterministic regeneration for a single owning outbox item, keyed by an application secret
    // never persisted in the database. Recomputing the identical raw token (and therefore the same
    // hash already on the active row) lets a retried delivery attempt for the exact same logical
    // notification reuse the credential it already minted instead of rotating it - without ever
    // storing the raw value at rest. The key must stay secret: ticketId and outboxItemId are both
    // readable by anyone with database access, so without a secret key this would be as bad as
    // persisting the raw token in plaintext.
    public static string GenerateForNotification(ReadOnlySpan<byte> signingKey, Guid ticketId, Guid outboxItemId)
    {
        Span<byte> material = stackalloc byte[32];
        ticketId.TryWriteBytes(material[..16]);
        outboxItemId.TryWriteBytes(material[16..]);
        Span<byte> mac = stackalloc byte[32];
        HMACSHA256.HashData(signingKey, material, mac);
        return Base64UrlEncode(mac.ToArray());
    }

    public static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
