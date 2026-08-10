using System.Net;
using System.Text;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Configuration;
using ReadyToGoTravel.Web.Authentication;
using ReadyToGoTravel.Web.Client;

namespace ReadyToGoTravel.Web.Tests;

public sealed class SupportApiClientTests
{
    private static readonly IConfiguration EmptyConfiguration = new ConfigurationBuilder().Build();

    [Fact]
    public async Task UploadAttachmentAsyncReturnsNullRatherThanThrowingWhenTheFileExceedsTheSizeLimit()
    {
        var handler = new RecordingHandler(HttpStatusCode.Created, "{}");
        var client = new SupportApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test") });
        var oversizedFile = new OversizedBrowserFile();

        var result = await client.UploadAttachmentAsync(Guid.NewGuid(), Guid.NewGuid(), oversizedFile);

        Assert.Null(result);
        Assert.Null(handler.RequestUri);
    }

    [Fact]
    public async Task CreateTicketAsyncPostsToTheSupportTicketsRoute()
    {
        var handler = new RecordingHandler(HttpStatusCode.Created, """
            {
              "id": "018f2b6a-1234-7abc-9def-0123456789ab",
              "contactName": "Ari",
              "contactEmail": "ari@example.test",
              "category": "General",
              "isUrgent": false,
              "bookingReference": null,
              "status": "WaitingOnSupport",
              "createdAt": "2026-08-09T00:00:00Z",
              "updatedAt": "2026-08-09T00:00:00Z",
              "closedAt": null,
              "messages": [],
              "attachments": []
            }
            """);
        var client = new SupportApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test") });

        var ticket = await client.CreateTicketAsync(new CreateSupportTicketRequest("Ari", "ari@example.test", "General", null, "Help please"));

        Assert.NotNull(ticket);
        Assert.Equal("/api/v1/support/tickets", handler.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Post, handler.Method);
    }

    [Fact]
    public async Task ANonSuccessResponseYieldsNullRatherThanThrowing()
    {
        var handler = new RecordingHandler(HttpStatusCode.BadRequest, """{"code":"support_category_invalid"}""");
        var client = new SupportApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test") });

        var ticket = await client.CreateTicketAsync(new CreateSupportTicketRequest("Ari", "ari@example.test", "Bogus", null, "Help please"));

        Assert.Null(ticket);
    }

    [Fact]
    public async Task TheGuestClientSendsTheRawTokenAsABearerHeaderRatherThanThePlatformAccessToken()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, """
            {
              "id": "018f2b6a-1234-7abc-9def-0123456789ab",
              "contactName": "Guest",
              "contactEmail": "guest@example.test",
              "category": "General",
              "isUrgent": false,
              "bookingReference": null,
              "status": "WaitingOnSupport",
              "createdAt": "2026-08-09T00:00:00Z",
              "updatedAt": "2026-08-09T00:00:00Z",
              "closedAt": null,
              "messages": [],
              "attachments": []
            }
            """);
        var client = new SupportGuestApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test") }, new GuestClientAddressAccessor(), EmptyConfiguration);

        await client.GetTicketAsync("raw-guest-token-value");

        Assert.Equal("/api/v1/support/guest/ticket", handler.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("raw-guest-token-value", handler.AuthorizationParameter);
    }

    [Fact]
    public async Task TheGuestClientForwardsTheCapturedBrowserAddressOnEveryCallNotJustTheFirst()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new("Support:InternalCallerSecret", "real-secret")])
            .Build();
        var clientAddress = new GuestClientAddressAccessor();
        // Simulates the address being captured once, during the HTTP request that establishes the
        // Blazor circuit - well before any of the interactive calls below, which have no
        // HttpContext of their own at all.
        clientAddress.Capture(System.Net.IPAddress.Parse("198.51.100.42"));

        var getHandler = new RecordingHandler(HttpStatusCode.OK, """
            {
              "id": "018f2b6a-1234-7abc-9def-0123456789ab",
              "contactName": "Guest",
              "contactEmail": "guest@example.test",
              "category": "General",
              "isUrgent": false,
              "bookingReference": null,
              "status": "WaitingOnSupport",
              "createdAt": "2026-08-09T00:00:00Z",
              "updatedAt": "2026-08-09T00:00:00Z",
              "closedAt": null,
              "messages": [],
              "attachments": []
            }
            """);
        var getClient = new SupportGuestApiClient(
            new HttpClient(getHandler) { BaseAddress = new Uri("https://api.example.test") }, clientAddress, configuration);
        await getClient.GetTicketAsync("raw-guest-token-value");
        Assert.Equal("198.51.100.42", getHandler.ForwardedClientIp);
        Assert.Equal("real-secret", getHandler.InternalCallerSecret);

        var replyHandler = new RecordingHandler(HttpStatusCode.OK, "{}");
        var replyClient = new SupportGuestApiClient(
            new HttpClient(replyHandler) { BaseAddress = new Uri("https://api.example.test") }, clientAddress, configuration);
        // Simulates an interactive circuit event (e.g. a form submit raised over the already
        // established SignalR connection) rather than the initial page request.
        await replyClient.ReplyAsync("raw-guest-token-value", "Still need help");
        Assert.Equal("198.51.100.42", replyHandler.ForwardedClientIp);
        Assert.Equal("real-secret", replyHandler.InternalCallerSecret);
    }

    [Fact]
    public async Task TheGuestClientOmitsForwardingHeadersWhenNoAddressHasBeenCaptured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new("Support:InternalCallerSecret", "real-secret")])
            .Build();
        var handler = new RecordingHandler(HttpStatusCode.OK, "{}");
        var client = new SupportGuestApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test") }, new GuestClientAddressAccessor(), configuration);

        await client.ReplyAsync("raw-guest-token-value", "Still need help");

        Assert.Null(handler.ForwardedClientIp);
        Assert.Null(handler.InternalCallerSecret);
    }

    [Fact]
    public async Task TheGuestClientsUploadAttachmentAsyncReturnsNullRatherThanThrowingWhenTheFileExceedsTheSizeLimit()
    {
        var handler = new RecordingHandler(HttpStatusCode.Created, "{}");
        var client = new SupportGuestApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test") }, new GuestClientAddressAccessor(), EmptyConfiguration);
        var oversizedFile = new OversizedBrowserFile();

        var result = await client.UploadAttachmentAsync("raw-guest-token-value", Guid.NewGuid(), oversizedFile);

        Assert.Null(result);
        Assert.Null(handler.RequestUri);
    }

    private sealed class OversizedBrowserFile : IBrowserFile
    {
        public string Name => "too-big.bin";
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public long Size => 100 * 1024 * 1024;
        public string ContentType => "application/octet-stream";

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            throw new IOException($"Supplied file with size {Size} bytes exceeds the maximum of {maxAllowedSize} bytes.");
    }

    private sealed class RecordingHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public HttpMethod? Method { get; private set; }

        public string? AuthorizationScheme { get; private set; }

        public string? AuthorizationParameter { get; private set; }

        public string? ForwardedClientIp { get; private set; }

        public string? InternalCallerSecret { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Method = request.Method;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            ForwardedClientIp = request.Headers.TryGetValues("X-Rtgt-Forwarded-Client-Ip", out var ipValues)
                ? ipValues.FirstOrDefault()
                : null;
            InternalCallerSecret = request.Headers.TryGetValues("X-Rtgt-Internal-Caller-Secret", out var secretValues)
                ? secretValues.FirstOrDefault()
                : null;
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            });
        }
    }
}
