using Microsoft.AspNetCore.Http;

namespace ReadyToGoTravel.Search.Http;

internal static class SearchHttpResults
{
    public static IResult Problem(HttpContext context, int statusCode, string code, string title) =>
        Results.Problem(
            statusCode: statusCode,
            title: title,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["correlationId"] = context.TraceIdentifier,
            });
}
