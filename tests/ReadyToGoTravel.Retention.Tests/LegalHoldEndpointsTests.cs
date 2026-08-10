using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ReadyToGoTravel.Retention.Http;
using ReadyToGoTravel.Retention.Persistence;

namespace ReadyToGoTravel.Retention.Tests;

public sealed class LegalHoldEndpointsTests
{
    [Fact]
    public async Task UnauthenticatedCallerIsRejected()
    {
        await using var app = await TestApplication.CreateAsync();

        var response = await app.Client.GetAsync("/api/v1/retention/legal-holds");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ConsumerOnlyPrincipalIsForbiddenFromEveryRoute()
    {
        await using var app = await TestApplication.CreateAsync();
        app.SetConsumerSubject("customer-1");

        var listResponse = await app.Client.GetAsync("/api/v1/retention/legal-holds");
        var createResponse = await app.Client.PostAsJsonAsync(
            "/api/v1/retention/legal-holds",
            new CreateLegalHoldRequest("MATTER-1", "Dispute", DateTimeOffset.UtcNow.AddDays(90),
                [new LegalHoldScopeEntryRequest("GeneralSupportTicket", null, null, Guid.CreateVersion7())]));

        Assert.Equal(HttpStatusCode.Forbidden, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
    }

    [Fact]
    public async Task LegalHoldOfficerCanCreateListGetAndRelease()
    {
        await using var app = await TestApplication.CreateAsync();
        app.SetStaffSubject("officer-1");
        var supportTicketId = Guid.CreateVersion7();

        var createResponse = await app.Client.PostAsJsonAsync(
            "/api/v1/retention/legal-holds",
            new CreateLegalHoldRequest("MATTER-1", "Dispute", DateTimeOffset.UtcNow.AddDays(90),
                [new LegalHoldScopeEntryRequest("GeneralSupportTicket", null, null, supportTicketId)]));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.ReadAsAsync<LegalHoldResponse>();
        Assert.NotNull(created);
        Assert.True(created.IsActive);
        Assert.Equal("officer-1", created.AuthorizedOwnerSubject);

        var listResponse = await app.Client.GetAsync("/api/v1/retention/legal-holds");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.ReadAsAsync<LegalHoldResponse[]>();
        Assert.Single(list!);

        var getResponse = await app.Client.GetAsync($"/api/v1/retention/legal-holds/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var releaseResponse = await app.Client.PostAsJsonAsync(
            $"/api/v1/retention/legal-holds/{created.Id}/release",
            new ReleaseLegalHoldRequest("Matter resolved"));
        Assert.Equal(HttpStatusCode.OK, releaseResponse.StatusCode);
        var released = await releaseResponse.ReadAsAsync<LegalHoldResponse>();
        Assert.False(released!.IsActive);
        Assert.Equal("officer-1", released.ReleasedBySubject);
    }

    [Fact]
    public async Task CreatingWithZeroScopesReturnsBadRequest()
    {
        await using var app = await TestApplication.CreateAsync();
        app.SetStaffSubject("officer-1");

        var response = await app.Client.PostAsJsonAsync(
            "/api/v1/retention/legal-holds",
            new CreateLegalHoldRequest("MATTER-1", "Dispute", DateTimeOffset.UtcNow.AddDays(90), []));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreatingWithAnInvalidRecordClassReturnsBadRequest()
    {
        await using var app = await TestApplication.CreateAsync();
        app.SetStaffSubject("officer-1");

        var response = await app.Client.PostAsJsonAsync(
            "/api/v1/retention/legal-holds",
            new CreateLegalHoldRequest("MATTER-1", "Dispute", DateTimeOffset.UtcNow.AddDays(90),
                [new LegalHoldScopeEntryRequest("NotARealRecordClass", Guid.CreateVersion7(), null, null)]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreatingWithMultipleSubjectKeysOnOneScopeReturnsBadRequest()
    {
        await using var app = await TestApplication.CreateAsync();
        app.SetStaffSubject("officer-1");

        var response = await app.Client.PostAsJsonAsync(
            "/api/v1/retention/legal-holds",
            new CreateLegalHoldRequest("MATTER-1", "Dispute", DateTimeOffset.UtcNow.AddDays(90),
                [new LegalHoldScopeEntryRequest("GeneralSupportTicket", Guid.CreateVersion7(), Guid.CreateVersion7(), null)]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreatingAScopeWithASubjectKeyItsOwningSweepDoesNotQueryReturnsBadRequest()
    {
        // Regression test for a bug found by Codex review of PR #12 (github issue #14):
        // GeneralSupportTicket's owning sweep (SweepTicketsAsync) only ever queries
        // ExcludeHeldAsync/IsHeldAsync with RetentionSubjectKind.SupportTicket. A scope naming
        // CustomerId instead was previously accepted (201) and persisted, but could never be
        // found by that sweep - a silently inert hold that looked like real protection.
        await using var app = await TestApplication.CreateAsync();
        app.SetStaffSubject("officer-1");

        var response = await app.Client.PostAsJsonAsync(
            "/api/v1/retention/legal-holds",
            new CreateLegalHoldRequest("MATTER-1", "Dispute", DateTimeOffset.UtcNow.AddDays(90),
                [new LegalHoldScopeEntryRequest("GeneralSupportTicket", Guid.CreateVersion7(), null, null)]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("legal_hold_scope_subject_kind_mismatch", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReleasingAnAlreadyReleasedHoldIsIdempotentOverHttp()
    {
        await using var app = await TestApplication.CreateAsync();
        app.SetStaffSubject("officer-1");
        var createResponse = await app.Client.PostAsJsonAsync(
            "/api/v1/retention/legal-holds",
            new CreateLegalHoldRequest("MATTER-1", "Dispute", DateTimeOffset.UtcNow.AddDays(90),
                [new LegalHoldScopeEntryRequest("GeneralSupportTicket", null, null, Guid.CreateVersion7())]));
        var created = await createResponse.ReadAsAsync<LegalHoldResponse>();
        await app.Client.PostAsJsonAsync($"/api/v1/retention/legal-holds/{created!.Id}/release", new ReleaseLegalHoldRequest("First"));

        var secondRelease = await app.Client.PostAsJsonAsync(
            $"/api/v1/retention/legal-holds/{created.Id}/release", new ReleaseLegalHoldRequest("Second"));

        Assert.Equal(HttpStatusCode.OK, secondRelease.StatusCode);
    }

    [Fact]
    public async Task ReleasingAnUnknownHoldReturnsNotFound()
    {
        await using var app = await TestApplication.CreateAsync();
        app.SetStaffSubject("officer-1");

        var response = await app.Client.PostAsJsonAsync(
            $"/api/v1/retention/legal-holds/{Guid.CreateVersion7()}/release", new ReleaseLegalHoldRequest("Reason"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateAndReleaseAreRecordedInTheAuditTrailWithTheActingSubject()
    {
        await using var app = await TestApplication.CreateAsync();
        app.SetStaffSubject("officer-1");
        var createResponse = await app.Client.PostAsJsonAsync(
            "/api/v1/retention/legal-holds",
            new CreateLegalHoldRequest("MATTER-1", "Dispute", DateTimeOffset.UtcNow.AddDays(90),
                [new LegalHoldScopeEntryRequest("GeneralSupportTicket", null, null, Guid.CreateVersion7())]));
        var created = await createResponse.ReadAsAsync<LegalHoldResponse>();

        await using var scope = app.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RetentionDbContext>();
        var events = database.LegalHoldAuditEvents.Where(value => value.LegalHoldId == created!.Id).ToList();
        Assert.Contains(events, value => value.ActorSubject == "officer-1");
    }
}
