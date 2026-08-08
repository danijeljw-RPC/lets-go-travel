using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ReadyToGoTravel.Booking.Application;

namespace ReadyToGoTravel.Booking.Http;

public static class BookingEndpoints
{
    public static RouteGroupBuilder MapBookingEndpoints(this RouteGroupBuilder group)
    {
        var checkouts = group.MapGroup("/checkouts")
            .RequireAuthorization("consumer")
            .RequireRateLimiting("checkout");
        checkouts.MapPost(string.Empty, CreateAsync);
        checkouts.MapGet("/{checkoutId:guid}", GetAsync);
        checkouts.MapGet("/{checkoutId:guid}/history", GetHistoryAsync);
        checkouts.MapPost("/{checkoutId:guid}/acceptance", AcceptAsync);
        checkouts.MapPost("/{checkoutId:guid}/payment-session", PreparePaymentAsync);
        checkouts.MapPost("/{checkoutId:guid}/payment-return", ReturnPaymentAsync);
        checkouts.MapPost("/{checkoutId:guid}/book", BookAsync);
        checkouts.MapPost("/{checkoutId:guid}/recover", RecoverAsync);
        return group;
    }

    private static async Task<IResult> CreateAsync(
        CreateCheckoutRequest request,
        ClaimsPrincipal principal,
        CheckoutService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!TryGetIdempotencyKey(context, out var key))
        {
            return BookingHttpResults.MissingIdempotencyKey(context);
        }

        var result = await service.CreateAsync(
            principal.FindFirstValue("sub")!,
            request,
            key,
            cancellationToken);
        return BookingHttpResults.From(context, result);
    }

    private static async Task<IResult> GetAsync(
        Guid checkoutId,
        ClaimsPrincipal principal,
        CheckoutService service,
        HttpContext context,
        CancellationToken cancellationToken) => BookingHttpResults.From(
            context,
            await service.GetAsync(principal.FindFirstValue("sub")!, checkoutId, cancellationToken));

    private static async Task<IResult> GetHistoryAsync(
        Guid checkoutId,
        ClaimsPrincipal principal,
        BookingHistoryService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var history = await service.GetAsync(
            principal.FindFirstValue("sub")!,
            checkoutId,
            cancellationToken);
        return history is null
            ? Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "checkout_not_found",
                    ["correlationId"] = context.TraceIdentifier,
                })
            : Results.Ok(history);
    }

    private static async Task<IResult> AcceptAsync(
        Guid checkoutId,
        AcceptCheckoutRequest request,
        ClaimsPrincipal principal,
        CheckoutService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!TryGetIdempotencyKey(context, out var key))
        {
            return BookingHttpResults.MissingIdempotencyKey(context);
        }

        return BookingHttpResults.From(
            context,
            await service.AcceptAsync(
                principal.FindFirstValue("sub")!,
                checkoutId,
                request,
                key,
                cancellationToken));
    }

    private static async Task<IResult> PreparePaymentAsync(
        Guid checkoutId,
        EmptyCheckoutCommandRequest request,
        ClaimsPrincipal principal,
        CheckoutService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!TryGetIdempotencyKey(context, out var key))
        {
            return BookingHttpResults.MissingIdempotencyKey(context);
        }

        return BookingHttpResults.From(
            context,
            await service.PreparePaymentAsync(
                principal.FindFirstValue("sub")!, checkoutId, request, key, cancellationToken));
    }

    private static async Task<IResult> ReturnPaymentAsync(
        Guid checkoutId,
        PaymentReturnRequest request,
        ClaimsPrincipal principal,
        CheckoutService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!TryGetIdempotencyKey(context, out var key))
        {
            return BookingHttpResults.MissingIdempotencyKey(context);
        }

        return BookingHttpResults.From(
            context,
            await service.ReturnPaymentAsync(
                principal.FindFirstValue("sub")!, checkoutId, request, key, cancellationToken));
    }

    private static async Task<IResult> BookAsync(
        Guid checkoutId,
        EmptyCheckoutCommandRequest request,
        ClaimsPrincipal principal,
        CheckoutService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!TryGetIdempotencyKey(context, out var key))
        {
            return BookingHttpResults.MissingIdempotencyKey(context);
        }

        return BookingHttpResults.From(
            context,
            await service.BookAsync(
                principal.FindFirstValue("sub")!, checkoutId, request, key, cancellationToken));
    }

    // Recovery is intentionally exempt from Idempotency-Key because it only retrieves provider state
    // and converges durable records; it never initiates a charge, settlement, or supplier booking.
    private static async Task<IResult> RecoverAsync(
        Guid checkoutId,
        EmptyCheckoutCommandRequest request,
        ClaimsPrincipal principal,
        CheckoutService service,
        HttpContext context,
        CancellationToken cancellationToken) => BookingHttpResults.From(
            context,
            await service.RecoverAsync(
                principal.FindFirstValue("sub")!, checkoutId, request, cancellationToken));

    private static bool TryGetIdempotencyKey(HttpContext context, out string key)
    {
        key = context.Request.Headers["Idempotency-Key"].ToString();
        return !string.IsNullOrWhiteSpace(key) && key.Length <= 255;
    }
}
