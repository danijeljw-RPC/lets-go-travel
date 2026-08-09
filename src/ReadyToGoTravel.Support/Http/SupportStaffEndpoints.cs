using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ReadyToGoTravel.Support.Application;
using ReadyToGoTravel.Support.Domain;
using ReadyToGoTravel.Support.Guest;

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

        await service.CloseAsync(ticketId, principal.FindFirstValue("sub") ?? "support", cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> RotateGuestLinkAsync(
        Guid ticketId,
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

        await tokens.RotateAsync(ticketId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> RevokeGuestLinkAsync(
        Guid ticketId,
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

        await tokens.RevokeAsync(ticketId, cancellationToken);
        return Results.NoContent();
    }
}
