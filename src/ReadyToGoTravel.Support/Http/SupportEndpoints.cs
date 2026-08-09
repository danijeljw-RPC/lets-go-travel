using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using ReadyToGoTravel.Support.Application;
using ReadyToGoTravel.Support.Domain;

namespace ReadyToGoTravel.Support.Http;

public static class SupportEndpoints
{
    public static RouteGroupBuilder MapSupportEndpoints(this RouteGroupBuilder group)
    {
        var support = group.MapGroup("/support");
        support.MapPost("/tickets", CreateTicketAsync).RequireRateLimiting("support-ticket-create");

        var authenticated = support.MapGroup(string.Empty)
            .RequireAuthorization("consumer")
            .RequireRateLimiting("support");
        authenticated.MapGet("/tickets", ListTicketsAsync);
        authenticated.MapGet("/tickets/{ticketId:guid}", GetTicketAsync);
        authenticated.MapPost("/tickets/{ticketId:guid}/messages", AddMessageAsync);
        authenticated.MapPost("/tickets/{ticketId:guid}/attachments", UploadAttachmentAsync).DisableAntiforgery();
        authenticated.MapGet("/tickets/{ticketId:guid}/attachments/{attachmentId:guid}/download", DownloadAttachmentAsync);
        return group;
    }

    private static async Task<IResult> CreateTicketAsync(
        CreateTicketRequest request,
        ClaimsPrincipal principal,
        SupportTicketService service,
        SupportAttachmentService attachments,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<SupportTicketCategory>(request.Category, true, out var category) || !Enum.IsDefined(category))
        {
            return SupportHttpResults.Problem(context, StatusCodes.Status400BadRequest, "support_category_invalid");
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return SupportHttpResults.Problem(context, StatusCodes.Status400BadRequest, "support_message_required");
        }

        if (request.Message.Length > SupportTicketMessage.MaxBodyLength)
        {
            return SupportHttpResults.Problem(context, StatusCodes.Status400BadRequest, "support_message_too_long");
        }

        var isAuthenticated = principal.Identity?.IsAuthenticated == true;
        var subject = isAuthenticated ? principal.FindFirstValue("sub") : null;
        var contactEmail = isAuthenticated ? principal.FindFirstValue("email") : request.ContactEmail;
        var contactName = request.ContactName;

        if (string.IsNullOrWhiteSpace(contactName))
        {
            return SupportHttpResults.Problem(context, StatusCodes.Status400BadRequest, "support_contact_name_required");
        }

        if (contactName.Length > SupportTicket.MaxContactNameLength)
        {
            return SupportHttpResults.Problem(context, StatusCodes.Status400BadRequest, "support_contact_name_too_long");
        }

        if (string.IsNullOrWhiteSpace(contactEmail) || !contactEmail.Contains('@', StringComparison.Ordinal))
        {
            return SupportHttpResults.Problem(context, StatusCodes.Status400BadRequest, "support_contact_email_required");
        }

        if (contactEmail.Length > SupportTicket.MaxContactEmailLength)
        {
            return SupportHttpResults.Problem(context, StatusCodes.Status400BadRequest, "support_contact_email_too_long");
        }

        if (request.BookingReference is { Length: > SupportTicket.MaxBookingReferenceLength })
        {
            return SupportHttpResults.Problem(context, StatusCodes.Status400BadRequest, "support_booking_reference_too_long");
        }

