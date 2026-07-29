using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ReadyToGoTravel.Search.Capabilities;
using ReadyToGoTravel.Search.Http;

namespace ReadyToGoTravel.Search.Tests;

public sealed class SearchApiTests
{
    [Fact]
    public async Task CapabilitiesDescribeObservedCarriersWithoutProductionClaims()
    {
        await using var application = await TestApplication.CreateAsync(SearchEnvironment.Sandbox, true);

        var response = await application.Client.GetFromJsonAsync<CapabilityResponse[]>(
            "/api/v1/search/capabilities?pointOfSale=AU");

        Assert.NotNull(response);
        var flights = response
            .Where(capability => capability.Operation == "flightSearch")
            .OrderBy(capability => capability.CarrierCode)
            .ToArray();
        Assert.Equal(["JQ", "QF", "VA"], flights.Select(capability => capability.CarrierCode));
        Assert.All(flights, capability =>
        {
            Assert.True(capability.Enabled);
            Assert.False(capability.ProductionEnabled);
            Assert.Equal("observedSearchOnly", capability.EvidenceStatus);
        });
    }

    [Fact]
    public async Task HotelSearchReturnsMinimumTotalComponentsAndExpiry()
    {
        await using var application = await TestApplication.CreateAsync(SearchEnvironment.Sandbox, true);

        var response = await application.Client.PostAsJsonAsync(
            "/api/v1/search/hotels",
            new
            {
                destination = "Melbourne",
                checkIn = "2026-10-10",
                checkOut = "2026-10-12",
                adults = 2,
                childAges = Array.Empty<int>(),
                rooms = 1,
                requestedCurrency = "AUD",
                pointOfSale = "AU",
            });

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var offer = payload.RootElement.GetProperty("offers")[0];
        Assert.Equal(420m, offer.GetProperty("price").GetProperty("minimumTotal").GetDecimal());
        Assert.Equal(48m, offer.GetProperty("price").GetProperty("includedTaxes").GetDecimal());
        Assert.Equal("supplierReturned", offer.GetProperty("price").GetProperty("currencyProvenance").GetString());
        Assert.True(offer.GetProperty("requiresRevalidation").GetBoolean());
        Assert.NotEqual(default, offer.GetProperty("expiresAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task FlightSearchReturnsObservedFixtureOffers()
    {
        await using var application = await TestApplication.CreateAsync(SearchEnvironment.Sandbox, true);

        var response = await application.Client.PostAsJsonAsync(
            "/api/v1/search/flights",
            new
            {
                legs = new[] { new { origin = "SYD", destination = "MEL", departureDate = "2026-10-10" } },
                adults = 1,
                children = 0,
                infants = 0,
                cabinClass = "Economy",
                requestedCurrency = "AUD",
                pointOfSale = "AU",
            });

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var carriers = payload.RootElement.GetProperty("offers")
            .EnumerateArray()
            .Select(offer => offer.GetProperty("marketingCarrier").GetString()!)
            .Order()
            .ToArray();
        Assert.Equal(["JQ", "QF", "VA"], carriers);
    }

    [Fact]
    public async Task InvalidAirportReturnsStableProblemCode()
    {
        await using var application = await TestApplication.CreateAsync(SearchEnvironment.Sandbox, true);

        var response = await application.Client.PostAsJsonAsync(
            "/api/v1/search/flights",
            new
            {
                legs = new[] { new { origin = "Sydney", destination = "MEL", departureDate = "2026-10-10" } },
                adults = 1,
                children = 0,
                infants = 0,
                cabinClass = "Economy",
                requestedCurrency = "AUD",
                pointOfSale = "AU",
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("invalid_airport_code", problem!.Code);
        Assert.False(string.IsNullOrWhiteSpace(problem.CorrelationId));
    }

    [Fact]
    public async Task UnknownSearchJsonMembersAreRejected()
    {
        await using var application = await TestApplication.CreateAsync(SearchEnvironment.Sandbox, true);
        using var content = new StringContent(
            """{"destination":"Melbourne","checkIn":"2026-10-10","checkOut":"2026-10-12","adults":2,"childAges":[],"rooms":1,"requestedCurrency":"AUD","pointOfSale":"AU","supplier":"LiteAPI"}""",
            Encoding.UTF8,
            "application/json");

        var response = await application.Client.PostAsync("/api/v1/search/hotels", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProductionSearchFailsClosedWithoutProviderActivation()
    {
        await using var application = await TestApplication.CreateAsync(SearchEnvironment.Production, false);

        var response = await application.Client.PostAsJsonAsync(
            "/api/v1/search/hotels",
            new
            {
                destination = "Melbourne",
                checkIn = "2026-10-10",
                checkOut = "2026-10-12",
                adults = 2,
                childAges = Array.Empty<int>(),
                rooms = 1,
                requestedCurrency = "AUD",
                pointOfSale = "AU",
            });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("search_capability_unavailable", problem!.Code);
    }

    private sealed record CapabilityResponse(
        string Operation,
        string? CarrierCode,
        bool Enabled,
        bool ProductionEnabled,
        string EvidenceStatus);

    private sealed record ProblemResponse(string Code, string CorrelationId);

    private sealed class TestApplication : IAsyncDisposable
    {
        private readonly WebApplication application;

        private TestApplication(WebApplication application, HttpClient client)
        {
            this.application = application;
            Client = client;
        }

        public HttpClient Client { get; }

        public static async Task<TestApplication> CreateAsync(
            SearchEnvironment environment,
            bool enableFixtures)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Testing",
            });
            builder.WebHost.UseTestServer();
            builder.Services.ConfigureHttpJsonOptions(options =>
                options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
            builder.Services.AddProblemDetails(options =>
                options.CustomizeProblemDetails = context =>
                {
                    context.ProblemDetails.Extensions.TryAdd("code", "request_failed");
                    context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;
                });
            builder.Services.AddRateLimiter(options =>
                options.AddPolicy("search", context => RateLimitPartition.GetNoLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "test")));
            builder.Services.AddSearchModule(environment, enableFixtures);

            var application = builder.Build();
            application.UseExceptionHandler();
            application.UseRateLimiter();
            application.MapGroup("/api/v1").MapSearchEndpoints();
            await application.StartAsync();

            return new TestApplication(application, application.GetTestClient());
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await application.DisposeAsync();
        }
    }
}
