using System.Net;
using System.Text;
using ReadyToGoTravel.Web.Client;

namespace ReadyToGoTravel.Web.Tests;

public sealed class SearchClientTests
{
    [Fact]
    public async Task HotelSearchUsesPlatformApiMinimumTotalWithoutClientCalculation()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, """
            {
              "searchId": "src_example",
              "environment": "sandbox",
              "searchedAt": "2026-07-29T00:00:00Z",
              "sandboxObservation": true,
              "offers": [
                {
                  "offerId": "off_example",
                  "propertyName": "Harbour Lane Hotel",
                  "destination": "Melbourne",
                  "roomName": "King studio",
                  "rateName": "Flexible stay",
                  "refundable": true,
                  "price": {
                    "minimumTotal": 420.00,
                    "returnedCurrency": "AUD",
                    "requestedCurrency": "AUD",
                    "currencyProvenance": "supplierReturned",
                    "baseAmount": 360.00,
                    "includedTaxes": 48.00,
                    "includedFees": 12.00
                  },
                  "expiresAt": "2026-07-29T00:20:00Z",
                  "requiresRevalidation": true
                }
              ]
            }
            """);
        var client = CreateClient(handler);

        var result = await client.SearchHotelsAsync(new HotelSearchInput(
            "Melbourne",
            new DateOnly(2026, 10, 10),
            new DateOnly(2026, 10, 12),
            2,
            [],
            1,
            "AUD",
            "AU"));

        Assert.True(result.Available);
        var offer = Assert.Single(result.Offers);
        Assert.Equal(420m, offer.Price.MinimumTotal);
        Assert.Equal(48m, offer.Price.IncludedTaxes);
        Assert.Equal(12m, offer.Price.IncludedFees);
        Assert.Equal("/api/v1/search/hotels", handler.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task CapabilityUnavailablePreservesStablePlatformCode()
    {
        var handler = new RecordingHandler(HttpStatusCode.ServiceUnavailable, """
            {
              "type": "about:blank",
              "title": "Search is not enabled for this environment and market.",
              "status": 503,
              "code": "search_capability_unavailable",
              "correlationId": "test-correlation"
            }
            """);
        var client = CreateClient(handler);

        var result = await client.SearchFlightsAsync(new FlightSearchInput(
            [new FlightLegInput("SYD", "MEL", new DateOnly(2026, 10, 10))],
            1,
            0,
            0,
            "Economy",
            "AUD",
            "AU"));

        Assert.False(result.Available);
        Assert.Equal("search_capability_unavailable", result.ErrorCode);
        Assert.Empty(result.Offers);
        Assert.Equal("/api/v1/search/flights", handler.RequestUri!.AbsolutePath);
    }

    private static SearchApiClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test") });

    private sealed class RecordingHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            });
        }
    }
}
