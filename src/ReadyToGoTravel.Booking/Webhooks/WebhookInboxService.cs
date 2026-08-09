using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Booking.Persistence;

namespace ReadyToGoTravel.Booking.Webhooks;

public enum WebhookAcceptanceOutcome
{
    Accepted,
    Duplicate,
    Conflict,
}

public sealed record WebhookEnvelopeInput(
    string Environment,
    string EventId,
    string EventName,
    string RawBody,
    bool Sandbox,
    string CorrelationId);

public interface IWebhookInboxWriter
{
    Task<WebhookAcceptanceOutcome> AcceptAsync(
        WebhookEnvelopeInput input,
        CancellationToken cancellationToken = default);
}

internal sealed class WebhookInboxService(
    BookingDbContext database,
    TimeProvider timeProvider) : IWebhookInboxWriter
{
    public async Task<WebhookAcceptanceOutcome> AcceptAsync(
        WebhookEnvelopeInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input.RawBody)));
        var existing = await database.WebhookInbox.SingleOrDefaultAsync(value =>
            value.Provider == "LiteAPI" &&
            value.Environment == input.Environment &&
            value.EventId == input.EventId,
            cancellationToken);
        if (existing is not null)
        {
            if (string.Equals(existing.PayloadHash, hash, StringComparison.Ordinal))
            {
                return WebhookAcceptanceOutcome.Duplicate;
            }

            existing.Quarantine("event_identity_payload_conflict", timeProvider.GetUtcNow().ToUniversalTime());
            await database.SaveChangesAsync(cancellationToken);
            return WebhookAcceptanceOutcome.Conflict;
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        database.WebhookInbox.Add(new WebhookInboxItem(
            Guid.CreateVersion7(now),
            input.Environment,
            input.EventId,
            input.EventName,
            input.RawBody,
            hash,
            input.Sandbox,
            input.CorrelationId,
            now));
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return WebhookAcceptanceOutcome.Accepted;
        }
        catch (DbUpdateException)
        {
            database.ChangeTracker.Clear();
            existing = await database.WebhookInbox.SingleOrDefaultAsync(value =>
                value.Provider == "LiteAPI" &&
                value.Environment == input.Environment &&
                value.EventId == input.EventId,
                cancellationToken);
            if (existing is null)
            {
                throw;
            }

            if (string.Equals(existing.PayloadHash, hash, StringComparison.Ordinal))
            {
                return WebhookAcceptanceOutcome.Duplicate;
            }

            existing.Quarantine("event_identity_payload_conflict", now);
            await database.SaveChangesAsync(cancellationToken);
            return WebhookAcceptanceOutcome.Conflict;
        }
    }
}
