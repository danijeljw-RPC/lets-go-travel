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
    public void CheckoutRejectsMixedCurrencyComponents()
    {
        var result = CheckoutSession.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            [HotelOffer("hotel-1"), FlightOffer("flight-1") with { Currency = "NZD" }],
            Travellers("hotel-1", "flight-1"),
            Clock);

        Assert.False(result.IsSuccess);
        Assert.Equal("checkout_currency_mismatch", result.ErrorCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CheckoutRejectsNonPositiveOfferTotals(decimal total)
    {
        var result = CheckoutSession.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            [HotelOffer("hotel-1") with { MinimumTotal = total }],
            Travellers(),
            Clock);

        Assert.False(result.IsSuccess);
        Assert.Equal("checkout_price_invalid", result.ErrorCode);
    }

    [Fact]
    public void CheckoutRejectsOfferTotalsThatWouldBeRoundedByPersistence()
    {
        var result = CheckoutSession.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            [HotelOffer("hotel-1") with { MinimumTotal = 400.001m }],
            Travellers(),
            Clock);

        Assert.False(result.IsSuccess);
        Assert.Equal("checkout_price_invalid", result.ErrorCode);
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
    public void RepricingRejectsAnInvalidOfferTotal()
    {
        var checkout = CreateAcceptedCheckout([HotelOffer("hotel-1")]);

        var result = checkout.ApplyResolvedOffers(
            [HotelOffer("hotel-1") with { MinimumTotal = -1m, Revision = "hotel-r2" }],
            Clock);

        Assert.False(result.IsSuccess);
        Assert.Equal("checkout_price_invalid", result.ErrorCode);
        Assert.Equal(1, checkout.CurrentRevision.Number);
    }

    [Fact]
    public void RepricingRejectsMixedCurrencyComponents()
    {
        var checkout = CreateAcceptedCheckout([HotelOffer("hotel-1"), FlightOffer("flight-1")]);

        var result = checkout.ApplyResolvedOffers(
            [HotelOffer("hotel-1"), FlightOffer("flight-1") with { Currency = "NZD", Revision = "flight-r2" }],
            Clock);

        Assert.False(result.IsSuccess);
        Assert.Equal("checkout_currency_mismatch", result.ErrorCode);
        Assert.Equal(1, checkout.CurrentRevision.Number);
    }

    [Theory]
    [InlineData(401, "AUD")]
    [InlineData(400, "NZD")]
    public void AcceptanceRejectsAClientTotalOrCurrencyMismatch(decimal total, string currency)
    {
        var checkout = CreateCheckout([HotelOffer("hotel-1")]);

        var result = checkout.AcceptRevision(
            checkout.CurrentRevision.Number,
            total,
            currency,
            checkout.CurrentRevision.TermsHash,
            "checkout-v1",
            Clock);

        Assert.False(result.IsSuccess);
        Assert.Equal("checkout_acceptance_mismatch", result.ErrorCode);
        Assert.Null(checkout.AcceptedRevision);
    }

    [Fact]
    public void AcceptanceRejectsAnUnknownCheckoutPolicyVersion()
    {
        var checkout = CreateCheckout([HotelOffer("hotel-1")]);

        var result = checkout.AcceptRevision(
            checkout.CurrentRevision.Number,
            checkout.CurrentRevision.Total,
            checkout.CurrentRevision.TransactionCurrency,
            checkout.CurrentRevision.TermsHash,
            "client-selected-policy",
            Clock);

        Assert.False(result.IsSuccess);
        Assert.Equal("checkout_acceptance_mismatch", result.ErrorCode);
        Assert.Null(checkout.AcceptedRevision);
    }

    [Fact]
    public void CurrentRevisionUsesTheHighestNumberWhenRevisionsAreMaterializedOutOfOrder()
    {
        var checkout = CreateAcceptedCheckout([HotelOffer("hotel-1")]);
        Assert.True(checkout.ApplyResolvedOffers(
            [HotelOffer("hotel-1") with { MinimumTotal = 440m, Revision = "hotel-r2" }],
            Clock).IsSuccess);
        var revisions = Assert.IsType<List<CheckoutRevision>>(checkout.Revisions);
        revisions.Reverse();

        Assert.Equal(2, checkout.CurrentRevision.Number);
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

    [Fact]
    public void CapturedPaymentRejectsAProcessingEvent()
    {
        var checkout = CreatePaymentCheckout();
        Assert.True(checkout.RecordPayment(PaymentProviderResult.Captured("return_123"), Clock).IsSuccess);

        var result = checkout.RecordPayment(PaymentProviderResult.Processing("return_123"), Clock);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_payment_transition", result.ErrorCode);
        Assert.Equal(PaymentStatus.Captured, checkout.PaymentAttempts.Single().Status);
    }

    [Fact]
    public void RepeatedUnchangedProcessingPaymentIsIdempotent()
    {
        var checkout = CreatePaymentCheckout();
        Assert.True(checkout.RecordPayment(PaymentProviderResult.Processing("return_123"), Clock).IsSuccess);

        var result = checkout.RecordPayment(PaymentProviderResult.Processing("return_123"), Clock);

        Assert.True(result.IsSuccess);
        Assert.Equal(CheckoutStatus.PaymentPending, checkout.Status);
        Assert.Equal(PaymentStatus.Processing, checkout.PaymentAttempts.Single().Status);
    }

    [Fact]
    public void CapturedPaymentRejectsAFailedEvent()
    {
        var checkout = CreatePaymentCheckout();
        Assert.True(checkout.RecordPayment(PaymentProviderResult.Captured("return_123"), Clock).IsSuccess);

        var result = checkout.RecordPayment(PaymentProviderResult.Failed("provider_failure"), Clock);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_payment_transition", result.ErrorCode);
        Assert.Equal(PaymentStatus.Captured, checkout.PaymentAttempts.Single().Status);
    }

    [Fact]
    public void PaymentRejectsAnUnknownProviderOutcome()
    {
        var checkout = CreatePaymentCheckout();

        var result = checkout.RecordPayment(
            new PaymentProviderResult((PaymentProviderOutcome)999, "return_123", null),
            Clock);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_payment_provider_outcome", result.ErrorCode);
        Assert.Equal(PaymentStatus.ActionRequired, checkout.PaymentAttempts.Single().Status);
    }

    [Fact]
    public void OfferIdChangeInvalidatesCustomerAcceptance()
    {
        AssertRepricingRequiresAcceptance(HotelOffer("hotel-1") with { OfferId = "hotel-2" });
    }

    [Fact]
    public void ProviderBindingChangeInvalidatesCustomerAcceptance()
    {
        AssertRepricingRequiresAcceptance(HotelOffer("hotel-1") with { ProviderBinding = "fixture-hotel-2" });
    }

    [Fact]
    public void ProviderRevisionChangeInvalidatesCustomerAcceptance()
    {
        AssertRepricingRequiresAcceptance(HotelOffer("hotel-1") with { Revision = "hotel-r2" });
    }

    [Fact]
    public void OfferExpiryChangeInvalidatesCustomerAcceptance()
    {
        AssertRepricingRequiresAcceptance(HotelOffer("hotel-1") with { ExpiresAt = Clock.GetUtcNow().AddMinutes(45) });
    }

    private static CheckoutSession CreateAcceptedCheckout(IReadOnlyCollection<ResolvedCheckoutOffer> offers)
    {
        var checkout = CreateCheckout(offers);
        var acceptance = checkout.AcceptRevision(
            checkout.CurrentRevision.Number,
            checkout.CurrentRevision.Total,
            checkout.CurrentRevision.TransactionCurrency,
            checkout.CurrentRevision.TermsHash,
            CheckoutAcceptancePolicy.CurrentVersion,
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

    private static CheckoutSession CreatePaymentCheckout()
    {
        var checkout = CreateAcceptedCheckout([HotelOffer("hotel-1")]);
        Assert.True(checkout.BeginPayment("fixture", "payment_123", Clock).IsSuccess);
        return checkout;
    }

    private static void AssertRepricingRequiresAcceptance(ResolvedCheckoutOffer replacement)
    {
        var checkout = CreateAcceptedCheckout([HotelOffer("hotel-1")]);

        var result = checkout.ApplyResolvedOffers([replacement], Clock);

        Assert.True(result.IsSuccess);
        Assert.Equal(CheckoutStatus.AwaitingAcceptance, checkout.Status);
        Assert.Null(checkout.AcceptedRevision);
    }

    private static CheckoutSession CreateCheckout(IReadOnlyCollection<ResolvedCheckoutOffer> offers)
    {
        var travellers = offers.Select(offer =>
            new TravellerSnapshot(offer.OfferId, Guid.CreateVersion7(), "Ari", "Taylor", false, null)).ToArray();
        var result = CheckoutSession.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), offers, travellers, Clock);
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

    private static TravellerSnapshot[] Travellers(params string[] offerIds) =>
        (offerIds.Length == 0 ? ["hotel-1"] : offerIds)
        .Select(offerId => new TravellerSnapshot(
            offerId,
            Guid.CreateVersion7(),
            "Ari",
            "Taylor",
            false,
            null))
        .ToArray();

    private sealed class FixtureTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
