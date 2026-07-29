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

    public static IdempotentResponse<TResponse> InProgress<TResponse>(int statusCode, TResponse value) =>
        new(IdempotencyOutcome.InProgress, statusCode, value);
}

public interface IIdempotencyService
{
    Task<IdempotentResponse<TResponse>> ExecuteAsync<TResponse>(
        Guid customerId,
        string operation,
        string key,
        string fingerprint,
        IdempotentResponse<TResponse> inProgressResponse,
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
        "pan",
        "primaryaccountnumber",
        "cardnumber",
        "cvv",
        "cvc",
        "sensitiveauthenticationdata",
        "identitydocument",
        "passport",
        "suppliercredential",
        "suppliercredentials",
        "apikey",
        "reusablepaymenttoken",
    ];

    public async Task<IdempotentResponse<TResponse>> ExecuteAsync<TResponse>(
        Guid customerId,
        string operation,
        string key,
        string fingerprint,
        IdempotentResponse<TResponse> inProgressResponse,
        Func<CancellationToken, Task<IdempotentResponse<TResponse>>> action,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        ArgumentNullException.ThrowIfNull(inProgressResponse);
        ArgumentNullException.ThrowIfNull(action);
        if (inProgressResponse.Outcome != IdempotencyOutcome.InProgress)
        {
            throw new ArgumentException("The durable response must be InProgress.", nameof(inProgressResponse));
        }

        var existing = await context.IdempotencyRecords.SingleOrDefaultAsync(value =>
            value.CustomerId == customerId &&
            value.Operation == operation &&
            value.Key == key,
            cancellationToken);

        if (existing is not null)
        {
            return Replay<TResponse>(existing, fingerprint);
        }

        var inProgressBody = SerializeSafeResponse(inProgressResponse.Value);
        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var record = IdempotencyRecord.Begin(
            customerId,
            operation,
            key,
            fingerprint,
            inProgressResponse.StatusCode,
            inProgressBody,
            now);
        context.IdempotencyRecords.Add(record);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsIdempotencyKeyRace(exception))
        {
            context.ChangeTracker.Clear();
            var winningRecord = await context.IdempotencyRecords.SingleAsync(value =>
                value.CustomerId == customerId &&
                value.Operation == operation &&
                value.Key == key,
                cancellationToken);
            return Replay<TResponse>(winningRecord, fingerprint);
        }

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
                if (IsForbiddenResponseField(normalizedName))
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

    private static bool IsForbiddenResponseField(string normalizedName) =>
        ForbiddenResponseFields.Any(forbidden =>
            normalizedName.Equals(forbidden, StringComparison.Ordinal) ||
            normalizedName.Contains(forbidden, StringComparison.Ordinal)) ||
        (normalizedName.Contains("card", StringComparison.Ordinal) &&
         normalizedName.Contains("number", StringComparison.Ordinal));

    private static bool IsIdempotencyKeyRace(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            var sqlState = current.GetType().GetProperty("SqlState")?.GetValue(current)?.ToString();
            var constraintName = current.GetType().GetProperty("ConstraintName")?.GetValue(current)?.ToString();
            if (string.Equals(sqlState, "23505", StringComparison.Ordinal) &&
                string.Equals(constraintName, "ux_idempotency_records_customer_operation_key", StringComparison.Ordinal))
            {
                return true;
            }

            var sqliteErrorCode = current.GetType().GetProperty("SqliteErrorCode")?.GetValue(current);
            if (sqliteErrorCode is int sqliteCode && sqliteCode == 19 &&
                current.Message.Contains("idempotency_records.customer_id", StringComparison.Ordinal) &&
                current.Message.Contains("idempotency_records.operation", StringComparison.Ordinal) &&
                current.Message.Contains("idempotency_records.key", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
