using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Booking.Bookings;
using ReadyToGoTravel.Booking.Persistence;
using ReadyToGoTravel.Consumer.Retention;

namespace ReadyToGoTravel.Booking.Retention;

/// <summary>
/// Booking's contribution to Consumer's account-closure evidence check: a customer has protected
/// evidence if any component booking left OfferSelected (payment/supplier engagement began),
/// consistent with the exact status set BookingRetentionSweepProcessor's abandoned-checkout rule
/// treats as "real evidence exists" (see AbandonableStatuses there).
/// </summary>
internal sealed class BookingConsumerRetentionEvidenceAdapter(BookingDbContext database) : IConsumerRetentionEvidencePort
{
    public Task<bool> HasProtectedEvidenceAsync(Guid customerId, string subject, CancellationToken cancellationToken = default) =>
        database.Checkouts
            .Where(checkout => checkout.CustomerId == customerId)
            .SelectMany(checkout => checkout.Components)
            .AnyAsync(component => component.Status != ComponentBookingStatus.OfferSelected, cancellationToken);
}
