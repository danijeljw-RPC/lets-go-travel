using System.Net;
using System.Net.Http.Json;

namespace ReadyToGoTravel.Web.Client;

public sealed class BookingApiClient(HttpClient client)
{
    public Task<BookingClientResult> CreateCheckoutAsync(
        CreateCheckoutInput input,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Post,
            "/api/v1/checkouts",
            input,
            idempotencyKey,
            cancellationToken);

    public Task<BookingClientResult> GetCheckoutAsync(
        Guid checkoutId,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Get,
            $"/api/v1/checkouts/{checkoutId}",
            null,
            null,
            cancellationToken);

    public Task<BookingClientResult> AcceptAsync(
        Guid checkoutId,
        CheckoutAcceptanceInput input,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Post,
            $"/api/v1/checkouts/{checkoutId}/acceptance",
            input,
            null,
            cancellationToken);

    public Task<BookingClientResult> CreatePaymentSessionAsync(
        Guid checkoutId,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Post,
            $"/api/v1/checkouts/{checkoutId}/payment-session",
            EmptyCheckoutCommand.Instance,
            idempotencyKey,
            cancellationToken);

    public Task<BookingClientResult> ReturnPaymentAsync(
        Guid checkoutId,
        string completionReference,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Post,
            $"/api/v1/checkouts/{checkoutId}/payment-return",
            new PaymentReturnInput(completionReference),
            idempotencyKey,
            cancellationToken);

    public Task<BookingClientResult> BookAsync(
        Guid checkoutId,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Post,
            $"/api/v1/checkouts/{checkoutId}/book",
            EmptyCheckoutCommand.Instance,
            idempotencyKey,
            cancellationToken);

    public Task<BookingClientResult> RecoverAsync(
        Guid checkoutId,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Post,
            $"/api/v1/checkouts/{checkoutId}/recover",
            EmptyCheckoutCommand.Instance,
            null,
            cancellationToken);

    private async Task<BookingClientResult> SendAsync(
        HttpMethod method,
        string path,
        object? input,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (input is not null)
            {
                request.Content = JsonContent.Create(input, input.GetType());
            }

            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                request.Headers.Add("Idempotency-Key", idempotencyKey);
            }

            using var response = await client.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var checkout = await response.Content.ReadFromJsonAsync<CheckoutView>(cancellationToken);
                return checkout is null
                    ? new BookingClientResult(null, "booking_response_invalid", response.StatusCode)
                    : new BookingClientResult(checkout, null, response.StatusCode);
            }

            var problem = await response.Content.ReadFromJsonAsync<BookingApiProblem>(cancellationToken);
            return new BookingClientResult(
                problem?.Checkout,
                problem?.Code ?? "booking_unavailable",
                response.StatusCode);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return new BookingClientResult(null, "booking_unavailable", HttpStatusCode.ServiceUnavailable);
        }
    }

    private sealed record EmptyCheckoutCommand
    {
        internal static EmptyCheckoutCommand Instance { get; } = new();
    }

    private sealed record PaymentReturnInput(string CompletionReference);

    private sealed record BookingApiProblem(
        string Code,
        string CorrelationId,
        CheckoutView? Checkout,
        int? RetryAfterSeconds);
}

public sealed record CreateCheckoutInput(
    Guid TripId,
    IReadOnlyList<string> OfferIds,
    IReadOnlyList<CheckoutTravellerInput> TravellerAssignments);

public sealed record CheckoutTravellerInput(Guid TravellerId, int? AgeAtTravel);

public sealed record CheckoutAcceptanceInput(
    int RevisionNumber,
    decimal AcceptedTotal,
    string Currency,
    string TermsHash,
    string PolicyVersion);

public sealed record BookingClientResult(
    CheckoutView? Checkout,
    string? ErrorCode,
    HttpStatusCode StatusCode)
{
    public bool IsSuccess => Checkout is not null && ErrorCode is null;
}

public sealed record CheckoutView(
    Guid Id,
    Guid TripId,
    string Status,
    CheckoutRevisionView CurrentRevision,
    CheckoutAcceptanceView? Acceptance,
    IReadOnlyList<CheckoutTravellerView> Travellers,
    IReadOnlyList<CheckoutComponentView> Components,
    IReadOnlyList<CheckoutPaymentView> Payments,
    HostedPaymentSessionView? PaymentSession,
    int RecoveryCaseCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset ExpiresAt,
    int? RetryAfterSeconds);

public sealed record CheckoutRevisionView(
    int Number,
    decimal Total,
    string Currency,
    string TermsHash,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<CheckoutRevisionComponentView> Components);

public sealed record CheckoutRevisionComponentView(
    string Product,
    string OfferId,
    string ProductDetail,
    decimal MinimumTotal,
    string Currency);

public sealed record CheckoutAcceptanceView(
    int RevisionNumber,
    decimal AcceptedTotal,
    string Currency,
    string TermsHash,
    string PolicyVersion,
    DateTimeOffset AcceptedAt);

public sealed record CheckoutTravellerView(
    Guid TravellerId,
    string GivenName,
    string FamilyName,
    bool IsMinor,
    DateTimeOffset? GuardianAuthorityConfirmedAt,
    int? AgeAtTravel);

public sealed record CheckoutComponentView(
    Guid Id,
    string Product,
    string OfferId,
    string Status,
    string? FailureCode);

public sealed record CheckoutPaymentView(
    Guid Id,
    decimal Amount,
    string Currency,
    string Status,
    string? FailureCode);

public sealed record HostedPaymentSessionView(
    string PaymentReference,
    string BrowserToken,
    DateTimeOffset BrowserTokenExpiresAt);
