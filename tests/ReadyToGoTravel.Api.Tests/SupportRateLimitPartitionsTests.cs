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
}
