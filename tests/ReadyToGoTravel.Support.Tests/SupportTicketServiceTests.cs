using ReadyToGoTravel.Support.Application;
using ReadyToGoTravel.Support.Domain;

namespace ReadyToGoTravel.Support.Tests;

public sealed class SupportTicketServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateTicketAsyncPersistsTheTicketAndItsFirstMessageAtomically()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var service = new SupportTicketService(fixture.Context, new FixedTimeProvider(Now));

        var ticket = await service.CreateTicketAsync(new CreateSupportTicketCommand(
            "sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help please"));

        var stored = await service.GetForCustomerAsync("sub-1", ticket.Id);
        Assert.NotNull(stored);
        Assert.Single(stored!.Messages);
    }

    [Fact]
    public async Task GuestTicketHasNoCustomerSubject()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var service = new SupportTicketService(fixture.Context, new FixedTimeProvider(Now));

        var ticket = await service.CreateTicketAsync(new CreateSupportTicketCommand(
            null, "Guest", "guest@example.test", SupportTicketCategory.General, null, "Help please"));

        Assert.Null(ticket.CustomerSubject);
    }

    [Fact]
    public async Task GetForCustomerAsyncReturnsNullForAnotherCustomersTicket()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var service = new SupportTicketService(fixture.Context, new FixedTimeProvider(Now));

        var ticket = await service.CreateTicketAsync(new CreateSupportTicketCommand(
            "sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help please"));

        var result = await service.GetForCustomerAsync("sub-2", ticket.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task AddMessageAsyncThrowsForAnUnknownTicket()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var service = new SupportTicketService(fixture.Context, new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<TicketNotFoundException>(() =>
            service.AddMessageAsync(Guid.CreateVersion7(), SupportAuthorType.Support, "staff-1", "Hello", CancellationToken.None));
    }

    [Fact]
    public async Task CloseAsyncTransitionsTheTicketToClosed()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var service = new SupportTicketService(fixture.Context, new FixedTimeProvider(Now));
        var ticket = await service.CreateTicketAsync(new CreateSupportTicketCommand(
            "sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help please"));

        await service.CloseAsync(ticket.Id, "staff-1");

        var stored = await service.GetForCustomerAsync("sub-1", ticket.Id);
        Assert.Equal(SupportTicketStatus.Closed, stored!.Status);
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
