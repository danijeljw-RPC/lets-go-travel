namespace ReadyToGoTravel.Api.Endpoints;

internal static class PlatformEndpoints
{
    public static RouteGroupBuilder MapPlatformEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/platform", () => new PlatformResponse(
                Product: "readytogo.travel",
                Shortcode: "RTGT",
                ApiVersion: "v1"))
            .WithName("GetPlatform")
            .WithSummary("Returns the public product and API identity.");

        return group;
    }

    private sealed record PlatformResponse(string Product, string Shortcode, string ApiVersion);
}
