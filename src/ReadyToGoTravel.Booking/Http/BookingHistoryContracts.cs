using System.Text.Json;

namespace ReadyToGoTravel.Booking.Http;

internal sealed record BookingHistoryResponse(
    Guid CheckoutId,
    IReadOnlyList<BookingComponentHistoryResponse> Components);

internal sealed record BookingComponentHistoryResponse(
    Guid ComponentId,
    string Product,
    string CurrentStatus,
    int CurrentVersionNumber,
    DateTimeOffset? LastReconciledAt,
    IReadOnlyList<BookingVersionHistoryResponse> Versions);

internal sealed record BookingVersionHistoryResponse(
    int Number,
    DateTimeOffset ObservedAt,
    DateTimeOffset? EffectiveAt,
    string Source,
    string CanonicalisationVersion,
    string Severity,
    IReadOnlyList<string> Flags,
    JsonElement Diff);
