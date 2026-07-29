using ReadyToGoTravel.Booking.Bookings;
using ReadyToGoTravel.Booking.Checkout;
using ReadyToGoTravel.Booking.Payments;
using ReadyToGoTravel.Search.Checkout;

namespace ReadyToGoTravel.Booking.Providers;

public enum PaymentProviderStatus
{
    ActionRequired,
    Processing,
    Captured,
    Failed,
    OutcomeUnknown,
}

public sealed record CustomerPaymentPlan(decimal Amount, string Currency, string CheckoutReference);

public sealed record HostedPaymentPreparation(
    string PaymentReference,
    string BrowserToken,
    DateTimeOffset BrowserTokenExpiresAt,
    PaymentProviderStatus Status);

public sealed record CustomerPaymentStatusResult(
    string PaymentReference,
    PaymentProviderStatus Status,
    string? ProviderReturnReference,
    string? ErrorCode);

public interface ICustomerPaymentProvider
{
    Task<HostedPaymentPreparation> PrepareAsync(
        CustomerPaymentPlan plan,
        string returnKey,
        CancellationToken cancellationToken = default);

    Task<CustomerPaymentStatusResult> RetrieveAsync(
        string paymentReference,
        CancellationToken cancellationToken = default);
}

public sealed record SupplierSettlementPlan(
    string ProviderBinding,
    decimal Amount,
    string Currency,
    string PaymentReference);

public sealed record SupplierSettlementInstruction(
    string InstructionReference,
    string ProviderBinding,
    decimal Amount,
    string Currency);

public interface ISupplierSettlementProvider
{
    Task<SupplierSettlementInstruction> CreateInstructionAsync(
        SupplierSettlementPlan plan,
        CancellationToken cancellationToken = default);
}

public enum BookingProviderStatus
{
    Confirmed,
    Pending,
    Failed,
    Unknown,
}

public sealed record BookingCommand(
    CheckoutProduct Product,
    string OfferId,
    string ProviderBinding,
    string IdempotencyKey);

public sealed record BookingProviderExecutionResult(
    BookingProviderStatus Status,
    string? ExternalReference,
    string? ErrorCode);

public interface IBookingProvider
{
    Task<BookingProviderExecutionResult> BookAsync(
        BookingCommand command,
        CancellationToken cancellationToken = default);

    Task<BookingProviderExecutionResult> RetrieveAsync(
        string externalReference,
        CancellationToken cancellationToken = default);
}

internal static class CheckoutOfferMapper
{
    internal static ResolvedCheckoutOffer ToResolvedCheckoutOffer(CheckoutOfferResolutionResult resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        if (!resolution.IsSuccess || resolution.Value is null || string.IsNullOrWhiteSpace(resolution.ProviderBinding))
        {
            throw new InvalidOperationException("A successful checkout offer resolution with a provider binding is required.");
        }

        var offer = resolution.Value;
        return new ResolvedCheckoutOffer(
            offer.Product switch
            {
                CheckoutOfferProduct.Hotel => CheckoutProduct.Hotel,
                CheckoutOfferProduct.Flight => CheckoutProduct.Flight,
                _ => throw new InvalidOperationException("Unsupported checkout offer product."),
            },
            offer.OfferId,
            resolution.ProviderBinding,
            offer.ProductDetail,
            offer.MinimumTotal,
            offer.Currency,
            offer.TermsHash,
            offer.Revision,
            offer.ExpiresAt,
            offer.ResolvedAt);
    }

    internal static PaymentProviderResult ToDomainResult(CustomerPaymentStatusResult result) => result.Status switch
    {
        PaymentProviderStatus.ActionRequired => PaymentProviderResult.ActionRequired(result.ProviderReturnReference),
        PaymentProviderStatus.Processing => PaymentProviderResult.Processing(result.ProviderReturnReference),
        PaymentProviderStatus.Captured when result.ProviderReturnReference is not null =>
            PaymentProviderResult.Captured(result.ProviderReturnReference),
        PaymentProviderStatus.Captured => PaymentProviderResult.Captured(result.PaymentReference),
        PaymentProviderStatus.Failed => PaymentProviderResult.Failed(result.ErrorCode ?? "payment_failed"),
        PaymentProviderStatus.OutcomeUnknown => PaymentProviderResult.Unknown(result.ProviderReturnReference),
        _ => throw new InvalidOperationException("Unsupported customer payment status."),
    };

    internal static BookingProviderResult ToDomainResult(BookingProviderExecutionResult result) => result.Status switch
    {
        BookingProviderStatus.Confirmed when result.ExternalReference is not null =>
            BookingProviderResult.Confirmed(result.ExternalReference),
        BookingProviderStatus.Confirmed => throw new InvalidOperationException("Confirmed bookings require an external reference."),
        BookingProviderStatus.Pending => BookingProviderResult.Pending(result.ExternalReference),
        BookingProviderStatus.Failed => BookingProviderResult.Failed(result.ErrorCode ?? "booking_failed"),
        BookingProviderStatus.Unknown => BookingProviderResult.Unknown(result.ExternalReference),
        _ => throw new InvalidOperationException("Unsupported booking provider status."),
    };
}
