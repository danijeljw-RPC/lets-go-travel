using Microsoft.AspNetCore.Http;
using ReadyToGoTravel.Support.Domain;

namespace ReadyToGoTravel.Support.Http;

internal static class SupportHttpResults
{
    public static IResult Problem(HttpContext context, int statusCode, string code) => Results.Problem(
        statusCode: statusCode,
        extensions: new Dictionary<string, object?>
        {
            ["code"] = code,
            ["correlationId"] = context.TraceIdentifier,
        });

    public static TicketResponse ToResponse(SupportTicket ticket, IReadOnlyList<SupportAttachment> attachments) => new(
        ticket.Id,
        ticket.ContactName,
        ticket.ContactEmail,
        ticket.Category.ToString(),
        ticket.IsUrgent,
        ticket.BookingReference,
        ticket.Status.ToString(),
        ticket.CreatedAt,
        ticket.UpdatedAt,
        ticket.ClosedAt,
        [.. ticket.Messages
            .OrderBy(value => value.SequenceNumber)
            .Select(value => new MessageResponse(value.Id, value.SequenceNumber, value.AuthorType.ToString(), value.Body, value.CreatedAt))],
        [.. attachments.Select(value => new AttachmentResponse(
            value.Id, value.MessageId, value.OriginalFileName, value.ContentType, value.SizeBytes, value.ScanStatus.ToString(), value.CreatedAt))]);

    public static TicketSummaryResponse ToSummary(SupportTicket ticket) => new(
        ticket.Id, ticket.Category.ToString(), ticket.IsUrgent, ticket.Status.ToString(), ticket.UpdatedAt);
}
