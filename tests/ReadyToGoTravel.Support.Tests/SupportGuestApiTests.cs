using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReadyToGoTravel.Support.Domain;
using ReadyToGoTravel.Support.Http;
using ReadyToGoTravel.Support.Persistence;

namespace ReadyToGoTravel.Support.Tests;

public sealed class SupportGuestApiTests
{
    [Fact]
    public async Task AValidGuestTokenResolvesAndReturnsOnlyThatTicketsThread()
    {
        await using var app = await TestApplication.CreateAsync();
        var (ticket, token) = await CreateGuestTicketAsync(app);

        var response = await GuestGetAsync(app, token, "/api/v1/support/guest/ticket");

        response.EnsureSuccessStatusCode();
        var body = await response.ReadAsAsync<TicketResponse>();
        Assert.Equal(ticket.Id, body!.Id);
    }

    [Fact]
    public async Task AGuestReplyLongerThanTheStoredLimitIsRejected()
    {
        await using var app = await TestApplication.CreateAsync();
        var (_, token) = await CreateGuestTicketAsync(app);

        var response = await GuestPostAsync(app, token, "/api/v1/support/guest/ticket/messages", new { body = new string('a', 4001) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AMissingAuthorizationHeaderReturnsUnauthorizedAndWritesAnAuthenticationFailedAuditEvent()
    {
        await using var app = await TestApplication.CreateAsync();

        var response = await app.Client.GetAsync("/api/v1/support/guest/ticket");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertAuthenticationFailedAuditEventWrittenAsync(app);
    }

    [Fact]
    public async Task AnEmptyAuthorizationHeaderReturnsUnauthorizedAndWritesAnAuthenticationFailedAuditEvent()
    {
        await using var app = await TestApplication.CreateAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/support/guest/ticket");
        request.Headers.TryAddWithoutValidation("Authorization", string.Empty);
        var response = await app.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertAuthenticationFailedAuditEventWrittenAsync(app);
    }

    [Fact]
    public async Task ANonBearerAuthorizationSchemeReturnsUnauthorizedAndWritesAnAuthenticationFailedAuditEventWithoutLoggingTheCredential()
    {
        await using var app = await TestApplication.CreateAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/support/guest/ticket");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", "dXNlcjpwYXNzd29yZA==");
        var response = await app.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertAuthenticationFailedAuditEventWrittenAsync(app);

        await using var scope = app.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
        // The non-Bearer credential must never be hashed, stored or otherwise treated as if it were
        // a guest token.
        Assert.DoesNotContain(
            await database.GuestAccessTokens.ToListAsync(),
            value => value.TokenHash == ReadyToGoTravel.Support.Guest.GuestAccessTokenGenerator.Hash("dXNlcjpwYXNzd29yZA=="));
    }

    [Fact]
    public async Task AMalformedBearerCredentialReturnsUnauthorizedAndWritesAnAuthenticationFailedAuditEvent()
    {
        await using var app = await TestApplication.CreateAsync();

        var response = await GuestGetAsync(app, "not base64url!!! and way too $hort", "/api/v1/support/guest/ticket");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertAuthenticationFailedAuditEventWrittenAsync(app);
    }

    [Fact]
    public async Task AnUnknownTokenReturnsUnauthorizedAndWritesAnAuthenticationFailedAuditEvent()
    {
        await using var app = await TestApplication.CreateAsync();

        var response = await GuestGetAsync(app, Convert.ToBase64String(new byte[32]), "/api/v1/support/guest/ticket");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertAuthenticationFailedAuditEventWrittenAsync(app);
    }

    [Fact]
    public async Task AValidTokenNeverWritesAnAuthenticationFailedAuditEventAndWritesAnAuthenticatedOne()
    {
        await using var app = await TestApplication.CreateAsync();
        var (_, token) = await CreateGuestTicketAsync(app);

        var response = await GuestGetAsync(app, token, "/api/v1/support/guest/ticket");

        response.EnsureSuccessStatusCode();
        await using var scope = app.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
        var events = await database.AuditEvents.ToListAsync();
        Assert.DoesNotContain(events, value => value.EventType == SupportAuditEventType.GuestLinkAuthenticationFailed);
        Assert.Contains(events, value => value.EventType == SupportAuditEventType.GuestLinkAuthenticated);
    }

    private static async Task AssertAuthenticationFailedAuditEventWrittenAsync(TestApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
        Assert.Contains(
            await database.AuditEvents.ToListAsync(),
            value => value.EventType == SupportAuditEventType.GuestLinkAuthenticationFailed);
    }

    [Fact]
    public async Task AnExpiredTokenReturnsUnauthorized()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero));
        await using var app = await TestApplication.CreateAsync(clock);
        var (_, token) = await CreateGuestTicketAsync(app);
        clock.Advance(TimeSpan.FromDays(31));

