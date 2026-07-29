using ReadyToGoTravel.Booking.Providers;

namespace ReadyToGoTravel.Booking.Payments;

public interface IPaymentService
{
    Task<PaymentPreparation> PrepareAsync(
        CustomerPaymentPlan plan,
        string returnKey,
        CancellationToken cancellationToken = default);

    Task<CustomerPaymentStatusResult> VerifyReturnAsync(
        string paymentReference,
        string completionReference,
        CancellationToken cancellationToken = default);

    Task<CustomerPaymentStatusResult> RetrieveAsync(
        string paymentReference,
        CancellationToken cancellationToken = default);

    Task<SupplierSettlementInstruction> CreateSettlementAsync(PaymentPlan plan, string providerBinding,
        decimal amount, string currency, string paymentReference, CancellationToken cancellationToken = default);
}

public sealed record PaymentPreparation(PaymentPlan Plan, HostedPaymentPreparation HostedSession);
public sealed record PaymentPlan(string Provider, string MerchantModel, string CustomerPaymentRoute,
    string SettlementRoute, decimal Amount, string Currency, string RequiredCustomerAction,
    string RefundOwner, bool SeparateComponentCharges);

public sealed class PaymentService(ICustomerPaymentProvider customerPaymentProvider,
    ISupplierSettlementProvider supplierSettlementProvider) : IPaymentService
{
    public async Task<PaymentPreparation> PrepareAsync(
        CustomerPaymentPlan plan,
        string returnKey,
        CancellationToken cancellationToken = default)
    {
        var policy = new PaymentPlan("hosted-payment", "SupplierOrProviderManaged",
            "ProviderHostedCustomerPayment", "OpaqueSupplierSettlementInstruction", plan.Amount, plan.Currency,
            "HostedComponent", "SupplierOrProvider", false);
        return new PaymentPreparation(policy, await customerPaymentProvider.PrepareAsync(plan, returnKey, cancellationToken));
    }

    public Task<CustomerPaymentStatusResult> VerifyReturnAsync(
        string paymentReference,
        string completionReference,
        CancellationToken cancellationToken = default) =>
        customerPaymentProvider.CompleteReturnAsync(paymentReference, completionReference, cancellationToken);

    public Task<CustomerPaymentStatusResult> RetrieveAsync(
        string paymentReference,
        CancellationToken cancellationToken = default) =>
        customerPaymentProvider.RetrieveAsync(paymentReference, cancellationToken);

    public Task<SupplierSettlementInstruction> CreateSettlementAsync(PaymentPlan plan, string providerBinding,
        decimal amount, string currency, string paymentReference, CancellationToken cancellationToken = default) =>
        supplierSettlementProvider.CreateInstructionAsync(
            new SupplierSettlementPlan(providerBinding, amount, currency, paymentReference), cancellationToken);
}
