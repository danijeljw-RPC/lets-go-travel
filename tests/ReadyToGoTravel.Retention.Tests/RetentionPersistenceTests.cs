using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Retention.Application;
using ReadyToGoTravel.Retention.Domain;

namespace ReadyToGoTravel.Retention.Tests;

public sealed class RetentionPersistenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ADbLevelCheckConstraintRejectsAScopeWithZeroSubjectKeys()
    {
        await using var fixture = await RetentionDatabaseFixture.CreateAsync();
        var service = new LegalHoldService(fixture.Context, new FixedTimeProvider(Now));
        var hold = await service.OpenAsync(
            "MATTER-1", "Dispute", "officer-1", Now.AddDays(90),
            [new LegalHoldScopeRequest(RetentionRecordClass.GeneralSupportTicket, Guid.CreateVersion7(Now), null, null)],
            default);

        // Bypass the domain-level guard entirely to prove the database itself enforces the
        // invariant (defence in depth) - insert a scope row with no subject key via raw SQL. Raw
        // SQL execution is not wrapped by SaveChanges, so the provider exception (SqliteException
        // here; a PostgresException against real PostgreSQL) surfaces directly rather than as
        // DbUpdateException.
        await Assert.ThrowsAsync<SqliteException>(async () =>
        {
            await fixture.Context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO legal_hold_scopes (id, legal_hold_id, record_class, customer_id, component_booking_id, support_ticket_id)
                VALUES ({Guid.CreateVersion7(Now)}, {hold.Id}, 'GeneralSupportTicket', NULL, NULL, NULL)
                """);
        });
    }

    [Fact]
    public async Task RetentionOperationalCaseDedupeKeyIsUnique()
    {
        await using var fixture = await RetentionDatabaseFixture.CreateAsync();
        var entity = fixture.Context.Model.FindEntityType(typeof(RetentionOperationalCase))!;
        var index = entity.GetIndexes().Single(value =>
            value.Properties.Select(property => property.Name).SequenceEqual([nameof(RetentionOperationalCase.DedupeKey)]));

        Assert.True(index.IsUnique);
    }

    [Fact]
    public async Task DirectUpdateOfALegalHoldAuditEventThroughEfIsRejected()
    {
        await using var fixture = await RetentionDatabaseFixture.CreateAsync();
        var service = new LegalHoldService(fixture.Context, new FixedTimeProvider(Now));
        await service.OpenAsync(
            "MATTER-1", "Dispute", "officer-1", Now.AddDays(90),
            [new LegalHoldScopeRequest(RetentionRecordClass.GeneralSupportTicket, Guid.CreateVersion7(Now), null, null)],
            default);

        var auditEvent = await fixture.Context.LegalHoldAuditEvents.SingleAsync();
        fixture.Context.Entry(auditEvent).Property("Detail").CurrentValue = "Tampered";
        fixture.Context.Entry(auditEvent).State = EntityState.Modified;

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task DirectDeleteOfALegalHoldAuditEventThroughEfIsRejected()
    {
        await using var fixture = await RetentionDatabaseFixture.CreateAsync();
        var service = new LegalHoldService(fixture.Context, new FixedTimeProvider(Now));
        await service.OpenAsync(
            "MATTER-1", "Dispute", "officer-1", Now.AddDays(90),
            [new LegalHoldScopeRequest(RetentionRecordClass.GeneralSupportTicket, Guid.CreateVersion7(Now), null, null)],
            default);

        var auditEvent = await fixture.Context.LegalHoldAuditEvents.SingleAsync();
        fixture.Context.LegalHoldAuditEvents.Remove(auditEvent);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task DirectUpdateOfARetentionDeletionReceiptThroughEfIsRejected()
    {
        await using var fixture = await RetentionDatabaseFixture.CreateAsync();
        var service = new LegalHoldService(fixture.Context, new FixedTimeProvider(Now));
        await service.RecordAsync(RetentionRecordClass.SupportAttachment, 1, "Delete", 1, 0, Now, null, default);

        var receipt = await fixture.Context.RetentionDeletionReceipts.SingleAsync();
        fixture.Context.Entry(receipt).Property("SuccessCount").CurrentValue = 999;
        fixture.Context.Entry(receipt).State = EntityState.Modified;

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Context.SaveChangesAsync());
    }
}
