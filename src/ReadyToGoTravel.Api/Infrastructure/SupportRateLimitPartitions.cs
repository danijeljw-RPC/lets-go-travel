using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace ReadyToGoTravel.Api.Infrastructure;

// Every other rate-limit policy in this file partitions by the immediate TCP peer's address, which
// is correct only when clients reach the API directly. The Web host runs Blazor Server, so every
// call it makes to the API - including the anonymous support-ticket creation flow - originates from
// the Web host's own connection, not the browser's. Partitioning that policy by RemoteIpAddress
// therefore puts every visitor of a Web deployment into one shared bucket, and the first five
// unrelated guests to submit a ticket in the same minute lock everyone else out.
internal static class SupportRateLimitPartitions
{
    public const string ForwardedClientIpHeader = "X-Rtgt-Forwarded-Client-Ip";
    public const string InternalCallerSecretHeader = "X-Rtgt-Internal-Caller-Secret";

    // Trusts the forwarded client IP only when it is accompanied by a secret known solely to the
    // Web host's own configuration - never a bare header, which any direct caller could set to
    // fabricate a fresh bucket per request and defeat the limiter entirely.
    public static string TicketCreationKey(HttpContext context, string? internalCallerSecret)
    {
        if (!string.IsNullOrEmpty(internalCallerSecret))
        {
            var providedSecret = context.Request.Headers[InternalCallerSecretHeader].ToString();
            var forwardedIp = context.Request.Headers[ForwardedClientIpHeader].ToString();
            if (!string.IsNullOrEmpty(forwardedIp) &&
                CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(providedSecret), Encoding.UTF8.GetBytes(internalCallerSecret)) &&
                IPAddress.TryParse(forwardedIp, out var parsed))
            {
                return $"forwarded:{parsed}";
            }
        }

        return $"direct:{context.Connection.RemoteIpAddress}";
    }

    // Authenticated requests carry an identity that survives being proxied through the Web host,
    // unlike the TCP peer address; partitioning by subject also means one caller can no longer
    // exhaust a shared bucket for every other authenticated user behind the same deployment.
    public static string AuthenticatedKey(HttpContext context)
    {
        var subject = context.User.FindFirst("sub")?.Value;
        return string.IsNullOrEmpty(subject) ? $"anon:{context.Connection.RemoteIpAddress}" : $"sub:{subject}";
    }

    // A guest bearer credential is itself the identity: hashing it (never logging or storing the
    // raw value) naturally partitions per guest link rather than per network peer, so it survives
    // being proxied and limits abuse per credential instead of collapsing every guest behind one
    // Web deployment into a single bucket.
    public static string GuestKey(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        if (!header.StartsWith(prefix, StringComparison.Ordinal) || header.Length <= prefix.Length)
        {
            return $"anon:{context.Connection.RemoteIpAddress}";
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(header[prefix.Length..])));
        return $"token:{hash}";
    }
}
