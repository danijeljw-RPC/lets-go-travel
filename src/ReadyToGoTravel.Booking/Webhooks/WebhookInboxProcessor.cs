using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Booking.Persistence;
using ReadyToGoTravel.Booking.Reconciliation;

namespace ReadyToGoTravel.Booking.Webhooks;

public interface IWebhookInboxProcessor
{
    Task<bool> ProcessNextAsync(
        string workerId,
        CancellationToken cancellationToken = default);
}

internal sealed class WebhookInboxProcessor(
    BookingDbContext database,
    IReconciliationScheduler scheduler,
    TimeProvider timeProvider) : IWebhookInboxProcessor
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);
    private static readonly HashSet<string> ReconciliationEvents = new(StringComparer.Ordinal)
    {
        "booking.book",
        "booking.cancel",
        "booking.book.hotelConfirmationNumber",
        "booking.checkinInstruction",
        "booking.book_error",
        "booking.cancel_error",
        "booking.rebook.rfn",
        "booking.rebook.nrfn",
        "booking.amendment",
        "booking.amendment.relocation",
        "booking.refund",
        "booking.compensation",
        "flight.attachServices",
        "flight.book.created",
        "flight.book.pending.confirmation",
        "flight.book.confirmed",
        "flight.book.cancelled",
        "flight.book.failed",
        "flight.book.expired",
    };

    public async Task<bool> ProcessNextAsync(
        string workerId,
        CancellationToken cancellationToken = default)
    {
        var item = await ClaimNextAsync(workerId, cancellationToken);
        if (item is null)
        {
            return false;
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        if (!ReconciliationEvents.Contains(item.EventName))
        {
            AddOperationalCase(
                item,
                null,
                "UnsupportedWebhook",
                "unsupported_webhook_event",
                now);
            item.Complete(now);
            await database.SaveChangesAsync(cancellationToken);
            return true;
        }

        if (!TryGetNestedPayloads(item.RawBody, out var request, out var response))
        {
            item.Quarantine("webhook_nested_payload_invalid", now);
            await database.SaveChangesAsync(cancellationToken);
            return true;
        }

        var reference = FindBookingReference(response) ?? FindBookingReference(request);
        if (string.IsNullOrWhiteSpace(reference))
        {
            AddOperationalCase(item, null, "WebhookCorrelation", "webhook_booking_reference_missing", now);
            item.Complete(now);
            await database.SaveChangesAsync(cancellationToken);
            return true;
        }

        var component = await database.ComponentBookings.AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.ProviderBookingReference == reference,
                cancellationToken);
        if (component is null)
        {
            AddOperationalCase(item, null, "WebhookCorrelation", "webhook_booking_not_found", now);
            item.Complete(now);
            await database.SaveChangesAsync(cancellationToken);
            return true;
        }

        try
        {
            await scheduler.EnqueueImmediateAsync(
                component.Id,
                "Webhook",
                item.CorrelationId,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            var delayMinutes = Math.Min(60, Math.Pow(2, Math.Min(item.Attempts, 5)));
            item.Retry("reconciliation_enqueue_failed", now.AddMinutes(delayMinutes));
            await database.SaveChangesAsync(cancellationToken);
            return true;
        }

        item.Complete(now);
        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<WebhookInboxItem?> ClaimNextAsync(
        string workerId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var nowUtc = now.UtcDateTime;
        var id = await database.WebhookInbox.AsNoTracking()
            .Where(value =>
                value.NextAttemptAtUtc != null && value.NextAttemptAtUtc <= nowUtc &&
                (value.Status == WebhookInboxStatus.Pending ||
                 value.Status == WebhookInboxStatus.Retrying ||
                 value.Status == WebhookInboxStatus.Processing && value.LeaseExpiresAtUtc <= nowUtc))
            .OrderBy(value => value.NextAttemptAtUtc)
            .ThenBy(value => value.Id)
            .Select(value => (Guid?)value.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (!id.HasValue)
        {
            return null;
        }

        var claimed = await database.WebhookInbox
            .Where(value => value.Id == id &&
                value.NextAttemptAtUtc != null && value.NextAttemptAtUtc <= nowUtc &&
                (value.Status == WebhookInboxStatus.Pending ||
                 value.Status == WebhookInboxStatus.Retrying ||
                 value.Status == WebhookInboxStatus.Processing && value.LeaseExpiresAtUtc <= nowUtc))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.Status, WebhookInboxStatus.Processing)
                .SetProperty(value => value.LeaseOwner, workerId)
                .SetProperty(value => value.LeaseExpiresAtUtc, now.Add(LeaseDuration).UtcDateTime)
                .SetProperty(value => value.Attempts, value => value.Attempts + 1), cancellationToken);
        if (claimed == 0)
        {
            return null;
        }

        database.ChangeTracker.Clear();
        return await database.WebhookInbox.SingleAsync(value => value.Id == id, cancellationToken);
    }

    private void AddOperationalCase(
        WebhookInboxItem item,
        Guid? componentBookingId,
        string category,
        string reason,
        DateTimeOffset now)
    {
        database.OperationalCases.Add(new OperationalCase(
            Guid.CreateVersion7(now),
            componentBookingId,
            $"webhook:{item.Environment}:{item.EventId}:{reason}",
            category,
            reason,
            now));
    }

    private static bool TryGetNestedPayloads(
        string rawBody,
        out JsonElement request,
        out JsonElement response)
    {
        request = default;
        response = default;
        try
        {
            using var envelope = JsonDocument.Parse(rawBody);
            var requestJson = envelope.RootElement.GetProperty("request").GetString();
            var responseJson = envelope.RootElement.GetProperty("response").GetString();
            if (requestJson is null || responseJson is null)
            {
                return false;
            }

            using var requestDocument = JsonDocument.Parse(requestJson);
            using var responseDocument = JsonDocument.Parse(responseJson);
            request = requestDocument.RootElement.Clone();
            response = responseDocument.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
    }

    private static string? FindBookingReference(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String &&
                    property.Name is "booking_id" or "bookingId" or "booking_reference" or "bookingReference")
                {
                    return property.Value.GetString();
                }

                var nested = FindBookingReference(property.Value);
                if (nested is not null)
                {
                    return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindBookingReference(item);
                if (nested is not null)
                {
                    return nested;
                }
            }
        }

        return null;
    }
}
