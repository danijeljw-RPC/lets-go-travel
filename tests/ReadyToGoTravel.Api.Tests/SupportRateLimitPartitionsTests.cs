using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using ReadyToGoTravel.Api.Infrastructure;

namespace ReadyToGoTravel.Api.Tests;

public sealed class SupportRateLimitPartitionsTests
{
    [Fact]
    public void TicketCreationKeyFallsBackToTheDirectPeerAddressWhenNoSecretIsConfigured()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.10");

        var key = SupportRateLimitPartitions.TicketCreationKey(context, internalCallerSecret: null);

        Assert.Equal("direct:203.0.113.10", key);
    }

    [Fact]
    public void TicketCreationKeyIgnoresAForwardedIpWithoutTheMatchingInternalSecret()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.10");
        context.Request.Headers[SupportRateLimitPartitions.ForwardedClientIpHeader] = "198.51.100.7";
        context.Request.Headers[SupportRateLimitPartitions.InternalCallerSecretHeader] = "wrong-secret";

        var key = SupportRateLimitPartitions.TicketCreationKey(context, internalCallerSecret: "real-secret");

        // A caller other than the trusted Web host cannot forge a distinct bucket per request just
        // by sending a different X-Rtgt-Forwarded-Client-Ip - the direct peer address still wins.
        Assert.Equal("direct:203.0.113.10", key);
    }

    [Fact]
    public void TicketCreationKeyUsesTheForwardedIpWhenTheInternalSecretMatches()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.10");
        context.Request.Headers[SupportRateLimitPartitions.ForwardedClientIpHeader] = "198.51.100.7";
        context.Request.Headers[SupportRateLimitPartitions.InternalCallerSecretHeader] = "real-secret";

        var key = SupportRateLimitPartitions.TicketCreationKey(context, internalCallerSecret: "real-secret");

        Assert.Equal("forwarded:198.51.100.7", key);
    }

    [Fact]
    public void TicketCreationKeyDistinguishesTwoDifferentGuestsBehindTheSameWebHost()
    {
        var contextA = new DefaultHttpContext();
        contextA.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.5");
        contextA.Request.Headers[SupportRateLimitPartitions.ForwardedClientIpHeader] = "198.51.100.7";
        contextA.Request.Headers[SupportRateLimitPartitions.InternalCallerSecretHeader] = "real-secret";

        var contextB = new DefaultHttpContext();
        contextB.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.5");
        contextB.Request.Headers[SupportRateLimitPartitions.ForwardedClientIpHeader] = "198.51.100.99";
        contextB.Request.Headers[SupportRateLimitPartitions.InternalCallerSecretHeader] = "real-secret";

        var keyA = SupportRateLimitPartitions.TicketCreationKey(contextA, internalCallerSecret: "real-secret");
        var keyB = SupportRateLimitPartitions.TicketCreationKey(contextB, internalCallerSecret: "real-secret");

        // Both requests reach the API from the same Web-host connection (10.0.0.5), but two
        // genuinely different guests must not share a rate-limit bucket.
        Assert.NotEqual(keyA, keyB);
    }

    [Fact]
    public void AuthenticatedKeyUsesTheSubjectClaimWhenPresent()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.5");
        context.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim("sub", "customer-1")], "test"));

        var key = SupportRateLimitPartitions.AuthenticatedKey(context);

        Assert.Equal("sub:customer-1", key);
    }

    [Fact]
    public void AuthenticatedKeyDistinguishesTwoUsersBehindTheSameWebHostConnection()
    {
        var contextA = new DefaultHttpContext { Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.5") } };
        contextA.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim("sub", "customer-1")], "test"));
        var contextB = new DefaultHttpContext { Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.5") } };
        contextB.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim("sub", "customer-2")], "test"));

        Assert.NotEqual(
            SupportRateLimitPartitions.AuthenticatedKey(contextA),
            SupportRateLimitPartitions.AuthenticatedKey(contextB));
    }

    [Fact]
    public void GuestKeyHashesTheBearerCredentialRatherThanUsingTheConnectionAddress()
    {
        var contextA = new DefaultHttpContext { Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.5") } };
        contextA.Request.Headers.Authorization = "Bearer guest-token-a";
        var contextB = new DefaultHttpContext { Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.5") } };
        contextB.Request.Headers.Authorization = "Bearer guest-token-b";

        var keyA = SupportRateLimitPartitions.GuestKey(contextA);
        var keyB = SupportRateLimitPartitions.GuestKey(contextB);

        Assert.NotEqual(keyA, keyB);
        Assert.DoesNotContain("guest-token-a", keyA, StringComparison.Ordinal);
        Assert.DoesNotContain("guest-token-b", keyB, StringComparison.Ordinal);
    }

    [Fact]
    public void GuestKeyFallsBackToTheConnectionAddressWithoutABearerCredential()
    {
        var context = new DefaultHttpContext { Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.5") } };

        var key = SupportRateLimitPartitions.GuestKey(context);

        Assert.Equal("anon:10.0.0.5", key);
    }

    [Fact]
    public void GuestPartitionDeniesRequestsPastTheSharedIpCeilingEvenWithANeverRepeatedToken()
    {
        // A distinct, test-reserved documentation-range address (RFC 5737) so this test's usage of
        // the shared, process-wide ceiling limiter never collides with any other test.
        var ip = System.Net.IPAddress.Parse("192.0.2.201");

        for (var attempt = 0; attempt < 200; attempt++)
        {
            var context = new DefaultHttpContext { Connection = { RemoteIpAddress = ip } };
            // A fresh, never-repeated token every time: per-token partitioning alone would give
            // every one of these its own bucket and never throttle anything.
            context.Request.Headers.Authorization = $"Bearer never-issued-token-{attempt}";

            var partition = SupportRateLimitPartitions.GuestPartition(context, internalCallerSecret: null);

            Assert.StartsWith("token:", partition.PartitionKey, StringComparison.Ordinal);
        }

        var overflowContext = new DefaultHttpContext { Connection = { RemoteIpAddress = ip } };
        overflowContext.Request.Headers.Authorization = "Bearer never-issued-token-overflow";

        var overflowPartition = SupportRateLimitPartitions.GuestPartition(overflowContext, internalCallerSecret: null);

        // The 201st distinct, never-issued token from the same connection is denied outright by
        // the shared ceiling - it never even reaches its own per-token bucket.
        Assert.StartsWith("guest-ceiling-exceeded:", overflowPartition.PartitionKey, StringComparison.Ordinal);
    }

    [Fact]
    public void GuestPartitionCeilingsAreIndependentPerConnectionAddress()
    {
        var ipA = System.Net.IPAddress.Parse("192.0.2.211");
        var ipB = System.Net.IPAddress.Parse("192.0.2.212");

        for (var attempt = 0; attempt < 200; attempt++)
        {
            var context = new DefaultHttpContext { Connection = { RemoteIpAddress = ipA } };
            context.Request.Headers.Authorization = $"Bearer exhausting-ip-a-{attempt}";
            SupportRateLimitPartitions.GuestPartition(context, internalCallerSecret: null);
        }

        var contextB = new DefaultHttpContext { Connection = { RemoteIpAddress = ipB } };
        contextB.Request.Headers.Authorization = "Bearer first-request-from-ip-b";

        var partitionB = SupportRateLimitPartitions.GuestPartition(contextB, internalCallerSecret: null);

        Assert.StartsWith("token:", partitionB.PartitionKey, StringComparison.Ordinal);
    }

    [Fact]
    public void GuestPartitionsExceededLimiterActuallyRejectsInsteadOfThrowing()
    {
        var ip = System.Net.IPAddress.Parse("192.0.2.221");
        RateLimitPartition<string> exceededPartition = default;
        for (var attempt = 0; attempt <= 200; attempt++)
        {
            var context = new DefaultHttpContext { Connection = { RemoteIpAddress = ip } };
            context.Request.Headers.Authorization = $"Bearer token-{attempt}";
            exceededPartition = SupportRateLimitPartitions.GuestPartition(context, internalCallerSecret: null);
        }

        // Constructing and using the limiter this partition returns must not throw - a
        // PermitLimit of zero would make FixedWindowRateLimiter throw ArgumentOutOfRangeException
        // here, turning every request past the ceiling into an unhandled 500 instead of a 429.
        using var limiter = PartitionedRateLimiter.Create<string, string>(_ => exceededPartition);
        using var lease = limiter.AttemptAcquire("irrelevant-resource");

        Assert.False(lease.IsAcquired);
    }

    [Fact]
    public void GuestPartitionUsesTheForwardedBrowserAddressWhenTheInternalSecretMatches()
    {
        // Two different browsers reaching the API through the same Web-host connection (Blazor
        // Server) must not share a ceiling with each other, or with a connection resolved purely
        // by RemoteIpAddress for a caller that never sends the forwarded header at all.
        var webHostConnectionAddress = System.Net.IPAddress.Parse("10.0.0.9");
        var contextBrowserA = new DefaultHttpContext { Connection = { RemoteIpAddress = webHostConnectionAddress } };
        contextBrowserA.Request.Headers.Authorization = "Bearer browser-a-token";
        contextBrowserA.Request.Headers[SupportRateLimitPartitions.ForwardedClientIpHeader] = "198.51.100.31";
        contextBrowserA.Request.Headers[SupportRateLimitPartitions.InternalCallerSecretHeader] = "real-secret";

        var partitionA = SupportRateLimitPartitions.GuestPartition(contextBrowserA, internalCallerSecret: "real-secret");

        Assert.StartsWith("token:", partitionA.PartitionKey, StringComparison.Ordinal);

        // Exhausting browser A's forwarded-address ceiling must not affect browser B, even though
        // both share the same underlying Web-host TCP connection address.
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var context = new DefaultHttpContext { Connection = { RemoteIpAddress = webHostConnectionAddress } };
            context.Request.Headers.Authorization = $"Bearer browser-a-exhausting-{attempt}";
            context.Request.Headers[SupportRateLimitPartitions.ForwardedClientIpHeader] = "198.51.100.31";
            context.Request.Headers[SupportRateLimitPartitions.InternalCallerSecretHeader] = "real-secret";
            SupportRateLimitPartitions.GuestPartition(context, internalCallerSecret: "real-secret");
        }

        var contextBrowserB = new DefaultHttpContext { Connection = { RemoteIpAddress = webHostConnectionAddress } };
        contextBrowserB.Request.Headers.Authorization = "Bearer browser-b-token";
        contextBrowserB.Request.Headers[SupportRateLimitPartitions.ForwardedClientIpHeader] = "198.51.100.32";
        contextBrowserB.Request.Headers[SupportRateLimitPartitions.InternalCallerSecretHeader] = "real-secret";

        var partitionB = SupportRateLimitPartitions.GuestPartition(contextBrowserB, internalCallerSecret: "real-secret");

        Assert.StartsWith("token:", partitionB.PartitionKey, StringComparison.Ordinal);
    }
}
