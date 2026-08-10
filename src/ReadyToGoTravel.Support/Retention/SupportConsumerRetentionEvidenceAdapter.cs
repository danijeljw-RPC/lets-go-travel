using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Consumer.Retention;
using ReadyToGoTravel.Support.Persistence;

namespace ReadyToGoTravel.Support.Retention;

/// <summary>
/// Support's contribution to Consumer's account-closure evidence check: any ticket still linked to
/// the customer's subject (general tickets are fully deleted at 2 years, booking-related ones at
/// 7, by SupportRetentionSweepProcessor - so a remaining ticket is, by definition, still-retained
/// evidence) blocks profile minimisation.
/// </summary>
internal sealed class SupportConsumerRetentionEvidenceAdapter(SupportDbContext database) : IConsumerRetentionEvidencePort
{
    public Task<bool> HasProtectedEvidenceAsync(Guid customerId, string subject, CancellationToken cancellationToken = default) =>
        database.Tickets.AnyAsync(ticket => ticket.CustomerSubject == subject, cancellationToken);
}
