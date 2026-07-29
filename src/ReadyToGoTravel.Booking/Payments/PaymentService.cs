using ReadyToGoTravel.Booking.Providers;

namespace ReadyToGoTravel.Booking.Payments;

public interface IPaymentService
{
    Task<HostedPaymentPreparation> PrepareAsync(
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
}

public sealed class PaymentService(ICustomerPaymentProvider customerPaymentProvider) : IPaymentService
{
    public Task<HostedPaymentPreparation> PrepareAsync(
        CustomerPaymentPlan plan,
        string returnKey,
        CancellationToken cancellationToken = default) =>
        customerPaymentProvider.PrepareAsync(plan, returnKey, cancellationToken);

    public Task<CustomerPaymentStatusResult> VerifyReturnAsync(
        string paymentReference,
        string completionReference,
        CancellationToken cancellationToken = default) =>
        customerPaymentProvider.CompleteReturnAsync(paymentReference, completionReference, cancellationToken);

    public Task<CustomerPaymentStatusResult> RetrieveAsync(
        string paymentReference,
        CancellationToken cancellationToken = default) =>
        customerPaymentProvider.RetrieveAsync(paymentReference, cancellationToken);
}
