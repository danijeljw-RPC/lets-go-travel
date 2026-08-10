using System.Net;
using System.Net.Http.Json;
using ReadyToGoTravel.Support.Http;

namespace ReadyToGoTravel.Support.Tests;

public sealed class SupportApiTests
{
    [Fact]
    public async Task AnAuthenticatedCustomerCreatingATicketIgnoresAClientSuppliedEmail()
    {
        await using var app = await TestApplication.CreateAsync();
        app.SetSubject("sub-1");

        var response = await app.Client.PostAsJsonAsync("/api/v1/support/tickets", new
        {
            contactName = "Ari",
            contactEmail = "attacker-supplied@example.test",
            category = "General",
            message = "Help please",
        });

        response.EnsureSuccessStatusCode();
        var ticket = await response.ReadAsAsync<TicketResponse>();
        Assert.Equal("sub-1@example.test", ticket!.ContactEmail);
    }

    [Fact]
    public async Task CreatingATicketWithAMessageLongerThanTheStoredLimitIsRejected()
    {
        await using var app = await TestApplication.CreateAsync();
        app.SetSubject("sub-1");

        var response = await app.Client.PostAsJsonAsync("/api/v1/support/tickets", new
        {
            contactName = "Ari",
            category = "General",
            message = new string('a', 4001),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReplyingWithAMessageLongerThanTheStoredLimitIsRejected()
    {
        await using var app = await TestApplication.CreateAsync();
        app.SetSubject("sub-1");
        var created = await CreateTicketAsync(app);

        var response = await app.Client.PostAsJsonAsync(
            $"/api/v1/support/tickets/{created.Id}/messages", new { body = new string('a', 4001) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnAnonymousVisitorCanCreateAGuestTicket()
    {
        await using var app = await TestApplication.CreateAsync();

        var response = await app.Client.PostAsJsonAsync("/api/v1/support/tickets", new
        {
            contactName = "Guest",
            contactEmail = "guest@example.test",
            category = "General",
            message = "Help please",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var ticket = await response.ReadAsAsync<TicketResponse>();
        Assert.Equal("WaitingOnSupport", ticket!.Status);

        await app.DeliverPendingNotificationsAsync();
        Assert.Single(app.Sender.Sent);
    }

    [Fact]
    public async Task AnAnonymousTicketCreationMissingAnEmailIsRejected()
    {
        await using var app = await TestApplication.CreateAsync();

        var response = await app.Client.PostAsJsonAsync("/api/v1/support/tickets", new
        {
            contactName = "Guest",
            category = "General",
            message = "Help please",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TheOwnerCanRetrieveTheirOwnTicket()
    {
        await using var app = await TestApplication.CreateAsync();
        app.SetSubject("sub-1");
        var created = await CreateTicketAsync(app);

        var response = await app.Client.GetAsync($"/api/v1/support/tickets/{created.Id}");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task AnotherCustomerCannotRetrieveSomeoneElsesTicket()
    {
        await using var app = await TestApplication.CreateAsync();
        app.SetSubject("sub-1");
        var created = await CreateTicketAsync(app);

        app.SetSubject("sub-2");
        var response = await app.Client.GetAsync($"/api/v1/support/tickets/{created.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ticket_not_found", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnonymousAccessToTheAuthenticatedTicketListIsRejected()
    {
        await using var app = await TestApplication.CreateAsync();

        var response = await app.Client.GetAsync("/api/v1/support/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AReplyFromTheOwnerTransitionsStatusAndAppendsAMessage()
    {
        await using var app = await TestApplication.CreateAsync();
        app.SetSubject("sub-1");
        var created = await CreateTicketAsync(app);

        var response = await app.Client.PostAsJsonAsync(
            $"/api/v1/support/tickets/{created.Id}/messages", new { body = "Any update?" });

        response.EnsureSuccessStatusCode();
        var ticket = await response.ReadAsAsync<TicketResponse>();
        Assert.Equal(2, ticket!.Messages.Count);
    }

    [Fact]
    public async Task ACustomerReplyOnAClosedTicketReopensItAndClearsClosedAt()
    {
        await using var app = await TestApplication.CreateAsync();
        app.SetSubject("sub-1");
        var created = await CreateTicketAsync(app);

        app.SetStaffSubject("staff-1");
        (await app.Client.PostAsync($"/api/v1/support/staff/tickets/{created.Id}/close", null)).EnsureSuccessStatusCode();
        var closed = await (await app.Client.GetAsync($"/api/v1/support/staff/tickets/{created.Id}")).ReadAsAsync<TicketResponse>();
        Assert.Equal("Closed", closed!.Status);
        Assert.NotNull(closed.ClosedAt);

        app.SetSubject("sub-1");
        var reply = await app.Client.PostAsJsonAsync(
            $"/api/v1/support/tickets/{created.Id}/messages", new { body = "Still need help." });

        reply.EnsureSuccessStatusCode();
        var reopened = await reply.ReadAsAsync<TicketResponse>();
        Assert.Equal("WaitingOnSupport", reopened!.Status);
        Assert.Null(reopened.ClosedAt);

        var closedMessage = reopened.Messages.Single(message => message.Body == "Ticket closed by support.");
        Assert.NotNull(closedMessage);
    }

    [Fact]
    public async Task ClosingAReopenedTicketAgainEstablishesANewClosedAtLaterThanTheFirst()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero));
        await using var app = await TestApplication.CreateAsync(clock);
        app.SetSubject("sub-1");
        var created = await CreateTicketAsync(app);

        app.SetStaffSubject("staff-1");
        (await app.Client.PostAsync($"/api/v1/support/staff/tickets/{created.Id}/close", null)).EnsureSuccessStatusCode();
        var firstClosed = await (await app.Client.GetAsync($"/api/v1/support/staff/tickets/{created.Id}")).ReadAsAsync<TicketResponse>();
        clock.Advance(TimeSpan.FromMinutes(10));

        app.SetSubject("sub-1");
        (await app.Client.PostAsJsonAsync($"/api/v1/support/tickets/{created.Id}/messages", new { body = "Reopening." }))
            .EnsureSuccessStatusCode();
        clock.Advance(TimeSpan.FromMinutes(10));

        app.SetStaffSubject("staff-1");
        (await app.Client.PostAsync($"/api/v1/support/staff/tickets/{created.Id}/close", null)).EnsureSuccessStatusCode();
        var secondClosed = await (await app.Client.GetAsync($"/api/v1/support/staff/tickets/{created.Id}")).ReadAsAsync<TicketResponse>();

        Assert.Equal("Closed", secondClosed!.Status);
        Assert.NotNull(secondClosed.ClosedAt);
        Assert.NotEqual(firstClosed!.ClosedAt, secondClosed.ClosedAt);
        Assert.True(secondClosed.ClosedAt > firstClosed.ClosedAt);
    }

    [Fact]
    public async Task TheTicketResponseNeverExposesInternalFields()
    {
        await using var app = await TestApplication.CreateAsync();
        app.SetSubject("sub-1");
        var created = await CreateTicketAsync(app);

        var response = await app.Client.GetAsync($"/api/v1/support/tickets/{created.Id}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("tokenHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("storageKey", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authorSubject", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sha256Checksum", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadingAndDownloadingAnAttachmentWorksEndToEndOnceScanned()
    {
        await using var app = await TestApplication.CreateAsync();
        app.SetSubject("sub-1");
        var created = await CreateTicketAsync(app);
        var messageId = created.Messages[0].Id;

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent("%PDF-1.7 test"u8.ToArray());
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "file", "receipt.pdf");
        form.Add(new StringContent(messageId.ToString()), "messageId");

        var uploadResponse = await app.Client.PostAsync($"/api/v1/support/tickets/{created.Id}/attachments", form);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        var attachment = await uploadResponse.ReadAsAsync<AttachmentResponse>();

        await app.DrainAttachmentScansAsync();
        var downloadResponse = await app.Client.GetAsync(
            $"/api/v1/support/tickets/{created.Id}/attachments/{attachment!.Id}/download");

        downloadResponse.EnsureSuccessStatusCode();
        var download = await downloadResponse.ReadAsAsync<DownloadUrlResponse>();
        Assert.Contains(attachment.Id.ToString(), download!.Url, StringComparison.Ordinal);
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
}
