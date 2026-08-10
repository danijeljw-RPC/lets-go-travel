using Microsoft.AspNetCore.Http;
using ReadyToGoTravel.Booking.Application;

namespace ReadyToGoTravel.Booking.Http;

internal static class BookingHttpResults
{
    internal static IResult From(HttpContext context, CheckoutServiceResult result)
    {
        if (result.ErrorCode is null && result.Value is not null)
        {
            return result.StatusCode == StatusCodes.Status201Created
                ? Results.Created($"/api/v1/checkouts/{result.Value.Id}", result.Value)
                : Results.Json(result.Value, statusCode: result.StatusCode);
        }

        var extensions = new Dictionary<string, object?>
        {
            ["code"] = result.ErrorCode ?? "request_failed",
            ["correlationId"] = context.TraceIdentifier,
        };
        if (result.Value is not null)
        {
            extensions["checkout"] = result.Value;
        }

        if (result.RetryAfterSeconds.HasValue)
        {
            extensions["retryAfterSeconds"] = result.RetryAfterSeconds.Value;
        }

        return Results.Problem(
            statusCode: result.StatusCode,
            title: Title(result.ErrorCode),
            extensions: extensions);
    }

    internal static IResult MissingIdempotencyKey(HttpContext context) => Results.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Idempotency key required",
        extensions: new Dictionary<string, object?>
        {
            ["code"] = "idempotency_key_required",
            ["correlationId"] = context.TraceIdentifier,
        });

    private static string Title(string? errorCode) => errorCode switch
    {
        "checkout_not_found" => "Checkout not found",
        "booking_capability_unavailable" => "Booking capability unavailable",
        "price_acceptance_required" => "Price acceptance required",
        "idempotency_conflict" => "Idempotency conflict",
        _ => "Checkout request could not be completed",
    };
}
