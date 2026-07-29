using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;

namespace ReadyToGoTravel.Web.Authentication;

internal sealed class ApiAccessTokenHandler(
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration) : DelegatingHandler
{
    private readonly Uri apiBaseUri = new(
        configuration["PlatformApi:BaseUrl"]
            ?? throw new InvalidOperationException("PlatformApi:BaseUrl is required."),
        UriKind.Absolute);

    internal static bool IsApiOrigin(Uri requestUri, Uri apiBaseUri) =>
        string.Equals(requestUri.Scheme, apiBaseUri.Scheme, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(requestUri.Host, apiBaseUri.Host, StringComparison.OrdinalIgnoreCase) &&
        requestUri.Port == apiBaseUri.Port;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri is not null &&
            IsApiOrigin(request.RequestUri, apiBaseUri) &&
            httpContextAccessor.HttpContext is { } context)
        {
            var accessToken = await context.GetTokenAsync("access_token");
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
