using System.Net;
using System.Text;
using ReadyToGoTravel.Web.Client;

namespace ReadyToGoTravel.Web.Tests;

public sealed class BookingClientTests
{
    [Fact]
    public async Task CreateCheckoutSendsAllowedIdentifiersAndIdempotencyKey()
    {
        var handler = new RecordingHttpMessageHandler();
        var client = CreateClient(handler);
        var tripId = Guid.NewGuid();
        var travellerId = Guid.NewGuid();

        await client.CreateCheckoutAsync(
            new CreateCheckoutInput(
                tripId,
                ["hotel-offer"],
                [new CheckoutTravellerInput(travellerId, 30)]),
            "checkout-key",
            default);

        Assert.Equal("/api/v1/checkouts", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal("checkout-key", handler.LastRequest.Headers.GetValues("Idempotency-Key").Single());
        Assert.Contains(tripId.ToString(), handler.LastBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(travellerId.ToString(), handler.LastBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hotel-offer", handler.LastBody, StringComparison.Ordinal);
        Assert.DoesNotContain("minimumTotal", handler.LastBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("provider", handler.LastBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("supplier", handler.LastBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PaymentReturnSendsOnlyOpaqueCompletionReference()
    {
        var handler = new RecordingHttpMessageHandler();
        var client = CreateClient(handler);
        var checkoutId = Guid.NewGuid();

        await client.ReturnPaymentAsync(checkoutId, "opaque-completion", "return-key", default);

        Assert.Equal($"/api/v1/checkouts/{checkoutId}/payment-return", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal("return-key", handler.LastRequest.Headers.GetValues("Idempotency-Key").Single());
        Assert.Contains("completionReference", handler.LastBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("opaque-completion", handler.LastBody, StringComparison.Ordinal);
        Assert.DoesNotContain("status", handler.LastBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AllCheckoutOperationsUsePublicVersionedRoutes()
    {
        var handler = new RecordingHttpMessageHandler();
        var client = CreateClient(handler);
        var checkoutId = Guid.NewGuid();

        await client.GetCheckoutAsync(checkoutId, default);
        await client.AcceptAsync(
            checkoutId,
            new CheckoutAcceptanceInput(1, 420m, "AUD", "terms-r1", "rtgt-v1"),
            "acceptance-key",
            default);
        await client.CreatePaymentSessionAsync(checkoutId, "payment-key", default);
        await client.BookAsync(checkoutId, "book-key", default);
        await client.RecoverAsync(checkoutId, default);

        Assert.Equal(
            [
                $"/api/v1/checkouts/{checkoutId}",
                $"/api/v1/checkouts/{checkoutId}/acceptance",
                $"/api/v1/checkouts/{checkoutId}/payment-session",
                $"/api/v1/checkouts/{checkoutId}/book",
                $"/api/v1/checkouts/{checkoutId}/recover",
            ],
            handler.Paths);
        Assert.Equal(
            "acceptance-key",
            handler.Requests[1].Headers.GetValues("Idempotency-Key").Single());
    }

    private static BookingApiClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test") });

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public string LastBody { get; private set; } = string.Empty;

        public List<string> Paths { get; } = [];

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            Requests.Add(request);
            LastBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Paths.Add(request.RequestUri!.AbsolutePath);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CheckoutJson, Encoding.UTF8, "application/json"),
            };
        }

        private const string CheckoutJson = """
            {
              "id": "00000000-0000-0000-0000-000000000001",
              "tripId": "00000000-0000-0000-0000-000000000002",
              "status": "AwaitingAcceptance",
              "currentRevision": {
                "number": 1,
                "total": 420.00,
                "currency": "AUD",
                "termsHash": "terms-r1",
                "expiresAt": "2026-10-01T00:00:00Z",
                "components": []
              },
              "acceptance": null,
              "travellers": [],
              "components": [],
              "payments": [],
              "paymentSession": null,
              "recoveryCaseCount": 0,
              "createdAt": "2026-09-01T00:00:00Z",
              "updatedAt": "2026-09-01T00:00:00Z",
              "expiresAt": "2026-10-01T00:00:00Z"
            }
            """;
    }
}
