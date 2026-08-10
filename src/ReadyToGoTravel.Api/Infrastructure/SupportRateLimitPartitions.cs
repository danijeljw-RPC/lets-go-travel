using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
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

    // Per-token partitioning alone is exploitable: an unauthenticated caller can send a fresh,
    // syntactically valid but never-issued Bearer value on every request, so GuestKey mints a brand
    // new partition each time and no per-token bucket is ever exhausted - even though every one of
    // those requests still performs a database lookup and writes a GuestLinkAuthenticationFailed
    // audit row. A coarse, process-wide ceiling keyed by the connection address closes that: it is
    // consulted synchronously as a side effect of computing the partition (there is no supported
    // way to make ASP.NET Core's rate limiter consult two independent partitioned limiters for one
    // policy), so a caller who cannot exhaust the per-token bucket still exhausts the shared ceiling
    // for its own connection and gets a real 429 via the framework's normal rejection pipeline.
    private const int GuestIpCeilingPermitLimit = 200;

    private static readonly PartitionedRateLimiter<string> GuestIpCeiling = PartitionedRateLimiter.Create<string, string>(
        ip => RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = GuestIpCeilingPermitLimit,
            QueueLimit = 0,
            Window = TimeSpan.FromMinutes(1),
        }));

    public static RateLimitPartition<string> GuestPartition(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (!GuestIpCeiling.AttemptAcquire(ip).IsAcquired)
        {
            return RateLimitPartition.GetFixedWindowLimiter($"guest-ip-ceiling-exceeded:{ip}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 0,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1),
            });
        }

        return RateLimitPartition.GetFixedWindowLimiter(GuestKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            QueueLimit = 0,
            Window = TimeSpan.FromMinutes(1),
        });
    }
}
