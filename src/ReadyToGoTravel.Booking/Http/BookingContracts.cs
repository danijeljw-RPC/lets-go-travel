using ReadyToGoTravel.Booking.Checkout;

namespace ReadyToGoTravel.Booking.Http;

internal sealed record CreateCheckoutRequest(
    Guid TripId,
    IReadOnlyList<string> OfferIds,
    IReadOnlyList<CheckoutTravellerAssignmentRequest> TravellerAssignments);

internal sealed record CheckoutTravellerAssignmentRequest(Guid TravellerId, int? AgeAtTravel);

internal sealed record AcceptCheckoutRequest(
    int RevisionNumber,
    decimal AcceptedTotal,
    string Currency,
    string TermsHash,
    string PolicyVersion);

internal sealed record EmptyCheckoutCommandRequest();

internal sealed record PaymentReturnRequest(string CompletionReference);

internal sealed record CheckoutResponse(
    Guid Id,
    Guid TripId,
    string Status,
    CheckoutRevisionResponse CurrentRevision,
    CheckoutAcceptanceResponse? Acceptance,
    IReadOnlyList<CheckoutTravellerResponse> Travellers,
    IReadOnlyList<CheckoutComponentResponse> Components,
    IReadOnlyList<CheckoutPaymentResponse> Payments,
    HostedPaymentSessionResponse? PaymentSession,
    int RecoveryCaseCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset ExpiresAt,
    int? RetryAfterSeconds = null)
{
    internal static CheckoutResponse CreationPending(Guid tripId) => new(
        Guid.Empty,
        tripId,
        "CreationPending",
        new CheckoutRevisionResponse(0, 0, string.Empty, string.Empty, default, []),
        null,
        [],
        [],
        [],
        null,
        0,
        default,
        default,
        default);
}

internal sealed record CheckoutRevisionResponse(
    int Number,
    decimal Total,
    string Currency,
    string TermsHash,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<CheckoutRevisionComponentResponse> Components);

internal sealed record CheckoutRevisionComponentResponse(
    string Product,
    string OfferId,
    string ProductDetail,
    decimal MinimumTotal,
    string Currency);

internal sealed record CheckoutAcceptanceResponse(
    int RevisionNumber,
    decimal AcceptedTotal,
    string Currency,
    string TermsHash,
    string PolicyVersion,
    DateTimeOffset AcceptedAt);

internal sealed record CheckoutTravellerResponse(
    Guid TravellerId,
    string GivenName,
    string FamilyName,
    bool IsMinor,
    DateTimeOffset? GuardianAuthorityConfirmedAt,
    int? AgeAtTravel);

internal sealed record CheckoutComponentResponse(
    Guid Id,
    string Product,
    string OfferId,
    string Status,
    string? FailureCode);

internal sealed record CheckoutPaymentResponse(
    Guid Id,
    decimal Amount,
    string Currency,
    string Status,
    string? FailureCode);

internal sealed record HostedPaymentSessionResponse(
    string PaymentReference,
    string BrowserToken,
    DateTimeOffset BrowserTokenExpiresAt);

internal static class CheckoutResponseMapper
{
    internal static CheckoutResponse Map(
        CheckoutSession checkout,
        HostedPaymentSessionResponse? paymentSession = null,
        int? retryAfterSeconds = null)
    {
        var revision = checkout.CurrentRevision;
        return new CheckoutResponse(
            checkout.Id,
            checkout.TripId,
            checkout.Status.ToString(),
            new CheckoutRevisionResponse(
                revision.Number,
                revision.Total,
                revision.TransactionCurrency,
                revision.TermsHash,
                revision.ExpiresAt,
                revision.Components.Select(component => new CheckoutRevisionComponentResponse(
                    component.Product.ToString(),
                    component.OfferId,
                    component.ProductDetail,
                    component.MinimumTotal,
                    revision.TransactionCurrency)).ToArray()),
            checkout.AcceptedRevision is null
                ? null
                : new CheckoutAcceptanceResponse(
                    checkout.AcceptedRevision.RevisionNumber,
                    checkout.AcceptedRevision.AcceptedTotal,
                    checkout.AcceptedRevision.Currency,
                    checkout.AcceptedRevision.TermsHash,
                    checkout.AcceptedRevision.PolicyVersion,
                    checkout.AcceptedRevision.AcceptedAt),
            checkout.TravellerSnapshots.Select(traveller => new CheckoutTravellerResponse(
                traveller.TravellerId,
                traveller.GivenName,
                traveller.FamilyName,
                traveller.IsMinor,
                traveller.GuardianAuthorityConfirmedAt,
                traveller.AgeAtTravel)).ToArray(),
            checkout.Components.Select(component => new CheckoutComponentResponse(
                component.Id,
                component.Product.ToString(),
                component.OfferId,
                component.Status.ToString(),
                component.FailureCode)).ToArray(),
            checkout.PaymentAttempts.Select(payment => new CheckoutPaymentResponse(
                payment.Id,
                payment.Amount,
                payment.Currency,
                payment.Status.ToString(),
                payment.FailureCode)).ToArray(),
            paymentSession,
            checkout.RecoveryCases.Count,
            checkout.CreatedAt,
            checkout.UpdatedAt,
            checkout.ExpiresAt,
            retryAfterSeconds);
    }
}
