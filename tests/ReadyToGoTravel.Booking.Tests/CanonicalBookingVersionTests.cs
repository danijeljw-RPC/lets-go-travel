using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Booking.Bookings;
using ReadyToGoTravel.Booking.Checkout;
using ReadyToGoTravel.Booking.Payments;
using ReadyToGoTravel.Booking.Reconciliation;

namespace ReadyToGoTravel.Booking.Tests;

public sealed class CanonicalBookingVersionTests
{
    private static readonly DateTimeOffset ObservedAt =
        new(2026, 8, 8, 4, 30, 0, TimeSpan.Zero);

    [Fact]
    public void ReorderedFlightSegmentsProduceTheSameCanonicalHash()
    {
        var component = CreateConfirmedComponent(CheckoutProduct.Flight, "flight_123");
        var first = FlightState(
            Segment("leg-2", "MEL", "ADL", ObservedAt.AddDays(4).AddHours(2)),
            Segment("leg-1", "SYD", "MEL", ObservedAt.AddDays(4)));
        var reordered = FlightState(first.FlightSegments.Reverse().ToArray());

        var firstVersion = CanonicalBookingVersioner.Create(
            component,
            first,
            null,
            ObservedAt,
            "Scheduled",
            "correlation-1");
        var secondVersion = CanonicalBookingVersioner.Create(
            component,
            reordered,
            null,
            ObservedAt.AddMinutes(1),
            "Webhook",
            "correlation-2");

        Assert.Equal(firstVersion.CanonicalHash, secondVersion.CanonicalHash);
        Assert.Equal(firstVersion.CanonicalSnapshotJson, secondVersion.CanonicalSnapshotJson);
    }

    [Fact]
    public void MeaningfulFlightTimeChangeProducesStableDiffAndAdvancesCurrentProjection()
    {
        var component = CreateConfirmedComponent(CheckoutProduct.Flight, "flight_123");
        var initial = CanonicalBookingVersioner.Create(
            component,
            FlightState(Segment("leg-1", "SYD", "MEL", ObservedAt.AddDays(4))),
            null,
            ObservedAt,
            "Initial",
            "correlation-1");
        Assert.True(component.ApplyReconciliationVersion(initial, ObservedAt));

        var changed = CanonicalBookingVersioner.Create(
            component,
            FlightState(Segment("leg-1", "SYD", "MEL", ObservedAt.AddDays(4).AddMinutes(45))),
            initial,
            ObservedAt.AddHours(1),
            "Scheduled",
            "correlation-2");

        Assert.NotEqual(initial.CanonicalHash, changed.CanonicalHash);
        Assert.Contains("scheduledDeparture", changed.DiffJson, StringComparison.Ordinal);
        Assert.True(component.ApplyReconciliationVersion(changed, ObservedAt.AddHours(1)));
        Assert.Equal(2, component.CurrentVersionNumber);
        Assert.Equal(changed.CanonicalHash, component.CurrentCanonicalHash);
        Assert.Equal(ObservedAt.AddDays(4).AddMinutes(45), component.NextDepartureAt);
    }

    [Fact]
    public void UnchangedStateDoesNotAdvanceCurrentProjection()
    {
        var component = CreateConfirmedComponent(CheckoutProduct.Hotel, "hotel_123");
        var state = HotelState();
        var initial = CanonicalBookingVersioner.Create(
            component,
            state,
            null,
            ObservedAt,
            "Initial",
            "correlation-1");
        Assert.True(component.ApplyReconciliationVersion(initial, ObservedAt));

        var duplicate = CanonicalBookingVersioner.Create(
            component,
            state,
            initial,
            ObservedAt.AddMinutes(5),
            "Webhook",
            "correlation-2");

        Assert.False(component.ApplyReconciliationVersion(duplicate, ObservedAt.AddMinutes(5)));
        Assert.Equal(1, component.CurrentVersionNumber);
        Assert.Equal(ObservedAt.AddMinutes(5), component.LastReconciledAt);
    }

    [Fact]
    public async Task PersistenceRejectsModifiedOrDeletedBookingVersions()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var checkout = CheckoutFactory.CreateWithTraveller("Ari", "Taylor");
        var component = checkout.Components.Single();
        var version = CanonicalBookingVersioner.Create(
            component,
            HotelState(),
            null,
            ObservedAt,
            "Initial",
            "correlation-1");
        fixture.Context.Checkouts.Add(checkout);
        fixture.Context.BookingVersions.Add(version);
        await fixture.Context.SaveChangesAsync();

