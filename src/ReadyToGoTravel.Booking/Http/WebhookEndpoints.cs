using System.Buffers;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using ReadyToGoTravel.Booking.Webhooks;

namespace ReadyToGoTravel.Booking.Http;

public static class WebhookEndpoints
{
    public static RouteGroupBuilder MapWebhookEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/webhooks/liteapi/{environment}", AcceptLiteApiAsync)
            .RequireRateLimiting("webhook");
        return group;
    }

    private static async Task<IResult> AcceptLiteApiAsync(
        string environment,
        HttpContext context,
        IOptions<LiteApiWebhookOptions> optionsAccessor,
        IWebhookInboxWriter inbox,
        CancellationToken cancellationToken)
    {
        var options = optionsAccessor.Value;
        if (!options.Enabled)
        {
            return Results.NotFound();
        }

        if (!IsJson(context.Request.ContentType))
        {
            return Problem(context, StatusCodes.Status415UnsupportedMediaType, "webhook_content_type_invalid");
        }

        if (options.MaximumBodyBytes <= 0 ||
            context.Request.ContentLength > options.MaximumBodyBytes)
        {
            return Problem(context, StatusCodes.Status413PayloadTooLarge, "webhook_body_too_large");
        }

        var suppliedSecret = context.Request.Headers.Authorization.ToString();
        if (!SecretMatches(suppliedSecret, options.CurrentSecret) &&
            !SecretMatches(suppliedSecret, options.PreviousSecret))
        {
            return Problem(context, StatusCodes.Status401Unauthorized, "webhook_authentication_failed");
        }

        var body = await ReadBodyAsync(context.Request.Body, options.MaximumBodyBytes, cancellationToken);
        if (body is null)
        {
            return Problem(context, StatusCodes.Status413PayloadTooLarge, "webhook_body_too_large");
        }

        LiteApiWebhookEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<LiteApiWebhookEnvelope>(body);
        }
        catch (JsonException)
        {
            return Problem(context, StatusCodes.Status400BadRequest, "webhook_envelope_invalid");
        }

        if (envelope is null ||
            string.IsNullOrWhiteSpace(envelope.EventId) || envelope.EventId.Length > 255 ||
            string.IsNullOrWhiteSpace(envelope.EventName) || envelope.EventName.Length > 160 ||
            envelope.Request is null || envelope.Response is null)
        {
            return Problem(context, StatusCodes.Status400BadRequest, "webhook_envelope_invalid");
        }

        var normalizedEnvironment = environment switch
        {
            "sandbox" => "Sandbox",
            "production" => "Production",
            _ => null,
        };
        if (normalizedEnvironment is null ||
            !string.Equals(normalizedEnvironment, options.Environment, StringComparison.OrdinalIgnoreCase) ||
            envelope.Sandbox != string.Equals(normalizedEnvironment, "Sandbox", StringComparison.Ordinal))
        {
            return Problem(context, StatusCodes.Status400BadRequest, "webhook_environment_mismatch");
        }

        var outcome = await inbox.AcceptAsync(
            new WebhookEnvelopeInput(
                normalizedEnvironment,
                envelope.EventId,
                envelope.EventName,
                body,
                envelope.Sandbox,
                NormalizeCorrelationId(context.Request.Headers["X-Correlation-ID"].ToString(), context.TraceIdentifier)),
            cancellationToken);
        return outcome == WebhookAcceptanceOutcome.Conflict
            ? Problem(context, StatusCodes.Status409Conflict, "webhook_event_conflict")
            : Results.Accepted();
    }

    private static bool IsJson(string? contentType) =>
        MediaTypeHeaderValue.TryParse(contentType, out var parsed) &&
        (string.Equals(parsed.MediaType, "application/json", StringComparison.OrdinalIgnoreCase) ||
         parsed.MediaType?.EndsWith("+json", StringComparison.OrdinalIgnoreCase) == true);

    private static bool SecretMatches(string supplied, string? expected)
    {
        if (string.IsNullOrEmpty(supplied) || string.IsNullOrEmpty(expected))
        {
            return false;
        }

        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash);
    }

    private static async Task<string?> ReadBodyAsync(
        Stream body,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(Math.Min(maximumBytes + 1, 81920));
        try
        {
            using var output = new MemoryStream();
            while (true)
            {
                var read = await body.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                if (output.Length + read > maximumBytes)
                {
                    return null;
                }

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            return Encoding.UTF8.GetString(output.GetBuffer(), 0, checked((int)output.Length));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static string NormalizeCorrelationId(string supplied, string fallback) =>
        string.IsNullOrWhiteSpace(supplied) || supplied.Length > 128 ? fallback : supplied;

    private static IResult Problem(HttpContext context, int statusCode, string code) => Results.Problem(
        statusCode: statusCode,
        extensions: new Dictionary<string, object?>
        {
            ["code"] = code,
            ["correlationId"] = context.TraceIdentifier,
        });

    private sealed record LiteApiWebhookEnvelope(
        [property: JsonPropertyName("event_id")] string? EventId,
        [property: JsonPropertyName("event_name")] string? EventName,
        [property: JsonPropertyName("request")] string? Request,
        [property: JsonPropertyName("response")] string? Response,
        [property: JsonPropertyName("sandbox")] bool Sandbox);
}
