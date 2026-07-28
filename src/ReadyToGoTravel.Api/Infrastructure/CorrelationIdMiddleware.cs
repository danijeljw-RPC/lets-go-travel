using Microsoft.Extensions.Primitives;

namespace ReadyToGoTravel.Api.Infrastructure;

internal sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var requestedCorrelationId = context.Request.Headers[HeaderName].ToString();
        var correlationId = IsValid(requestedCorrelationId)
            ? requestedCorrelationId
            : Guid.CreateVersion7().ToString("N");

        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = new StringValues(correlationId);
            return Task.CompletedTask;
        });

        await next(context);
    }

    private static bool IsValid(string value) =>
        value.Length is > 0 and <= 128 && value.All(character => character is >= '!' and <= '~');
}
