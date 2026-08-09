using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ReadyToGoTravel.Support.Application;
using ReadyToGoTravel.Support.Domain;
using ReadyToGoTravel.Support.Guest;
using ReadyToGoTravel.Support.Notifications;

namespace ReadyToGoTravel.Support.Http;

public static class SupportStaffEndpoints
{
    public static RouteGroupBuilder MapSupportStaffEndpoints(this RouteGroupBuilder group)
    {
        var staff = group.MapGroup("/support/staff")
            .RequireAuthorization("support-agent")
            .RequireRateLimiting("support");
        staff.MapGet("/tickets", ListTicketsAsync);
        staff.MapGet("/tickets/{ticketId:guid}", GetTicketAsync);
        staff.MapPost("/tickets/{ticketId:guid}/messages", AddMessageAsync);
        staff.MapPost("/tickets/{ticketId:guid}/close", CloseAsync);
        staff.MapGet("/tickets/{ticketId:guid}/attachments/{attachmentId:guid}/download", DownloadAttachmentAsync);
        staff.MapPost("/tickets/{ticketId:guid}/guest-link/rotate", RotateGuestLinkAsync);
        staff.MapPost("/tickets/{ticketId:guid}/guest-link/revoke", RevokeGuestLinkAsync);
        return group;
    }

    private static async Task<IResult> ListTicketsAsync(
        SupportTicketService service,
        CancellationToken cancellationToken)
    {
        var tickets = await service.ListForStaffAsync(cancellationToken);
        return Results.Ok(tickets.Select(SupportHttpResults.ToSummary));
    }

    private static async Task<IResult> GetTicketAsync(
        Guid ticketId,
        SupportTicketService service,
        SupportAttachmentService attachments,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var ticket = await service.GetForStaffAsync(ticketId, cancellationToken);
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

        var existing = await service.GetForStaffAsync(ticketId, cancellationToken);
        if (existing is null)
        {
            return SupportHttpResults.Problem(context, StatusCodes.Status404NotFound, "ticket_not_found");
        }

        try
        {
            await service.AddMessageAsync(
                ticketId, SupportAuthorType.Support, principal.FindFirstValue("sub"), request.Body, cancellationToken);
        }
        catch (SupportMessageTooLongException)
        {
            return SupportHttpResults.Problem(context, StatusCodes.Status400BadRequest, "support_message_too_long");
        }
        catch (SupportReplyConflictException)
        {
            return SupportHttpResults.Problem(context, StatusCodes.Status409Conflict, "support_reply_conflict");
        }
        catch (InvalidOperationException)
        {
            return SupportHttpResults.Problem(context, StatusCodes.Status409Conflict, "ticket_closed");
        }

        var ticket = await service.GetForStaffAsync(ticketId, cancellationToken);
        var ticketAttachments = await attachments.ListForTicketAsync(ticketId, cancellationToken);
        return Results.Ok(SupportHttpResults.ToResponse(ticket!, ticketAttachments));
    }

    private static async Task<IResult> CloseAsync(
        Guid ticketId,
        ClaimsPrincipal principal,
        SupportTicketService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var existing = await service.GetForStaffAsync(ticketId, cancellationToken);
        if (existing is null)
        {
            return SupportHttpResults.Problem(context, StatusCodes.Status404NotFound, "ticket_not_found");
        }

        try
        {
            await service.CloseAsync(ticketId, principal.FindFirstValue("sub") ?? "support", cancellationToken);
        }
        catch (SupportReplyConflictException)
        {
            return SupportHttpResults.Problem(context, StatusCodes.Status409Conflict, "support_reply_conflict");
        }

        return Results.NoContent();
    }

    private static async Task<IResult> DownloadAttachmentAsync(
        Guid ticketId,
        Guid attachmentId,
        SupportTicketService service,
        SupportAttachmentService attachments,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var existing = await service.GetForStaffAsync(ticketId, cancellationToken);
        if (existing is null)
        {
            return SupportHttpResults.Problem(context, StatusCodes.Status404NotFound, "ticket_not_found");
        }

        var url = await attachments.CreateDownloadUrlAsync(ticketId, attachmentId, cancellationToken);
        return url is null
            ? SupportHttpResults.Problem(context, StatusCodes.Status404NotFound, "attachment_not_available")
            : Results.Ok(new DownloadUrlResponse(url.ToString(), DateTimeOffset.UtcNow.AddMinutes(5)));
    }

    private static async Task<IResult> RotateGuestLinkAsync(
        Guid ticketId,
        ClaimsPrincipal principal,
        SupportTicketService service,
        IGuestAccessTokenService tokens,
        ISupportNotificationSender sender,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var existing = await service.GetForStaffAsync(ticketId, cancellationToken);
        if (existing is null)
        {
            return SupportHttpResults.Problem(context, StatusCodes.Status404NotFound, "ticket_not_found");
        }

        var rawToken = await tokens.RotateAsync(ticketId, principal.FindFirstValue("sub"), cancellationToken);
        var payload = new SupportTicketNotificationPayload(existing.ContactName, existing.Id, existing.Category.ToString())
            .ToJsonWithGuestToken(rawToken);
        SupportNotificationSendResult result;
        try
        {
            result = await sender.SendAsync(
                new SupportNotification(
                    $"{ticketId:N}:guest-link-rotated:{Guid.CreateVersion7():N}",
                    existing.ContactEmail,
                    SupportNotificationTemplates.GuestLinkRotated,
                    payload),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            result = SupportNotificationSendResult.Retry("support_notification_delivery_failed");
        }

        return Results.Ok(new RotateGuestLinkResponse(result.Success));
    }

    private static async Task<IResult> RevokeGuestLinkAsync(
        Guid ticketId,
        ClaimsPrincipal principal,
        SupportTicketService service,
        IGuestAccessTokenService tokens,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var existing = await service.GetForStaffAsync(ticketId, cancellationToken);
        if (existing is null)
        {
            return SupportHttpResults.Problem(context, StatusCodes.Status404NotFound, "ticket_not_found");
        }

        await tokens.RevokeAsync(ticketId, principal.FindFirstValue("sub"), cancellationToken);
        return Results.NoContent();
    }
}
