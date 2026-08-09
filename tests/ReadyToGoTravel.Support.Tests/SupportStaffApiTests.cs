using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ReadyToGoTravel.Support.Http;

namespace ReadyToGoTravel.Support.Tests;

public sealed class SupportStaffApiTests
{
    [Fact]
    public async Task ACallerWithoutTheSupportAgentRoleIsForbiddenFromEveryStaffRoute()
    {
        await using var app = await TestApplication.CreateAsync();
        app.SetSubject("sub-1");
        var ticket = await CreateTicketAsync(app);

        Assert.Equal(HttpStatusCode.Forbidden, (await app.Client.GetAsync("/api/v1/support/staff/tickets")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await app.Client.GetAsync($"/api/v1/support/staff/tickets/{ticket.Id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await app.Client.PostAsync($"/api/v1/support/staff/tickets/{ticket.Id}/close", null)).StatusCode);
    }

    [Fact]
    public async Task AStaffCallerCanViewAnyTicketRegardlessOfOwner()
    {
        await using var app = await TestApplication.CreateAsync();
        app.SetSubject("sub-1");
        var ticket = await CreateTicketAsync(app);

        app.SetStaffSubject("staff-1");
        var listResponse = await app.Client.GetAsync("/api/v1/support/staff/tickets");
        var detailResponse = await app.Client.GetAsync($"/api/v1/support/staff/tickets/{ticket.Id}");

        listResponse.EnsureSuccessStatusCode();
        detailResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task AStaffReplyMovesTheTicketToWaitingOnCustomer()
    {
        await using var app = await TestApplication.CreateAsync();
        app.SetSubject("sub-1");
        var ticket = await CreateTicketAsync(app);

        app.SetStaffSubject("staff-1");
        var response = await app.Client.PostAsJsonAsync(
            $"/api/v1/support/staff/tickets/{ticket.Id}/messages", new { body = "We are looking into it." });

        response.EnsureSuccessStatusCode();
        var body = await response.ReadAsAsync<TicketResponse>();
        Assert.Equal("WaitingOnCustomer", body!.Status);
    }

    [Fact]
    public async Task ClosingATicketSetsItToClosed()
    {
        await using var app = await TestApplication.CreateAsync();
        app.SetSubject("sub-1");
        var ticket = await CreateTicketAsync(app);

        app.SetStaffSubject("staff-1");
        var closeResponse = await app.Client.PostAsync($"/api/v1/support/staff/tickets/{ticket.Id}/close", null);
        Assert.Equal(HttpStatusCode.NoContent, closeResponse.StatusCode);

        var detail = await (await app.Client.GetAsync($"/api/v1/support/staff/tickets/{ticket.Id}")).ReadAsAsync<TicketResponse>();
        Assert.Equal("Closed", detail!.Status);
    }

    [Fact]
    public async Task RotatingTheGuestLinkReturnsNoRawTokenAndInvalidatesThePreviousOne()
    {
        await using var app = await TestApplication.CreateAsync();
        var (ticket, originalToken) = await CreateGuestTicketAsync(app);

        app.SetStaffSubject("staff-1");
        var rotateResponse = await app.Client.PostAsync($"/api/v1/support/staff/tickets/{ticket.Id}/guest-link/rotate", null);
        Assert.Equal(HttpStatusCode.NoContent, rotateResponse.StatusCode);
        var rotateBody = await rotateResponse.Content.ReadAsStringAsync();
        Assert.Empty(rotateBody);

        app.ClearSubject();
        using var oldTokenRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/support/guest/ticket");
        oldTokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", originalToken);
        var oldTokenResponse = await app.Client.SendAsync(oldTokenRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, oldTokenResponse.StatusCode);
    }

    [Fact]
    public async Task RevokingTheGuestLinkLeavesTheGuestRouteInaccessible()
    {
        await using var app = await TestApplication.CreateAsync();
        var (ticket, token) = await CreateGuestTicketAsync(app);

        app.SetStaffSubject("staff-1");
        (await app.Client.PostAsync($"/api/v1/support/staff/tickets/{ticket.Id}/guest-link/revoke", null))
            .EnsureSuccessStatusCode();

        app.ClearSubject();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/support/guest/ticket");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await app.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<TicketResponse> CreateTicketAsync(TestApplication app)
    {
        var response = await app.Client.PostAsJsonAsync("/api/v1/support/tickets", new
        {
            contactName = "Ari",
            category = "General",
            message = "Help please",
        });
        response.EnsureSuccessStatusCode();
        return (await response.ReadAsAsync<TicketResponse>())!;
    }

    private static async Task<(TicketResponse Ticket, string Token)> CreateGuestTicketAsync(TestApplication app)
    {
        var response = await app.Client.PostAsJsonAsync("/api/v1/support/tickets", new
        {
            contactName = "Guest",
            contactEmail = $"guest-{Guid.NewGuid():N}@example.test",
            category = "General",
            message = "Help please",
        });
        response.EnsureSuccessStatusCode();
        var ticket = (await response.ReadAsAsync<TicketResponse>())!;
        await app.DeliverPendingNotificationsAsync();
        var payload = app.Sender.Sent.Last(item => item.PayloadJson.Contains(ticket.Id.ToString(), StringComparison.Ordinal));
        using var document = JsonDocument.Parse(payload.PayloadJson);
        var token = document.RootElement.GetProperty("GuestToken").GetString()!;
        return (ticket, token);
    }
}
