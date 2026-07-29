using ReadyToGoTravel.Booking.Bookings;
using ReadyToGoTravel.Booking.Checkout;
using ReadyToGoTravel.Booking.Payments;

namespace ReadyToGoTravel.Booking.Tests;

public sealed class CheckoutDomainTests
{
    private static readonly TimeProvider Clock = new FixtureTimeProvider(
        new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void CheckoutRejectsTwoHotelComponents()
    {
        var result = CheckoutSession.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            [HotelOffer("hotel-1"), HotelOffer("hotel-2")],
            Travellers(),
            Clock);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_checkout_composition", result.ErrorCode);
    }

    [Fact]
    public void RepricingInvalidatesCustomerAcceptance()
    {
        var checkout = CreateAcceptedCheckout([HotelOffer("hotel-1")]);

        var result = checkout.ApplyResolvedOffers(
            [HotelOffer("hotel-1") with { MinimumTotal = 440m, Revision = "hotel-r2" }],
            Clock);

        Assert.True(result.IsSuccess);
        Assert.Equal(CheckoutStatus.AwaitingAcceptance, checkout.Status);
        Assert.Null(checkout.AcceptedRevision);
    }

    [Fact]
    public void CombinedJourneyIsCompleteOnlyWhenEveryComponentIsConfirmed()
    {
        var checkout = CreateBookingCheckout([HotelOffer("hotel-1"), FlightOffer("flight-1")]);

        checkout.RecordBookingResult(checkout.Components[0].Id, BookingProviderResult.Confirmed("hotel_123"), Clock);
        checkout.RecordBookingResult(checkout.Components[1].Id, BookingProviderResult.Pending("flight_456"), Clock);

        Assert.Equal(CheckoutStatus.BookingPending, checkout.Status);
    }

    [Fact]
    public void ConfirmedAndFailedCombinedComponentsRequireSupportWithoutChangingConfirmation()
    {
        var checkout = CreateBookingCheckout([HotelOffer("hotel-1"), FlightOffer("flight-1")]);

        checkout.RecordBookingResult(checkout.Components[0].Id, BookingProviderResult.Confirmed("hotel_123"), Clock);
        checkout.RecordBookingResult(checkout.Components[1].Id, BookingProviderResult.Failed("flight_unavailable"), Clock);

        Assert.Equal(CheckoutStatus.RequiresSupport, checkout.Status);
        Assert.Equal(ComponentBookingStatus.Confirmed, checkout.Components[0].Status);
        Assert.Equal(ComponentBookingStatus.RefundRequired, checkout.Components[1].Status);
    }

    [Fact]
    public void PaymentCannotBeginBeforeTheCurrentRevisionIsAccepted()
    {
        var checkout = CreateCheckout([HotelOffer("hotel-1")]);

        var result = checkout.BeginPayment("fixture", "payment_123", Clock);

        Assert.False(result.IsSuccess);
        Assert.Equal("checkout_not_ready_for_payment", result.ErrorCode);
    }

    private static CheckoutSession CreateAcceptedCheckout(IReadOnlyCollection<ResolvedCheckoutOffer> offers)
    {
        var checkout = CreateCheckout(offers);
        var acceptance = checkout.AcceptRevision(
            checkout.CurrentRevision.Number,
            checkout.CurrentRevision.Total,
            checkout.CurrentRevision.TransactionCurrency,
            checkout.CurrentRevision.TermsHash,
            "fixture-policy-v1",
            Clock);
        Assert.True(acceptance.IsSuccess);
        return checkout;
    }

    private static CheckoutSession CreateBookingCheckout(IReadOnlyCollection<ResolvedCheckoutOffer> offers)
    {
        var checkout = CreateAcceptedCheckout(offers);
        Assert.True(checkout.BeginPayment("fixture", "payment_123", Clock).IsSuccess);
        Assert.True(checkout.RecordPayment(PaymentProviderResult.Captured("return_123"), Clock).IsSuccess);
        Assert.True(checkout.BeginBooking(Clock).IsSuccess);
        return checkout;
    }

    private static CheckoutSession CreateCheckout(IReadOnlyCollection<ResolvedCheckoutOffer> offers)
    {
        var result = CheckoutSession.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), offers, Travellers(), Clock);
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private static ResolvedCheckoutOffer HotelOffer(string offerId) => new(
        CheckoutProduct.Hotel,
        offerId,
        "fixture-hotel",
        "Melbourne hotel",
        400m,
        "AUD",
        "terms-hotel-v1",
        "hotel-r1",
        Clock.GetUtcNow().AddHours(1),
        Clock.GetUtcNow());

    private static ResolvedCheckoutOffer FlightOffer(string offerId) => new(
        CheckoutProduct.Flight,
        offerId,
        "fixture-flight",
        "Melbourne flight",
        250m,
        "AUD",
        "terms-flight-v1",
        "flight-r1",
        Clock.GetUtcNow().AddHours(1),
        Clock.GetUtcNow());

    private static IReadOnlyCollection<TravellerSnapshot> Travellers() =>
    [new TravellerSnapshot(Guid.CreateVersion7(), "Ari", "Taylor", false, null)];

    private sealed class FixtureTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
