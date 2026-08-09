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

    public static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
