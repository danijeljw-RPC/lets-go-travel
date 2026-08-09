using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Support.Domain;
using ReadyToGoTravel.Support.Guest;
using ReadyToGoTravel.Support.Persistence;

namespace ReadyToGoTravel.Support.Tests;

public sealed class GuestAccessTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task IssuingATokenStoresOnlyItsHash()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticketId = await CreateTicketAsync(fixture);
        var service = new GuestAccessTokenService(fixture.Context, new FixedTimeProvider(Now));

        var rawToken = await service.RotateAsync(ticketId);

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
        var rawToken = await service.RotateAsync(ticketId);

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
        var rawToken = await service.RotateAsync(ticketId);

        clock.Advance(TimeSpan.FromDays(30).Add(TimeSpan.FromSeconds(1)));

        Assert.Null(await service.ResolveAsync(rawToken));
    }

    [Fact]
    public async Task ResolveAsyncWithAStillValidTokenSucceedsOnRepeatedCalls()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticketId = await CreateTicketAsync(fixture);
        var service = new GuestAccessTokenService(fixture.Context, new FixedTimeProvider(Now));
        var rawToken = await service.RotateAsync(ticketId);

        Assert.Equal(ticketId, await service.ResolveAsync(rawToken));
        Assert.Equal(ticketId, await service.ResolveAsync(rawToken));
    }

    [Fact]
    public async Task RevokeAsyncThenResolveAsyncReturnsNull()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticketId = await CreateTicketAsync(fixture);
        var service = new GuestAccessTokenService(fixture.Context, new FixedTimeProvider(Now));
        var rawToken = await service.RotateAsync(ticketId);

        await service.RevokeAsync(ticketId);

        Assert.Null(await service.ResolveAsync(rawToken));
    }

    [Fact]
    public async Task RotateAsyncInvalidatesThePreviousTokenAndIssuesANewOneForTheSameTicket()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticketId = await CreateTicketAsync(fixture);
        var service = new GuestAccessTokenService(fixture.Context, new FixedTimeProvider(Now));
        var originalToken = await service.RotateAsync(ticketId);

        var rotatedToken = await service.RotateAsync(ticketId);

        Assert.Null(await service.ResolveAsync(originalToken));
        Assert.Equal(ticketId, await service.ResolveAsync(rotatedToken));
    }

    [Fact]
    public async Task RotatingAnExpiredButNeverRevokedTokenSucceedsAndTheReplacementWorks()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticketId = await CreateTicketAsync(fixture);
        var clock = new MutableTimeProvider(Now);
        var service = new GuestAccessTokenService(fixture.Context, clock);
        var originalToken = await service.RotateAsync(ticketId);
        clock.Advance(TimeSpan.FromDays(30).Add(TimeSpan.FromSeconds(1)));
        Assert.Null(await service.ResolveAsync(originalToken));

        var rotatedToken = await service.RotateAsync(ticketId);

        Assert.Equal(ticketId, await service.ResolveAsync(rotatedToken));
        var unrevokedCount = fixture.Context.GuestAccessTokens.Count(value => value.TicketId == ticketId && value.RevokedAt == null);
        Assert.Equal(1, unrevokedCount);
    }

    [Fact]
    public async Task RotatingTwiceAfterExpiryNeverThrowsAndEachReplacementSupersedesTheLast()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticketId = await CreateTicketAsync(fixture);
        var clock = new MutableTimeProvider(Now);
        var service = new GuestAccessTokenService(fixture.Context, clock);
        await service.RotateAsync(ticketId);
        clock.Advance(TimeSpan.FromDays(30).Add(TimeSpan.FromSeconds(1)));

        var first = await service.RotateAsync(ticketId);
        var second = await service.RotateAsync(ticketId);

        Assert.Null(await service.ResolveAsync(first));
        Assert.Equal(ticketId, await service.ResolveAsync(second));
    }

    [Fact]
    public async Task RotatingAfterAnExplicitRevokeSucceedsAndTheReplacementWorks()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticketId = await CreateTicketAsync(fixture);
        var service = new GuestAccessTokenService(fixture.Context, new FixedTimeProvider(Now));
        await service.RotateAsync(ticketId);
        await service.RevokeAsync(ticketId);

        var rotatedToken = await service.RotateAsync(ticketId);

        Assert.Equal(ticketId, await service.ResolveAsync(rotatedToken));
    }

    [Fact]
    public async Task RevokingAnExpiredButNeverRevokedTokenMarksItRevoked()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticketId = await CreateTicketAsync(fixture);
        var clock = new MutableTimeProvider(Now);
        var service = new GuestAccessTokenService(fixture.Context, clock);
        await service.RotateAsync(ticketId);
        clock.Advance(TimeSpan.FromDays(30).Add(TimeSpan.FromSeconds(1)));

        await service.RevokeAsync(ticketId);

        Assert.Empty(fixture.Context.GuestAccessTokens.Where(value => value.TicketId == ticketId && value.RevokedAt == null));
    }

    [Fact]
    public async Task StaffInitiatedRotationRecordsTheActingStaffSubjectOnTheAuditEvent()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticketId = await CreateTicketAsync(fixture);
        var service = new GuestAccessTokenService(fixture.Context, new FixedTimeProvider(Now));

        await service.RotateAsync(ticketId, actorSubject: "staff-42");

        var rotated = fixture.Context.AuditEvents.Single(value => value.EventType == SupportAuditEventType.GuestLinkRotated);
        Assert.Equal("staff-42", rotated.ActorSubject);
    }

    [Fact]
    public async Task StaffInitiatedRevocationRecordsTheActingStaffSubjectOnTheAuditEvent()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticketId = await CreateTicketAsync(fixture);
        var service = new GuestAccessTokenService(fixture.Context, new FixedTimeProvider(Now));
        await service.RotateAsync(ticketId);

        await service.RevokeAsync(ticketId, actorSubject: "staff-7");

        var revoked = fixture.Context.AuditEvents.Single(value => value.EventType == SupportAuditEventType.GuestLinkRevoked);
        Assert.Equal("staff-7", revoked.ActorSubject);
    }

    [Fact]
    public async Task SystemInitiatedRotationLeavesTheActorSubjectNullAndRemainsDistinguishableFromStaffAction()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticketId = await CreateTicketAsync(fixture);
        var clock = new MutableTimeProvider(Now);
        var service = new GuestAccessTokenService(fixture.Context, clock);

        await service.RotateAsync(ticketId);
        clock.Advance(TimeSpan.FromMinutes(1));
        await service.RotateAsync(ticketId, actorSubject: "staff-9");

        var events = fixture.Context.AuditEvents
            .Where(value => value.EventType == SupportAuditEventType.GuestLinkRotated)
            .ToList()
            .OrderBy(value => value.CreatedAt)
            .ToList();
        Assert.Equal(2, events.Count);
        Assert.Null(events[0].ActorSubject);
        Assert.Equal("staff-9", events[1].ActorSubject);
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

        var tokenForA = await service.RotateAsync(ticketA);
        var tokenForB = await service.RotateAsync(ticketB);

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
        var rawToken = await service.RotateAsync(ticketId);
        var eventsAfterIssue = fixture.Context.AuditEvents.Count();

        await service.ResolveAsync(rawToken);
        Assert.Equal(eventsAfterIssue + 1, fixture.Context.AuditEvents.Count());
        Assert.Equal(SupportAuditEventType.GuestLinkAuthenticated, fixture.Context.AuditEvents.ToList().OrderBy(e => e.CreatedAt).Last().EventType);

        await service.ResolveAsync("bogus");
        Assert.Equal(eventsAfterIssue + 2, fixture.Context.AuditEvents.Count());
        Assert.Equal(SupportAuditEventType.GuestLinkAuthenticationFailed, fixture.Context.AuditEvents.ToList().OrderBy(e => e.CreatedAt).Last().EventType);
    }

    [Fact]
    public async Task ConcurrentRotationsForTheSameTicketNeverLeaveMoreThanOneActiveToken()
    {
        var connectionString = $"Data Source=file:guest-token-race-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var keepAlive = new SqliteConnection(connectionString);
        await keepAlive.OpenAsync();

        await using (var setupContext = CreateContext(connectionString))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        Guid ticketId;
        await using (var setupContext = CreateContext(connectionString))
        {
            var ticket = SupportTicket.Create("sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help", Now);
            setupContext.Tickets.Add(ticket);
            await setupContext.SaveChangesAsync();
            ticketId = ticket.Id;
        }

        async Task<string> RotateWithFreshContextAsync()
        {
            await using var context = CreateContext(connectionString);
            var service = new GuestAccessTokenService(context, new FixedTimeProvider(Now));
            return await service.RotateAsync(ticketId);
        }

        var results = await Task.WhenAll(Task.Run(RotateWithFreshContextAsync), Task.Run(RotateWithFreshContextAsync));

        await using var verifyContext = CreateContext(connectionString);
        var tokens = await verifyContext.GuestAccessTokens
            .Where(value => value.TicketId == ticketId)
            .ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.Single(tokens, value => value.RevokedAt == null);
        Assert.Equal(2, results.Distinct().Count());
    }

    private static SupportDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(connectionString).Options);

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