        fixture.Context.Entry(version).Property(nameof(BookingVersion.Source)).CurrentValue = "Tampered";
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Context.SaveChangesAsync());

        fixture.Context.Entry(version).State = EntityState.Unchanged;
        fixture.Context.BookingVersions.Remove(version);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task CanonicalStateMayReturnToAnEarlierHashInANewSequentialVersion()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var checkout = CreateHotelCheckout();
        var component = checkout.Components.Single();
        var original = CanonicalBookingVersioner.Create(
            component,
            HotelState("Free cancellation until 2026-08-10"),
            null,
            ObservedAt,
            "Initial",
            "correlation-1");
        Assert.True(component.ApplyReconciliationVersion(original, ObservedAt));
        var changed = CanonicalBookingVersioner.Create(
            component,
            HotelState("Non-refundable"),
            original,
            ObservedAt.AddHours(1),
            "Scheduled",
            "correlation-2");
        Assert.True(component.ApplyReconciliationVersion(changed, ObservedAt.AddHours(1)));
        var restored = CanonicalBookingVersioner.Create(
            component,
            HotelState("Free cancellation until 2026-08-10"),
            changed,
            ObservedAt.AddHours(2),
            "Scheduled",
            "correlation-3");
        Assert.True(component.ApplyReconciliationVersion(restored, ObservedAt.AddHours(2)));
        fixture.Context.Checkouts.Add(checkout);
        fixture.Context.BookingVersions.AddRange(original, changed, restored);

        await fixture.Context.SaveChangesAsync();

        Assert.Equal(original.CanonicalHash, restored.CanonicalHash);
        var versionNumbers = await fixture.Context.BookingVersions
            .OrderBy(value => value.VersionNumber)
            .Select(value => value.VersionNumber)
            .ToArrayAsync();
        Assert.Equal([1, 2, 3], versionNumbers);
    }

    private static ComponentBooking CreateConfirmedComponent(CheckoutProduct product, string reference)
    {
        var checkout = product == CheckoutProduct.Hotel
            ? CreateHotelCheckout()
            : CreateFlightCheckout();
        var clock = new FixedTimeProvider(ObservedAt);
        Assert.True(checkout.AcceptRevision(
            checkout.CurrentRevision.Number,
            checkout.CurrentRevision.Total,
            checkout.CurrentRevision.TransactionCurrency,
            checkout.CurrentRevision.TermsHash,
            CheckoutAcceptancePolicy.CurrentVersion,
            clock).IsSuccess);
        Assert.True(checkout.BeginPayment("fixture", $"payment_{reference}", clock).IsSuccess);
        Assert.True(checkout.RecordPayment(PaymentProviderResult.Captured($"return_{reference}"), clock).IsSuccess);
        Assert.True(checkout.BeginBooking(clock).IsSuccess);
        var component = checkout.Components.Single();
        Assert.True(checkout.RecordBookingResult(component.Id, BookingProviderResult.Confirmed(reference), clock).IsSuccess);
        return component;
    }

    private static CheckoutSession CreateFlightCheckout()
    {
        var clock = new FixedTimeProvider(ObservedAt);
        var result = CheckoutSession.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            [new ResolvedCheckoutOffer(
                CheckoutProduct.Flight,
                "flight-1",
                "fixture-flight",
                "QF401 SYD-MEL",
                309.40m,
                "AUD",
                "terms-flight-v1",
                "flight-r1",
                ObservedAt.AddHours(1),
                ObservedAt)],
            [new TravellerSnapshot("flight-1", Guid.CreateVersion7(), "Ari", "Taylor", false, null)],
            clock);
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private static CheckoutSession CreateHotelCheckout()
    {
        var clock = new FixedTimeProvider(ObservedAt);
        var result = CheckoutSession.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            [new ResolvedCheckoutOffer(
                CheckoutProduct.Hotel,
                "hotel-1",
                "fixture-hotel",
                "Harbour Lane Hotel",
                420m,
                "AUD",
                "terms-hotel-v1",
                "hotel-r1",
                ObservedAt.AddHours(1),
                ObservedAt)],
            [new TravellerSnapshot("hotel-1", Guid.CreateVersion7(), "Ari", "Taylor", false, null)],
            clock);
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private static RetrievedBookingState HotelState(
        string cancellationPolicy = "Free cancellation until 2026-08-10") => new(
        CheckoutProduct.Hotel,
        RetrievedBookingStatus.Confirmed,
        "HTL-123",
        new RetrievedHotelStay(
            "Harbour Lane Hotel",
            new DateOnly(2026, 8, 12),
            new DateOnly(2026, 8, 14),
            "King studio",
            ["Breakfast"],
            cancellationPolicy),
        [],
        420m,
        "AUD",
        ObservedAt);

    private static RetrievedBookingState FlightState(params BookingFlightSegment[] segments) => new(
        CheckoutProduct.Flight,
        RetrievedBookingStatus.Confirmed,
        "FLT-123",
        null,
        segments,
        309.40m,
        "AUD",
        ObservedAt);

    private static BookingFlightSegment Segment(
        string id,
        string origin,
        string destination,
        DateTimeOffset departure) => new(
        id,
        "QF",
        id == "leg-1" ? "401" : "695",
        origin,
        destination,
        departure,
        departure.AddHours(1).AddMinutes(25));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
