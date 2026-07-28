using System.Net.Http.Json;

namespace ReadyToGoTravel.Web.Client;

public sealed partial class PlatformApiClient(HttpClient client, ILogger<PlatformApiClient> logger)
{
    public async Task<PlatformIdentity?> GetPlatformAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await client.GetFromJsonAsync<PlatformIdentity>(
                "/api/v1/platform",
                cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            LogUnavailable(exception);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The platform API is unavailable.")]
    private partial void LogUnavailable(Exception exception);
}

public sealed record PlatformIdentity(string Product, string Shortcode, string ApiVersion);
