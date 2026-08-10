using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;

namespace ReadyToGoTravel.Api.Infrastructure;

// Every other rate-limit policy in this file partitions by the immediate TCP peer's address, which
// is correct only when clients reach the API directly. The Web host runs Blazor Server, so every
// call it makes to the API - including the anonymous support-ticket creation flow and every guest
// ticket/thread request - originates from the Web host's own connection, not the browser's.
// Partitioning by RemoteIpAddress therefore puts every visitor of a Web deployment into one shared
// bucket, and a handful of unrelated guests in the same minute lock everyone else out.
internal static class SupportRateLimitPartitions
{
    public const string ForwardedClientIpHeader = "X-Rtgt-Forwarded-Client-Ip";
    public const string InternalCallerSecretHeader = "X-Rtgt-Internal-Caller-Secret";

    // Trusts the forwarded client IP only when it is accompanied by a secret known solely to the
    // Web host's own configuration - never a bare header, which any direct caller could set to
    // fabricate a fresh bucket per request and defeat the limiter entirely. Shared by every policy
    // below that needs to tell a genuinely different browser apart from another one behind the same
    // Web-host connection.
    private static string ResolveConnectionKey(HttpContext context, string? internalCallerSecret)
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

    public static string TicketCreationKey(HttpContext context, string? internalCallerSecret) =>
        ResolveConnectionKey(context, internalCallerSecret);

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
    // audit row. A coarse ceiling closes that: it is consulted synchronously as a side effect of
    // computing the partition (there is no supported way to make ASP.NET Core's rate limiter
    // consult two independent partitioned limiters for one policy), so a caller who cannot exhaust
    // the per-token bucket still exhausts the shared ceiling for its resolved connection key and
    // gets a real 429. That key is resolved the same trusted-forwarded-IP-or-direct-peer way as
    // support-ticket-create, not the raw connection address alone - otherwise every guest reaching
    // the API through the Web host's Blazor Server proxy (which is every guest, for every ticket
    // view, reply and attachment request) would collapse into the exact single shared bucket this
    // ceiling exists to avoid.
    private const int GuestIpCeilingPermitLimit = 200;

    private static readonly PartitionedRateLimiter<string> GuestIpCeiling = PartitionedRateLimiter.Create<string, string>(
        connectionKey => RateLimitPartition.GetFixedWindowLimiter(connectionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = GuestIpCeilingPermitLimit,
            QueueLimit = 0,
            Window = TimeSpan.FromMinutes(1),
        }));

    public static RateLimitPartition<string> GuestPartition(HttpContext context, string? internalCallerSecret)
    {
        var connectionKey = ResolveConnectionKey(context, internalCallerSecret);
        if (!GuestIpCeiling.AttemptAcquire(connectionKey).IsAcquired)
        {
            return RateLimitPartition.Get($"guest-ceiling-exceeded:{connectionKey}", _ => AlwaysRejectRateLimiter.Instance);
        }

        return RateLimitPartition.GetFixedWindowLimiter(GuestKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            QueueLimit = 0,
            Window = TimeSpan.FromMinutes(1),
        });
    }

    // FixedWindowRateLimiterOptions.PermitLimit must be at least 1 - a limiter constructed with
    // PermitLimit = 0 throws ArgumentOutOfRangeException instead of ever rejecting, which would
    // turn the 201st (and every subsequent) request past the ceiling into an unhandled 500 rather
    // than the intended 429. This limiter has no state and every acquisition attempt is denied.
    private sealed class AlwaysRejectRateLimiter : RateLimiter
    {
        public static readonly AlwaysRejectRateLimiter Instance = new();

        public override TimeSpan? IdleDuration => TimeSpan.Zero;

        public override RateLimiterStatistics GetStatistics() => new()
        {
            CurrentAvailablePermits = 0,
            CurrentQueuedCount = 0,
            TotalFailedLeases = 0,
            TotalSuccessfulLeases = 0,
        };

        protected override RateLimitLease AttemptAcquireCore(int permitCount) => RejectedLease.Instance;

        protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken) =>
            ValueTask.FromResult<RateLimitLease>(RejectedLease.Instance);

        private sealed class RejectedLease : RateLimitLease
        {
            public static readonly RejectedLease Instance = new();

            public override bool IsAcquired => false;

            public override IEnumerable<string> MetadataNames => [];

            public override bool TryGetMetadata(string metadataName, out object? metadata)
            {
                metadata = null;
                return false;
            }
        }
    }
}
