using ReadyToGoTravel.Booking.Checkout;
using ReadyToGoTravel.Booking.Payments;
using ReadyToGoTravel.Booking.Providers;

namespace ReadyToGoTravel.Booking.Tests;

public sealed class PaymentOrchestrationContractTests
{
    [Fact]
    public void PaymentBoundaryPersistsPolicyAndCarriesSettlementInstruction()
    {
        Assert.NotNull(typeof(CheckoutSession).GetProperty("PaymentPlan"));
        Assert.Contains(
            typeof(PaymentService).GetConstructors().Single().GetParameters(),
            parameter => parameter.ParameterType == typeof(ISupplierSettlementProvider));
        Assert.Contains(
            typeof(IPaymentService).GetMethods(),
            method => method.Name == "CreateSettlementAsync");
        Assert.NotNull(typeof(BookingCommand).GetProperty("SettlementInstruction"));
    }
}
