using ReadyToGoTravel.Support.Domain;
using ReadyToGoTravel.Support.Retention;

namespace ReadyToGoTravel.Support.Tests;

public sealed class SupportConsumerRetentionEvidenceAdapterTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ASubjectWithATicketHasProtectedEvidence()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticket = SupportTicket.Create("sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help", Now);
        fixture.Context.Tickets.Add(ticket);
        await fixture.Context.SaveChangesAsync();
        var adapter = new SupportConsumerRetentionEvidenceAdapter(fixture.Context);

        var hasEvidence = await adapter.HasProtectedEvidenceAsync(Guid.CreateVersion7(Now), "sub-1");

        Assert.True(hasEvidence);
    }

    [Fact]
    public async Task ASubjectWithNoTicketsHasNoProtectedEvidence()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var adapter = new SupportConsumerRetentionEvidenceAdapter(fixture.Context);

        var hasEvidence = await adapter.HasProtectedEvidenceAsync(Guid.CreateVersion7(Now), "sub-1");

        Assert.False(hasEvidence);
    }

    [Fact]
    public async Task AnUnrelatedSubjectsTicketDoesNotCountAsEvidence()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticket = SupportTicket.Create("sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help", Now);
        fixture.Context.Tickets.Add(ticket);
        await fixture.Context.SaveChangesAsync();
        var adapter = new SupportConsumerRetentionEvidenceAdapter(fixture.Context);

        var hasEvidence = await adapter.HasProtectedEvidenceAsync(Guid.CreateVersion7(Now), "sub-2");

        Assert.False(hasEvidence);
    }
}
