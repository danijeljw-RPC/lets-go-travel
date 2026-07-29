using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReadyToGoTravel.Booking;
using ReadyToGoTravel.Booking.Bookings;
using ReadyToGoTravel.Booking.Checkout;
using ReadyToGoTravel.Booking.Payments;
using ReadyToGoTravel.Booking.Providers;
using ReadyToGoTravel.Booking.SupplierIntegrations.LiteApi;
using ReadyToGoTravel.Search.Capabilities;

namespace ReadyToGoTravel.Booking.Tests;

public sealed class LiteApiFixtureProviderTests
{
    [Fact]
    public void ProductionModuleRegistersNoFixtureProviders()
    {
        var services = new ServiceCollection();
        services.AddBookingModule(
            (_, options) => options.UseSqlite("Data Source=fixture-registration.db"));
        using var provider = services.BuildServiceProvider();

        Assert.Null(provider.GetService<ICustomerPaymentProvider>());
        Assert.Null(provider.GetService<IBookingProvider>());
        Assert.Null(provider.GetService<IPaymentService>());
    }

    [Fact]
    public void ProductionModuleRejectsFixtureRegistration()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddBookingModule(
            (_, options) => options.UseSqlite("Data Source=fixture-registration.db"),
            SearchEnvironment.Production,
            enableFixtures: true));

        Assert.Equal("Sanitized booking fixtures cannot be enabled in Production.", exception.Message);
    }

    [Fact]
    public async Task HostedPaymentPreparationReturnsOnlyOpaqueBrowserMaterial()
    {
        var paymentProvider = new LiteApiFixturePaymentProvider();
        var plan = new CustomerPaymentPlan(420m, "AUD", "checkout-001");

        var result = await paymentProvider.PrepareAsync(plan, "return-key", default);

        Assert.StartsWith("pay_", result.PaymentReference, StringComparison.Ordinal);
        Assert.StartsWith("browser_", result.BrowserToken, StringComparison.Ordinal);
        Assert.Equal(PaymentProviderStatus.ActionRequired, result.Status);
        Assert.DoesNotContain("card", JsonSerializer.Serialize(result), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PaymentSuccessAndBookingFailureRemainSeparate()
    {
        var paymentProvider = new LiteApiFixturePaymentProvider();
        var bookingProvider = new LiteApiFixtureBookingProvider();
        var flightCommand = new BookingCommand(
            CheckoutProduct.Flight,
            "off_fixture_flight",
            "booking-failed",
            "booking-key-001");

        var payment = await paymentProvider.RetrieveAsync("pay_captured_book_failed", default);
        var booking = await bookingProvider.BookAsync(flightCommand, default);

        Assert.Equal(PaymentProviderStatus.Captured, payment.Status);
        Assert.Equal(BookingProviderStatus.Failed, booking.Status);
    }

    [Fact]
    public async Task DuplicateBookingCommandReturnsTheSameOpaqueReference()
    {
        var bookingProvider = new LiteApiFixtureBookingProvider();
        var command = new BookingCommand(
            CheckoutProduct.Hotel,
            "off_fixture_hotel",
            "booking-confirmed",
            "booking-key-002");

        var first = await bookingProvider.BookAsync(command, default);
        var replay = await bookingProvider.BookAsync(command, default);

        Assert.Equal(first.ExternalReference, replay.ExternalReference);
        Assert.StartsWith("book_", first.ExternalReference, StringComparison.Ordinal);
    }
}