        var ticket = await service.CreateTicketAsync(
            new CreateSupportTicketCommand(subject, contactName, contactEmail, category, request.BookingReference, request.Message),
            cancellationToken);
        var ticketAttachments = await attachments.ListForTicketAsync(ticket.Id, cancellationToken);
        return Results.Created(
            $"/api/v1/support/tickets/{ticket.Id}",
            SupportHttpResults.ToResponse(ticket, ticketAttachments));
    }

    private static async Task<IResult> ListTicketsAsync(
        ClaimsPrincipal principal,
        SupportTicketService service,
        CancellationToken cancellationToken)
    {
        var tickets = await service.GetForCustomerAsync(principal.FindFirstValue("sub")!, cancellationToken);
        return Results.Ok(tickets.Select(SupportHttpResults.ToSummary));
    }

    private static async Task<IResult> GetTicketAsync(
        Guid ticketId,
        ClaimsPrincipal principal,
        SupportTicketService service,
        SupportAttachmentService attachments,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var ticket = await service.GetForCustomerAsync(principal.FindFirstValue("sub")!, ticketId, cancellationToken);
        if (ticket is null)
        {
            return SupportHttpResults.Problem(context, StatusCodes.Status404NotFound, "ticket_not_found");
        }

        var ticketAttachments = await attachments.ListForTicketAsync(ticketId, cancellationToken);
        return Results.Ok(SupportHttpResults.ToResponse(ticket, ticketAttachments));
    }

    private static async Task<IResult> AddMessageAsync(
        Guid ticketId,
        AddMessageRequest request,
        ClaimsPrincipal principal,
        SupportTicketService service,
        SupportAttachmentService attachments,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return SupportHttpResults.Problem(context, StatusCodes.Status400BadRequest, "support_message_required");
        }

        if (request.Body.Length > SupportTicketMessage.MaxBodyLength)
        {
            return SupportHttpResults.Problem(context, StatusCodes.Status400BadRequest, "support_message_too_long");
        }

        var subject = principal.FindFirstValue("sub")!;
        var owned = await service.GetForCustomerAsync(subject, ticketId, cancellationToken);
        if (owned is null)
        {
            return SupportHttpResults.Problem(context, StatusCodes.Status404NotFound, "ticket_not_found");
        }

        try
        {
            await service.AddMessageAsync(ticketId, SupportAuthorType.Customer, subject, request.Body, cancellationToken);
        }
        catch (SupportReplyConflictException)
        {
            return SupportHttpResults.Problem(context, StatusCodes.Status409Conflict, "support_reply_conflict");
        }

        var ticket = await service.GetForCustomerAsync(subject, ticketId, cancellationToken);
        var ticketAttachments = await attachments.ListForTicketAsync(ticketId, cancellationToken);
        return Results.Ok(SupportHttpResults.ToResponse(ticket!, ticketAttachments));
    }

    private static async Task<IResult> UploadAttachmentAsync(
        Guid ticketId,
        IFormFile file,
        [FromForm] Guid messageId,
        ClaimsPrincipal principal,
        SupportTicketService service,
        SupportAttachmentService attachments,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var subject = principal.FindFirstValue("sub")!;
        var ticket = await service.GetForCustomerAsync(subject, ticketId, cancellationToken);
        if (ticket is null || ticket.Messages.All(value => value.Id != messageId))
        {
            return SupportHttpResults.Problem(context, StatusCodes.Status404NotFound, "ticket_not_found");
        }

        await using var content = file.OpenReadStream();
        var result = await attachments.UploadAsync(
            ticketId, messageId, subject, null, file.FileName, file.ContentType, content, cancellationToken);
        return result switch
        {
            SupportAttachmentUploadResult.Accepted accepted => Results.Created(
                $"/api/v1/support/tickets/{ticketId}/attachments/{accepted.Attachment.Id}/download",
                new AttachmentResponse(
                    accepted.Attachment.Id,
                    accepted.Attachment.MessageId,
                    accepted.Attachment.OriginalFileName,
                    accepted.Attachment.ContentType,
                    accepted.Attachment.SizeBytes,
                    accepted.Attachment.ScanStatus.ToString(),
                    accepted.Attachment.CreatedAt)),
            SupportAttachmentUploadResult.Rejected rejected => SupportHttpResults.Problem(
                context, StatusCodes.Status422UnprocessableEntity, rejected.Reason),
            _ => SupportHttpResults.Problem(context, StatusCodes.Status400BadRequest, "attachment_rejected"),
        };
    }

    private static async Task<IResult> DownloadAttachmentAsync(
        Guid ticketId,
        Guid attachmentId,
        ClaimsPrincipal principal,
        SupportTicketService service,
        SupportAttachmentService attachments,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var subject = principal.FindFirstValue("sub")!;
        var ticket = await service.GetForCustomerAsync(subject, ticketId, cancellationToken);
        if (ticket is null)
        {
            return SupportHttpResults.Problem(context, StatusCodes.Status404NotFound, "ticket_not_found");
        }

        var url = await attachments.CreateDownloadUrlAsync(ticketId, attachmentId, cancellationToken);
        return url is null
            ? SupportHttpResults.Problem(context, StatusCodes.Status404NotFound, "attachment_not_available")
            : Results.Ok(new DownloadUrlResponse(url.ToString(), DateTimeOffset.UtcNow.AddMinutes(5)));
    }
}