        var response = await GuestGetAsync(app, token, "/api/v1/support/guest/ticket");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ARevokedTokenReturnsUnauthorized()
    {
        await using var app = await TestApplication.CreateAsync();
        var (ticket, token) = await CreateGuestTicketAsync(app);
        app.SetStaffSubject("staff-1");
        (await app.Client.PostAsync($"/api/v1/support/staff/tickets/{ticket.Id}/guest-link/revoke", null))
            .EnsureSuccessStatusCode();
        app.ClearSubject();

        var response = await GuestGetAsync(app, token, "/api/v1/support/guest/ticket");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AMalformedTokenReturnsUnauthorizedWithoutThrowing()
    {
        await using var app = await TestApplication.CreateAsync();

        var response = await GuestGetAsync(app, "not-a-real-token", "/api/v1/support/guest/ticket");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OneGuestTicketsTokenCannotAccessAnotherGuestTicket()
    {
        await using var app = await TestApplication.CreateAsync();
        var (ticketA, tokenA) = await CreateGuestTicketAsync(app);
        var (ticketB, tokenB) = await CreateGuestTicketAsync(app);

        var responseA = await GuestGetAsync(app, tokenA, "/api/v1/support/guest/ticket");
        var bodyA = await responseA.ReadAsAsync<TicketResponse>();
        var responseB = await GuestGetAsync(app, tokenB, "/api/v1/support/guest/ticket");
        var bodyB = await responseB.ReadAsAsync<TicketResponse>();

        Assert.Equal(ticketA.Id, bodyA!.Id);
        Assert.Equal(ticketB.Id, bodyB!.Id);
        Assert.NotEqual(bodyA.Id, bodyB.Id);
    }

    [Fact]
    public async Task GuestRoutesExposeNoTicketIdParameterOnlyTheTokenSelectsTheTicket()
    {
        await using var app = await TestApplication.CreateAsync();
        var (_, token) = await CreateGuestTicketAsync(app);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/support/guest/ticket?ticketId=00000000-0000-0000-0000-000000000000");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await app.Client.SendAsync(request);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task AGuestReplyOnAClosedTicketReopensItAndClearsClosedAt()
    {
        await using var app = await TestApplication.CreateAsync();
        var (ticket, token) = await CreateGuestTicketAsync(app);
        app.SetStaffSubject("staff-1");
        var closeResponse = await app.Client.PostAsync($"/api/v1/support/staff/tickets/{ticket.Id}/close", null);
        closeResponse.EnsureSuccessStatusCode();
        var closed = await (await app.Client.GetAsync($"/api/v1/support/staff/tickets/{ticket.Id}")).ReadAsAsync<TicketResponse>();
        Assert.NotNull(closed!.ClosedAt);
        app.ClearSubject();

        var reply = await GuestPostAsync(app, token, "/api/v1/support/guest/ticket/messages", new { body = "Still need help." });

        reply.EnsureSuccessStatusCode();
        var body = await reply.ReadAsAsync<TicketResponse>();
        Assert.Equal("WaitingOnSupport", body!.Status);
        Assert.Null(body.ClosedAt);
    }

    [Fact]
    public async Task UploadingAndDownloadingAnAttachmentWorksThroughGuestRoutes()
    {
        await using var app = await TestApplication.CreateAsync();
        var (ticket, token) = await CreateGuestTicketAsync(app);

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent("hello from a guest"u8.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(fileContent, "file", "note.txt");
        form.Add(new StringContent(ticket.Messages[0].Id.ToString()), "messageId");

        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/support/guest/ticket/attachments")
        {
            Content = form,
        };
        uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var uploadResponse = await app.Client.SendAsync(uploadRequest);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        var attachment = await uploadResponse.ReadAsAsync<AttachmentResponse>();

        await app.DrainAttachmentScansAsync();
        var downloadResponse = await GuestGetAsync(app, token, $"/api/v1/support/guest/ticket/attachments/{attachment!.Id}/download");

        downloadResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task AGuestUploadIsAttributedToTheAuthenticatingTokenGeneration()
    {
        await using var app = await TestApplication.CreateAsync();
        var (ticket, token) = await CreateGuestTicketAsync(app);

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent("hello from a guest"u8.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(fileContent, "file", "note.txt");
        form.Add(new StringContent(ticket.Messages[0].Id.ToString()), "messageId");
        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/support/guest/ticket/attachments")
        {
            Content = form,
        };
        uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var uploadResponse = await app.Client.SendAsync(uploadRequest);

        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        var attachment = await uploadResponse.ReadAsAsync<AttachmentResponse>();
        await using var scope = app.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
        var stored = await database.Attachments.SingleAsync(value => value.Id == attachment!.Id);
        var activeToken = await database.GuestAccessTokens
            .SingleAsync(value => value.TicketId == ticket.Id && value.RevokedAt == null);
        Assert.Equal(activeToken.Id, stored.UploaderGuestTokenId);
    }

    private static async Task<HttpResponseMessage> GuestGetAsync(TestApplication app, string token, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await app.Client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> GuestPostAsync(TestApplication app, string token, string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await app.Client.SendAsync(request);
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
