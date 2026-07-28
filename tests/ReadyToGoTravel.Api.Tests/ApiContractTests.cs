using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using ReadyToGoTravel.Api;

namespace ReadyToGoTravel.Api.Tests;

public sealed class ApiContractTests(WebApplicationFactory<ApiAssemblyMarker> factory)
    : IClassFixture<WebApplicationFactory<ApiAssemblyMarker>>
{
    [Fact]
    public async Task PlatformEndpointUsesV1AndReturnsProductIdentity()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/platform", CancellationToken.None);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<PlatformResponse>(CancellationToken.None);
        Assert.NotNull(payload);
        Assert.Equal("readytogo.travel", payload.Product);
        Assert.Equal("RTGT", payload.Shortcode);
        Assert.Equal("v1", payload.ApiVersion);
    }

    [Fact]
    public async Task CallerCorrelationIdIsEchoed()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Correlation-ID", "test-correlation");

        var response = await client.SendAsync(request, CancellationToken.None);

        response.EnsureSuccessStatusCode();
        Assert.Equal("test-correlation", response.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Fact]
    public async Task UnknownApiRouteReturnsProblemDetailsWithCorrelationId()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/not-a-route", CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(CancellationToken.None);
        Assert.NotNull(problem);
        Assert.Equal("route_not_found", problem.Code);
        Assert.False(string.IsNullOrWhiteSpace(problem.CorrelationId));
    }

    private sealed record PlatformResponse(string Product, string Shortcode, string ApiVersion);

    private sealed record ProblemResponse(string Code, string CorrelationId);
}
