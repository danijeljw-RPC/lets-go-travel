using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ReadyToGoTravel.Retention.Application;
using ReadyToGoTravel.Retention.Domain;

namespace ReadyToGoTravel.Retention.Http;

public static class LegalHoldEndpoints
{
    public static RouteGroupBuilder MapLegalHoldEndpoints(this RouteGroupBuilder group)
    {
        var holds = group.MapGroup("/retention/legal-holds")
            .RequireAuthorization("legal-hold-officer");
        holds.MapPost("/", CreateAsync);
        holds.MapGet("/", ListAsync);
        holds.MapGet("/{legalHoldId:guid}", GetAsync);
        holds.MapPost("/{legalHoldId:guid}/release", ReleaseAsync);
        return group;
    }

    private static async Task<IResult> CreateAsync(
        CreateLegalHoldRequest request,
        ClaimsPrincipal principal,
        LegalHoldService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.MatterReference))
        {
            return Problem(context, StatusCodes.Status400BadRequest, "legal_hold_matter_reference_required");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Problem(context, StatusCodes.Status400BadRequest, "legal_hold_reason_required");
        }

        if (request.Scopes is not { Count: > 0 })
        {
            return Problem(context, StatusCodes.Status400BadRequest, "legal_hold_scope_required");
        }

        var scopeRequests = new List<LegalHoldScopeRequest>(request.Scopes.Count);
        foreach (var scope in request.Scopes)
        {
            if (!Enum.TryParse<RetentionRecordClass>(scope.RecordClass, out var recordClass) || !Enum.IsDefined(recordClass))
            {
                return Problem(context, StatusCodes.Status400BadRequest, "legal_hold_scope_record_class_invalid");
            }

            var keyCount = (scope.CustomerId is not null ? 1 : 0)
                + (scope.ComponentBookingId is not null ? 1 : 0)
                + (scope.SupportTicketId is not null ? 1 : 0);
            if (keyCount != 1)
            {
                return Problem(context, StatusCodes.Status400BadRequest, "legal_hold_scope_subject_invalid");
            }

            // Each live-swept record class's owning sweep queries ExcludeHeldAsync/IsHeldAsync
            // for exactly one subject kind (RetentionPolicyCatalog.ExpectedSubjectKind). A scope
            // naming a different, structurally valid subject kind would be persisted successfully
            // but never found by that sweep - silently providing zero protection while looking
            // like a real hold. Reject that combination here rather than accepting an inert hold.
            var providedSubjectKind = scope.CustomerId is not null
                ? RetentionSubjectKind.Customer
                : scope.ComponentBookingId is not null
                    ? RetentionSubjectKind.ComponentBooking
                    : RetentionSubjectKind.SupportTicket;
            var expectedSubjectKind = RetentionPolicyCatalog.Get(recordClass).ExpectedSubjectKind;
            if (expectedSubjectKind is not null && expectedSubjectKind != providedSubjectKind)
            {
                return Problem(context, StatusCodes.Status400BadRequest, "legal_hold_scope_subject_kind_mismatch");
            }

            scopeRequests.Add(new LegalHoldScopeRequest(recordClass, scope.CustomerId, scope.ComponentBookingId, scope.SupportTicketId));
        }

        var actorSubject = principal.FindFirstValue("sub")!;
        var hold = await service.OpenAsync(
            request.MatterReference, request.Reason, actorSubject, request.ReviewByUtc, scopeRequests, cancellationToken);
        return Results.Created($"/api/v1/retention/legal-holds/{hold.Id}", ToResponse(hold));
    }

    private static async Task<IResult> ListAsync(
        bool? active,
        LegalHoldService service,
        CancellationToken cancellationToken)
    {
        var holds = await service.ListAsync(active, cancellationToken);
        return Results.Ok(holds.Select(ToResponse));
    }

    private static async Task<IResult> GetAsync(
        Guid legalHoldId,
        LegalHoldService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var hold = await service.GetAsync(legalHoldId, cancellationToken);
        return hold is null
            ? Problem(context, StatusCodes.Status404NotFound, "legal_hold_not_found")
            : Results.Ok(ToResponse(hold));
    }

    private static async Task<IResult> ReleaseAsync(
        Guid legalHoldId,
        ReleaseLegalHoldRequest request,
        ClaimsPrincipal principal,
        LegalHoldService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ReleaseReason))
        {
            return Problem(context, StatusCodes.Status400BadRequest, "legal_hold_release_reason_required");
        }

        var actorSubject = principal.FindFirstValue("sub")!;
        var hold = await service.ReleaseAsync(legalHoldId, actorSubject, request.ReleaseReason, cancellationToken);
        return hold is null
            ? Problem(context, StatusCodes.Status404NotFound, "legal_hold_not_found")
            : Results.Ok(ToResponse(hold));
    }

    private static IResult Problem(HttpContext context, int statusCode, string code) => Results.Problem(
        statusCode: statusCode,
        extensions: new Dictionary<string, object?>
        {
            ["code"] = code,
            ["correlationId"] = context.TraceIdentifier,
        });

    private static LegalHoldResponse ToResponse(LegalHold hold) => new(
        hold.Id,
        hold.MatterReference,
        hold.Reason,
        hold.AuthorizedOwnerSubject,
        hold.CreatedAtUtc,
        hold.ReviewByUtc,
        hold.ReleasedAtUtc,
        hold.ReleasedBySubject,
        hold.ReleaseReason,
        hold.IsActive,
        [.. hold.Scopes.Select(scope => new LegalHoldScopeResponse(
            scope.Id, scope.RecordClass.ToString(), scope.CustomerId, scope.ComponentBookingId, scope.SupportTicketId))]);
}
