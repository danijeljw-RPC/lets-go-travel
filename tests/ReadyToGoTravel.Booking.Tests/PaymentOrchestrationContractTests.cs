using ReadyToGoTravel.Booking.Payments;
using ReadyToGoTravel.Booking.Providers;

namespace ReadyToGoTravel.Booking.Tests;

public sealed class PaymentOrchestrationContractTests
{
    [Fact]
    public async Task PaymentPreparationReturnsTheServerPolicyAndForwardsTheStableOperationKey()
    {
        var customerProvider = new RecordingCustomerPaymentProvider();
        var settlementProvider = new RecordingSettlementProvider();
        var service = new PaymentService(customerProvider, settlementProvider);
        var requestedPlan = new CustomerPaymentPlan(420m, "AUD", "checkout-001");

        var preparation = await service.PrepareAsync(requestedPlan, "stable-payment-operation");

        Assert.Equal(requestedPlan, customerProvider.PreparedPlan);
        Assert.Equal("stable-payment-operation", customerProvider.OperationKey);
        Assert.Equal("hosted-payment", preparation.Plan.Provider);
        Assert.Equal("SupplierOrProviderManaged", preparation.Plan.MerchantModel);
        Assert.Equal("ProviderHostedCustomerPayment", preparation.Plan.CustomerPaymentRoute);
        Assert.Equal("OpaqueSupplierSettlementInstruction", preparation.Plan.SettlementRoute);
        Assert.Equal(420m, preparation.Plan.Amount);
        Assert.Equal("AUD", preparation.Plan.Currency);
        Assert.False(preparation.Plan.SeparateComponentCharges);
    }

    [Fact]
    public async Task SettlementCreationForwardsOnlyTheOpaqueSupplierInstructionInputs()
    {
        var settlementProvider = new RecordingSettlementProvider();
        var service = new PaymentService(new RecordingCustomerPaymentProvider(), settlementProvider);
        var policy = new PaymentPlan(
            "hosted-payment",
            "SupplierOrProviderManaged",
            "ProviderHostedCustomerPayment",
            "OpaqueSupplierSettlementInstruction",
            420m,
            "AUD",
            "HostedComponent",
            "SupplierOrProvider",
            false);

        var instruction = await service.CreateSettlementAsync(
            policy,
            "fixture-hotel",
            420m,
            "AUD",
            "pay-001");

        Assert.Equal(
            new SupplierSettlementPlan("fixture-hotel", 420m, "AUD", "pay-001"),
            settlementProvider.Plan);
        Assert.Equal("instruction-001", instruction.InstructionReference);
    }

    private sealed class RecordingCustomerPaymentProvider : ICustomerPaymentProvider
    {
        public CustomerPaymentPlan? PreparedPlan { get; private set; }

        public string? OperationKey { get; private set; }

        public Task<HostedPaymentPreparation> PrepareAsync(
            CustomerPaymentPlan plan,
            string operationKey,
            CancellationToken cancellationToken = default)
        {
            PreparedPlan = plan;
            OperationKey = operationKey;
            return Task.FromResult(new HostedPaymentPreparation(
                "pay-001",
                "browser-token",
                DateTimeOffset.UtcNow.AddMinutes(15),
                PaymentProviderStatus.ActionRequired));
        }

        public Task<CustomerPaymentStatusResult> RetrieveAsync(
            string paymentReference,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<CustomerPaymentStatusResult> CompleteReturnAsync(
            string paymentReference,
            string completionReference,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingSettlementProvider : ISupplierSettlementProvider
    {
        public SupplierSettlementPlan? Plan { get; private set; }

        public Task<SupplierSettlementInstruction> CreateInstructionAsync(
            SupplierSettlementPlan plan,
            CancellationToken cancellationToken = default)
        {
            Plan = plan;
            return Task.FromResult(new SupplierSettlementInstruction(
                "instruction-001",
                plan.ProviderBinding,
                plan.Amount,
                plan.Currency));
        }
    }
}
