using ReadyToGoTravel.Support.Domain;

namespace ReadyToGoTravel.Support.Tests;

public sealed class SupportTicketDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NewTicketStartsInNewStatus()
    {
        var ticket = SupportTicket.Create(null, "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help", Now);

        Assert.Equal(SupportTicketStatus.WaitingOnSupport, ticket.Status);
        Assert.Single(ticket.Messages);
        Assert.Equal(SupportAuthorType.Guest, ticket.Messages[0].AuthorType);
    }

    [Fact]
    public void CustomerReplyMovesTicketToWaitingOnSupport()
    {
        var ticket = SupportTicket.Create("sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help", Now);
        ticket.Reply(SupportAuthorType.Support, "staff-1", "We are looking into it.", Now.AddMinutes(5));

        ticket.Reply(SupportAuthorType.Customer, "sub-1", "Any update?", Now.AddMinutes(10));

        Assert.Equal(SupportTicketStatus.WaitingOnSupport, ticket.Status);
    }

    [Fact]
    public void SupportReplyMovesTicketToWaitingOnCustomer()
    {
        var ticket = SupportTicket.Create("sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help", Now);

        ticket.Reply(SupportAuthorType.Support, "staff-1", "We are looking into it.", Now.AddMinutes(5));

        Assert.Equal(SupportTicketStatus.WaitingOnCustomer, ticket.Status);
    }

    [Fact]
    public void ClosingATicketAppendsASystemMessageAndSetsClosedAt()
    {
        var ticket = SupportTicket.Create("sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help", Now);

        ticket.Close("staff-1", Now.AddMinutes(5));

        Assert.Equal(SupportTicketStatus.Closed, ticket.Status);
        Assert.NotNull(ticket.ClosedAt);
        Assert.Equal(SupportAuthorType.System, ticket.Messages[^1].AuthorType);
    }

    [Fact]
    public void CustomerReplyToAClosedTicketReopensItAndPreservesTheClosureMessage()
    {
        var ticket = SupportTicket.Create("sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help", Now);
        ticket.Close("staff-1", Now.AddMinutes(5));
        var closureMessage = ticket.Messages[^1];

        ticket.Reply(SupportAuthorType.Customer, "sub-1", "Still need help.", Now.AddMinutes(10));

        Assert.Equal(SupportTicketStatus.WaitingOnSupport, ticket.Status);
        Assert.Contains(ticket.Messages, message => message.Id == closureMessage.Id && message.Body == "Ticket closed by support.");
        Assert.Equal(SupportAuthorType.System, ticket.Messages[^1].AuthorType);
        Assert.Equal("Ticket reopened.", ticket.Messages[^1].Body);
    }

    [Fact]
    public void SupportCannotReplyToAClosedTicket()
    {
        var ticket = SupportTicket.Create("sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help", Now);
        ticket.Close("staff-1", Now.AddMinutes(5));

        Assert.Throws<InvalidOperationException>(() =>
            ticket.Reply(SupportAuthorType.Support, "staff-1", "Too late.", Now.AddMinutes(10)));
    }

    [Fact]
    public void SequenceNumbersAreStrictlyIncreasing()
    {
        var ticket = SupportTicket.Create("sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help", Now);
        ticket.Reply(SupportAuthorType.Support, "staff-1", "Reply 1", Now.AddMinutes(1));
        ticket.Reply(SupportAuthorType.Customer, "sub-1", "Reply 2", Now.AddMinutes(2));

        Assert.Equal([1, 2, 3], ticket.Messages.Select(message => message.SequenceNumber));
    }

    [Theory]
    [InlineData(SupportTicketCategory.TravelWithin24Hours, true)]
    [InlineData(SupportTicketCategory.PaymentBookingMismatch, true)]
    [InlineData(SupportTicketCategory.SupplierCancellationOrRelocation, true)]
    [InlineData(SupportTicketCategory.TravellerSafety, true)]
    [InlineData(SupportTicketCategory.General, false)]
    [InlineData(SupportTicketCategory.AccountOrOther, false)]
    public void IsUrgentMatchesTheOperationalPolicyCategories(SupportTicketCategory category, bool expected)
    {
        var ticket = SupportTicket.Create("sub-1", "Ari", "ari@example.test", category, null, "Help", Now);

        Assert.Equal(expected, ticket.IsUrgent);
    }
}
