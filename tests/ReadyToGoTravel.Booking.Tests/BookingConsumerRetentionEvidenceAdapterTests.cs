using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Booking.Bookings;
using ReadyToGoTravel.Booking.Checkout;
using ReadyToGoTravel.Booking.Retention;

namespace ReadyToGoTravel.Booking.Tests;

public sealed class BookingConsumerRetentionEvidenceAdapterTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CustomerWithOnlyAnOfferSelectedComponentHasNoProtectedEvidence()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var customerId = Guid.CreateVersion7(Now);
        var checkout = CreateCheckout(customerId);
        fixture.Context.Checkouts.Add(checkout);
        await fixture.Context.SaveChangesAsync();
        var adapter = new BookingConsumerRetentionEvidenceAdapter(fixture.Context);

        var hasEvidence = await adapter.HasProtectedEvidenceAsync(customerId, "sub-1");

        Assert.False(hasEvidence);
    }

    [Fact]
    public async Task CustomerWithAComponentPastOfferSelectedHasProtectedEvidence()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var customerId = Guid.CreateVersion7(Now);
        var checkout = CreateCheckout(customerId);
        fixture.Context.Checkouts.Add(checkout);
        await fixture.Context.SaveChangesAsync();
        var component = checkout.Components.Single();
        await fixture.Context.ComponentBookings
            .Where(value => value.Id == component.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.Status, ComponentBookingStatus.Confirmed));
        var adapter = new BookingConsumerRetentionEvidenceAdapter(fixture.Context);

        var hasEvidence = await adapter.HasProtectedEvidenceAsync(customerId, "sub-1");

        Assert.True(hasEvidence);
    }

    [Fact]
    public async Task AnUnrelatedCustomersComponentDoesNotCountAsEvidence()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var otherCustomerId = Guid.CreateVersion7(Now);
        var checkout = CreateCheckout(otherCustomerId);
        fixture.Context.Checkouts.Add(checkout);
        await fixture.Context.SaveChangesAsync();
        var component = checkout.Components.Single();
        await fixture.Context.ComponentBookings
            .Where(value => value.Id == component.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.Status, ComponentBookingStatus.Confirmed));
        var adapter = new BookingConsumerRetentionEvidenceAdapter(fixture.Context);

        var hasEvidence = await adapter.HasProtectedEvidenceAsync(Guid.CreateVersion7(Now), "sub-2");

        Assert.False(hasEvidence);
    }

    private static CheckoutSession CreateCheckout(Guid customerId)
    {
        var result = CheckoutSession.Create(
            customerId,
            Guid.CreateVersion7(Now),
            [new ResolvedCheckoutOffer(
                CheckoutProduct.Hotel, "hotel-1", "fixture-hotel", "Harbour Lane Hotel", 420m, "AUD",
                "terms-hotel-v1", "hotel-r1", Now.AddHours(1), Now)],
            [new TravellerSnapshot("hotel-1", Guid.CreateVersion7(Now), "Ari", "Taylor", false, null)],
            new FixedTimeProvider(Now));
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
