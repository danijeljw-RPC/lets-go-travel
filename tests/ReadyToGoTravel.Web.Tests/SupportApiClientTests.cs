using System.Net;
using System.Text;
using ReadyToGoTravel.Web.Client;

namespace ReadyToGoTravel.Web.Tests;

public sealed class SupportApiClientTests
{
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
        var client = new SupportGuestApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test") });

        await client.GetTicketAsync("raw-guest-token-value");

        Assert.Equal("/api/v1/support/guest/ticket", handler.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("raw-guest-token-value", handler.AuthorizationParameter);
    }

    private sealed class RecordingHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public HttpMethod? Method { get; private set; }

        public string? AuthorizationScheme { get; private set; }

        public string? AuthorizationParameter { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Method = request.Method;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            });
        }
    }
}
