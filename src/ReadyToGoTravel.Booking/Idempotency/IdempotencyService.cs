using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Booking.Persistence;

namespace ReadyToGoTravel.Booking.Idempotency;

public enum IdempotencyOutcome
{
    Completed,
    Conflict,
    InProgress,
}

public sealed record IdempotentResponse<TResponse>(
    IdempotencyOutcome Outcome,
    int StatusCode,
    TResponse? Value);

public static class IdempotentResponse
{
    public static IdempotentResponse<TResponse> Completed<TResponse>(int statusCode, TResponse value) =>
        new(IdempotencyOutcome.Completed, statusCode, value);
}

public interface IIdempotencyService
{
    Task<IdempotentResponse<TResponse>> ExecuteAsync<TResponse>(
        Guid customerId,
        string operation,
        string key,
        string fingerprint,
        Func<CancellationToken, Task<IdempotentResponse<TResponse>>> action,
        CancellationToken cancellationToken);
}

public static class IdempotencyFingerprint
{
    public static string Create<TRequest>(TRequest request) => FromJson(JsonSerializer.Serialize(request));

    public static string FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using var document = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonicalJson(writer, document.RootElement);
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteCanonicalJson(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(value => value.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalJson(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                {
                    WriteCanonicalJson(writer, item);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(value.GetString());
                break;
            case JsonValueKind.Number when value.TryGetInt64(out var integer):
                writer.WriteNumberValue(integer);
                break;
            case JsonValueKind.Number when value.TryGetDecimal(out var decimalValue):
                writer.WriteNumberValue(decimalValue);
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(value.GetRawText());
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new JsonException($"Unsupported JSON value kind: {value.ValueKind}.");
        }
    }
}

internal sealed class IdempotencyService(BookingDbContext context, TimeProvider timeProvider) : IIdempotencyService
{
    private static readonly string[] ForbiddenResponseFields =
    [
        "accesstoken",
        "suppliercredential",
        "suppliercredentials",
        "reusablepaymenttoken",
    ];

    public async Task<IdempotentResponse<TResponse>> ExecuteAsync<TResponse>(
        Guid customerId,
        string operation,
        string key,
        string fingerprint,
        Func<CancellationToken, Task<IdempotentResponse<TResponse>>> action,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        ArgumentNullException.ThrowIfNull(action);

        var existing = await context.IdempotencyRecords.SingleOrDefaultAsync(value =>
            value.CustomerId == customerId &&
            value.Operation == operation &&
            value.Key == key,
            cancellationToken);

        if (existing is not null)
        {
            return Replay<TResponse>(existing, fingerprint);
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var record = IdempotencyRecord.Begin(customerId, operation, key, fingerprint, now);
        context.IdempotencyRecords.Add(record);
        await context.SaveChangesAsync(cancellationToken);

        var response = await action(cancellationToken);
        var responseBody = SerializeSafeResponse(response.Value);
        record.Complete(response.StatusCode, responseBody, timeProvider.GetUtcNow().ToUniversalTime());
        await context.SaveChangesAsync(cancellationToken);

        return response;
    }

    private static IdempotentResponse<TResponse> Replay<TResponse>(IdempotencyRecord record, string fingerprint)
    {
        if (!string.Equals(record.Fingerprint, fingerprint, StringComparison.Ordinal))
        {
            return new IdempotentResponse<TResponse>(IdempotencyOutcome.Conflict, 409, default);
        }

        var value = record.ResponseBody is null
            ? default
            : JsonSerializer.Deserialize<TResponse>(record.ResponseBody);
        var outcome = record.Status == IdempotencyRecordStatus.Completed
            ? IdempotencyOutcome.Completed
            : IdempotencyOutcome.InProgress;
        return new IdempotentResponse<TResponse>(outcome, record.ResponseStatusCode ?? 202, value);
    }

    private static string SerializeSafeResponse<TResponse>(TResponse? value)
    {
        var body = JsonSerializer.Serialize(value);
        using var document = JsonDocument.Parse(body);
        EnsureResponseContainsNoReusableSecrets(document.RootElement);
        return body;
    }

    private static void EnsureResponseContainsNoReusableSecrets(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
            {
                var normalizedName = new string(property.Name
                    .Where(char.IsLetterOrDigit)
                    .Select(char.ToLowerInvariant)
                    .ToArray());
                if (ForbiddenResponseFields.Contains(normalizedName, StringComparer.Ordinal))
                {
                    throw new InvalidOperationException("Idempotency responses cannot contain reusable credentials.");
                }

                EnsureResponseContainsNoReusableSecrets(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                EnsureResponseContainsNoReusableSecrets(item);
            }
        }
    }
}
