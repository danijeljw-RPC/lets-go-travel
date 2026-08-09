using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using ReadyToGoTravel.Support.Application;
using ReadyToGoTravel.Support.Domain;
using ReadyToGoTravel.Support.Guest;

namespace ReadyToGoTravel.Support.Http;

public static class SupportGuestEndpoints
{
    public static RouteGroupBuilder MapSupportGuestEndpoints(this RouteGroupBuilder group)
    {
        var guest = group.MapGroup("/support/guest").RequireRateLimiting("support-guest");
        guest.MapGet("/ticket", GetTicketAsync);
        guest.MapPost("/ticket/messages", AddMessageAsync);
        guest.MapPost("/ticket/attachments", UploadAttachmentAsync).DisableAntiforgery();
        guest.MapGet("/ticket/attachments/{attachmentId:guid}/download", DownloadAttachmentAsync);
        return group;
    }

    private static async Task<IResult> GetTicketAsync(
        HttpContext context,
        IGuestAccessTokenService tokens,
        SupportTicketService service,
        SupportAttachmentService attachments,
        CancellationToken cancellationToken)
    {
        var ticketId = await ResolveAsync(context, tokens, cancellationToken);
        if (ticketId is null)
        {
            return SupportHttpResults.Problem(context, StatusCodes.Status401Unauthorized, "guest_link_invalid");
        }

        var ticket = await service.GetForStaffAsync(ticketId.Value, cancellationToken);
        if (ticket is null)
        {
            return SupportHttpResults.Problem(context, StatusCodes.Status401Unauthorized, "guest_link_invalid");
        }

        var ticketAttachments = await attachments.ListForTicketAsync(ticketId.Value, cancellationToken);
        return Results.Ok(SupportHttpResults.ToResponse(ticket, ticketAttachments));
    }

    private static async Task<IResult> AddMessageAsync(
        AddMessageRequest request,
        HttpContext context,
        IGuestAccessTokenService tokens,
        SupportTicketService service,
        SupportAttachmentService attachments,
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

        var ticketId = await ResolveAsync(context, tokens, cancellationToken);
        if (ticketId is null)
        {
            return SupportHttpResults.Problem(context, StatusCodes.Status401Unauthorized, "guest_link_invalid");
        }

        try
        {
            await service.AddMessageAsync(ticketId.Value, SupportAuthorType.Guest, null, request.Body, cancellationToken);
        }
        catch (SupportReplyConflictException)
        {
            return SupportHttpResults.Problem(context, StatusCodes.Status409Conflict, "support_reply_conflict");
        }

        var ticket = await service.GetForStaffAsync(ticketId.Value, cancellationToken);
        var ticketAttachments = await attachments.ListForTicketAsync(ticketId.Value, cancellationToken);
        return Results.Ok(SupportHttpResults.ToResponse(ticket!, ticketAttachments));
    }

    private static async Task<IResult> UploadAttachmentAsync(
        IFormFile file,
        [FromForm] Guid messageId,
        HttpContext context,
        IGuestAccessTokenService tokens,
        SupportTicketService service,
        SupportAttachmentService attachments,
        CancellationToken cancellationToken)
    {
        var ticketId = await ResolveAsync(context, tokens, cancellationToken);
        if (ticketId is null)
        {
            return SupportHttpResults.Problem(context, StatusCodes.Status401Unauthorized, "guest_link_invalid");
        }

        var ticket = await service.GetForStaffAsync(ticketId.Value, cancellationToken);
        if (ticket is null || ticket.Messages.All(value => value.Id != messageId))
        {
            return SupportHttpResults.Problem(context, StatusCodes.Status404NotFound, "ticket_message_not_found");
        }

        await using var content = file.OpenReadStream();
        var result = await attachments.UploadAsync(
            ticketId.Value, messageId, null, null, file.FileName, file.ContentType, content, cancellationToken);
        return result switch
        {
            SupportAttachmentUploadResult.Accepted accepted => Results.Created(
                $"/api/v1/support/guest/ticket/attachments/{accepted.Attachment.Id}/download",
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
        Guid attachmentId,
        HttpContext context,
        IGuestAccessTokenService tokens,
        SupportAttachmentService attachments,
        CancellationToken cancellationToken)
    {
        var ticketId = await ResolveAsync(context, tokens, cancellationToken);
        if (ticketId is null)
        {
            return SupportHttpResults.Problem(context, StatusCodes.Status401Unauthorized, "guest_link_invalid");
        }

        var url = await attachments.CreateDownloadUrlAsync(ticketId.Value, attachmentId, cancellationToken);
        return url is null
            ? SupportHttpResults.Problem(context, StatusCodes.Status404NotFound, "attachment_not_available")
            : Results.Ok(new DownloadUrlResponse(url.ToString(), DateTimeOffset.UtcNow.AddMinutes(5)));
    }

    private static async Task<Guid?> ResolveAsync(
        HttpContext context,
        IGuestAccessTokenService tokens,
        CancellationToken cancellationToken)
    {
        var header = context.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";

        // Always resolve through the shared credential path, even for a missing header or a
        // non-Bearer scheme, so every guest authentication failure - not just an invalid, expired
        // or revoked token - creates the same GuestLinkAuthenticationFailed audit event. A
        // non-Bearer credential (e.g. Basic) is never forwarded: it belongs to a different auth
        // scheme and must not be logged or hashed as if it were a guest token.
        var rawToken = header.StartsWith(prefix, StringComparison.Ordinal) ? header[prefix.Length..] : string.Empty;
        return await tokens.ResolveAsync(rawToken, cancellationToken);
    }
}
