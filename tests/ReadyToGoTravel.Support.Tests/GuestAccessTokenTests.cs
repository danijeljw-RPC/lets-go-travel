using System.Security.Cryptography;
using System.Text;
using ReadyToGoTravel.Support.Domain;
using ReadyToGoTravel.Support.Guest;

namespace ReadyToGoTravel.Support.Tests;

public sealed class GuestAccessTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task IssueAsyncStoresOnlyTheHashOfTheRawToken()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticketId = await CreateTicketAsync(fixture);
        var service = new GuestAccessTokenService(fixture.Context, new FixedTimeProvider(Now));

        var rawToken = await service.IssueAsync(ticketId);

        var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
        var stored = fixture.Context.GuestAccessTokens.Single();
        Assert.Equal(expectedHash, stored.TokenHash);
        Assert.DoesNotContain(rawToken, stored.TokenHash);
    }

    [Fact]
    public async Task ResolveAsyncWithTheIssuedTokenReturnsTheTicketId()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticketId = await CreateTicketAsync(fixture);
        var service = new GuestAccessTokenService(fixture.Context, new FixedTimeProvider(Now));
        var rawToken = await service.IssueAsync(ticketId);

        var resolved = await service.ResolveAsync(rawToken);

        Assert.Equal(ticketId, resolved);
    }

    [Fact]
    public async Task ResolveAsyncWithAnUnknownTokenReturnsNull()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var service = new GuestAccessTokenService(fixture.Context, new FixedTimeProvider(Now));

        var resolved = await service.ResolveAsync("never-issued-token");

        Assert.Null(resolved);
    }

    [Fact]
    public async Task ResolveAsyncWithAnExpiredTokenReturnsNull()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticketId = await CreateTicketAsync(fixture);
        var clock = new MutableTimeProvider(Now);
        var service = new GuestAccessTokenService(fixture.Context, clock);
        var rawToken = await service.IssueAsync(ticketId);

        clock.Advance(TimeSpan.FromDays(30).Add(TimeSpan.FromSeconds(1)));

        Assert.Null(await service.ResolveAsync(rawToken));
    }

    [Fact]
    public async Task ResolveAsyncWithAStillValidTokenSucceedsOnRepeatedCalls()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticketId = await CreateTicketAsync(fixture);
        var service = new GuestAccessTokenService(fixture.Context, new FixedTimeProvider(Now));
        var rawToken = await service.IssueAsync(ticketId);

        Assert.Equal(ticketId, await service.ResolveAsync(rawToken));
        Assert.Equal(ticketId, await service.ResolveAsync(rawToken));
    }

    [Fact]
    public async Task RevokeAsyncThenResolveAsyncReturnsNull()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticketId = await CreateTicketAsync(fixture);
        var service = new GuestAccessTokenService(fixture.Context, new FixedTimeProvider(Now));
        var rawToken = await service.IssueAsync(ticketId);

        await service.RevokeAsync(ticketId);

        Assert.Null(await service.ResolveAsync(rawToken));
    }

    [Fact]
    public async Task RotateAsyncInvalidatesThePreviousTokenAndIssuesANewOneForTheSameTicket()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticketId = await CreateTicketAsync(fixture);
        var service = new GuestAccessTokenService(fixture.Context, new FixedTimeProvider(Now));
        var originalToken = await service.IssueAsync(ticketId);

        var rotatedToken = await service.RotateAsync(ticketId);

        Assert.Null(await service.ResolveAsync(originalToken));
        Assert.Equal(ticketId, await service.ResolveAsync(rotatedToken));
    }

    [Fact]
    public async Task ResolveAsyncWithAMalformedTokenReturnsNullWithoutThrowing()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var service = new GuestAccessTokenService(fixture.Context, new FixedTimeProvider(Now));

        Assert.Null(await service.ResolveAsync("not base64url!!! and way too $hort"));
        Assert.Null(await service.ResolveAsync(string.Empty));
    }

    [Fact]
    public async Task ATokenIssuedForOneTicketNeverResolvesToAnotherTicket()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticketA = await CreateTicketAsync(fixture, "sub-a");
        var ticketB = await CreateTicketAsync(fixture, "sub-b");
        var service = new GuestAccessTokenService(fixture.Context, new FixedTimeProvider(Now));

        var tokenForA = await service.IssueAsync(ticketA);
        var tokenForB = await service.IssueAsync(ticketB);

        Assert.Equal(ticketA, await service.ResolveAsync(tokenForA));
        Assert.Equal(ticketB, await service.ResolveAsync(tokenForB));
        Assert.NotEqual(ticketA, ticketB);
    }

    [Fact]
    public async Task EveryResolveCallWritesExactlyOneAuditEvent()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticketId = await CreateTicketAsync(fixture);
        var service = new GuestAccessTokenService(fixture.Context, new FixedTimeProvider(Now));
        var rawToken = await service.IssueAsync(ticketId);
        var eventsAfterIssue = fixture.Context.AuditEvents.Count();

        await service.ResolveAsync(rawToken);
        Assert.Equal(eventsAfterIssue + 1, fixture.Context.AuditEvents.Count());
        Assert.Equal(SupportAuditEventType.GuestLinkAuthenticated, fixture.Context.AuditEvents.ToList().OrderBy(e => e.CreatedAt).Last().EventType);

        await service.ResolveAsync("bogus");
        Assert.Equal(eventsAfterIssue + 2, fixture.Context.AuditEvents.Count());
        Assert.Equal(SupportAuditEventType.GuestLinkAuthenticationFailed, fixture.Context.AuditEvents.ToList().OrderBy(e => e.CreatedAt).Last().EventType);
    }

    private static async Task<Guid> CreateTicketAsync(SupportDatabaseFixture fixture, string subject = "sub-1")
    {
        var ticket = SupportTicket.Create(subject, "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help", Now);
        fixture.Context.Tickets.Add(ticket);
        await fixture.Context.SaveChangesAsync();
        return ticket.Id;
    }
}

internal sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset current = now;

    public void Advance(TimeSpan by) => current = current.Add(by);

    public override DateTimeOffset GetUtcNow() => current;
}
