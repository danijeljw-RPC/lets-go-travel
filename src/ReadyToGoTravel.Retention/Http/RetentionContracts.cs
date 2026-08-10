namespace ReadyToGoTravel.Retention.Http;

public sealed record CreateLegalHoldRequest(
    string MatterReference,
    string Reason,
    DateTimeOffset ReviewByUtc,
    IReadOnlyList<LegalHoldScopeEntryRequest> Scopes);

public sealed record LegalHoldScopeEntryRequest(
    string RecordClass,
    Guid? CustomerId,
    Guid? ComponentBookingId,
    Guid? SupportTicketId);

public sealed record ReleaseLegalHoldRequest(string ReleaseReason);

public sealed record LegalHoldResponse(
    Guid Id,
    string MatterReference,
    string Reason,
    string AuthorizedOwnerSubject,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ReviewByUtc,
    DateTimeOffset? ReleasedAtUtc,
    string? ReleasedBySubject,
    string? ReleaseReason,
    bool IsActive,
    IReadOnlyList<LegalHoldScopeResponse> Scopes);

public sealed record LegalHoldScopeResponse(
    Guid Id,
    string RecordClass,
    Guid? CustomerId,
    Guid? ComponentBookingId,
    Guid? SupportTicketId);
