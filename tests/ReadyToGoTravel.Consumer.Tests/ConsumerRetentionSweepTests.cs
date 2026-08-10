using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Consumer.Customers;
using ReadyToGoTravel.Consumer.Persistence;
using ReadyToGoTravel.Consumer.Retention;
using ReadyToGoTravel.Retention;
using ReadyToGoTravel.Retention.Application;
using ReadyToGoTravel.Retention.Domain;

namespace ReadyToGoTravel.Consumer.Tests;

public sealed class ConsumerRetentionSweepTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 10, 0, 0, TimeSpan.Zero);
    private static readonly RetentionSweepOptionsWrapper Enabled = new(true);

    [Fact]
    public async Task ClosedAccountPast90DaysWithNoProtectedEvidenceIsMinimised()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var customerId = await CreateClosedCustomerAsync(fixture.Context, "sub-1", Now.AddDays(-91));
        var processor = CreateProcessor(fixture.Context, [], new NoHoldGuard(), new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.True(didWork);
        var customer = await fixture.Context.Customers.AsNoTracking().SingleAsync(value => value.Id == customerId);
        Assert.Equal("en-AU", customer.PreferredLocale);
        Assert.Equal("AUD", customer.DisplayCurrency);
        Assert.Equal(CustomerStatus.Closed, customer.Status);
    }

    [Fact]
    public async Task ClosedAccountWithin90DaysIsUntouched()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await CreateClosedCustomerAsync(fixture.Context, "sub-1", Now.AddDays(-89));
        var processor = CreateProcessor(fixture.Context, [], new NoHoldGuard(), new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.False(didWork);
    }

    [Fact]
    public async Task StillActiveAccountIsNeverSweptRegardlessOfAge()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var customer = Customer.Create("sub-1", "en-AU", true, new FixedTimeProvider(Now.AddYears(-1))).Value!;
        fixture.Context.Customers.Add(customer);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        var processor = CreateProcessor(fixture.Context, [], new NoHoldGuard(), new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.False(didWork);
    }

    [Fact]
    public async Task ClosedAccountWithProtectedBookingEvidenceIsNotMinimised()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var customerId = await CreateClosedCustomerAsync(fixture.Context, "sub-1", Now.AddDays(-91));
        var evidencePort = new FakeEvidencePort(hasEvidence: true);
        var processor = CreateProcessor(fixture.Context, [evidencePort], new NoHoldGuard(), new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.False(didWork);
        var customer = await fixture.Context.Customers.AsNoTracking().SingleAsync(value => value.Id == customerId);
        Assert.Equal("NZD", customer.DisplayCurrency);
    }

    [Fact]
    public async Task ClosedAccountWithNoProtectedEvidenceAcrossMultiplePortsIsMinimised()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var customerId = await CreateClosedCustomerAsync(fixture.Context, "sub-1", Now.AddDays(-91));
        var bookingPort = new FakeEvidencePort(hasEvidence: false);
        var supportPort = new FakeEvidencePort(hasEvidence: false);
        var processor = CreateProcessor(fixture.Context, [bookingPort, supportPort], new NoHoldGuard(), new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.True(didWork);
        var customer = await fixture.Context.Customers.AsNoTracking().SingleAsync(value => value.Id == customerId);
        Assert.Equal("en-AU", customer.PreferredLocale);
    }

    [Fact]
    public async Task ActiveLegalHoldProtectsTheAccountFromMinimisation()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var customerId = await CreateClosedCustomerAsync(fixture.Context, "sub-1", Now.AddDays(-91));
        var guard = new SelectiveHoldGuard(customerId);
        var processor = CreateProcessor(fixture.Context, [], guard, new RecordingReceiptRecorder(), Now);

        var didWork = await processor.ProcessCycleAsync();

        Assert.False(didWork);
    }

    [Fact]
    public async Task RunningTheSweepTwiceIsIdempotent()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await CreateClosedCustomerAsync(fixture.Context, "sub-1", Now.AddDays(-91));
        var processor = CreateProcessor(fixture.Context, [], new NoHoldGuard(), new RecordingReceiptRecorder(), Now);

        await processor.ProcessCycleAsync();
        var secondRun = await processor.ProcessCycleAsync();

        Assert.False(secondRun);
    }

    [Fact]
    public async Task DisabledFlagMakesEveryProcessCycleANoOp()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await CreateClosedCustomerAsync(fixture.Context, "sub-1", Now.AddYears(-1));
        var processor = new ConsumerRetentionSweepProcessor(
            fixture.Context, [], new NoHoldGuard(), new RecordingReceiptRecorder(),
            new FixedTimeProvider(Now), new RetentionSweepOptionsWrapper(false));

        var didWork = await processor.ProcessCycleAsync();

        Assert.False(didWork);
    }

    private static ConsumerRetentionSweepProcessor CreateProcessor(
        ConsumerDbContext database,
        IEnumerable<IConsumerRetentionEvidencePort> ports,
        ILegalHoldGuard guard,
        IRetentionReceiptRecorder recorder,
        DateTimeOffset now) =>
        new(database, ports, guard, recorder, new FixedTimeProvider(now), Enabled);

    private static async Task<Guid> CreateClosedCustomerAsync(ConsumerDbContext database, string subject, DateTimeOffset closedAt)
    {
        var customer = Customer.Create(subject, "en-AU", true, new FixedTimeProvider(closedAt.AddDays(-1))).Value!;
        customer.Close(new FixedTimeProvider(closedAt));
        database.Customers.Add(customer);
        await database.SaveChangesAsync();
        // Force the preference fields away from their defaults so the minimisation candidacy
        // predicate (PreferredLocale != Default || DisplayCurrency != "AUD") is exercised
        // meaningfully by these tests rather than the row already looking pre-minimised.
        await database.Customers.Where(value => value.Id == customer.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.DisplayCurrency, "NZD"));
        database.ChangeTracker.Clear();
        return customer.Id;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed record RetentionSweepOptionsWrapper(bool Enabled) : Microsoft.Extensions.Options.IOptions<RetentionSweepOptions>
    {
        public RetentionSweepOptions Value { get; } = new() { Enabled = Enabled };
    }

    private sealed class NoHoldGuard : ILegalHoldGuard
    {
        public Task<IReadOnlySet<Guid>> ExcludeHeldAsync(RetentionRecordClass recordClass, RetentionSubjectKind subjectKind, IReadOnlyCollection<Guid> candidateSubjectIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());

        public Task<bool> IsHeldAsync(RetentionRecordClass recordClass, RetentionSubjectKind subjectKind, Guid subjectId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class SelectiveHoldGuard(Guid heldId) : ILegalHoldGuard
    {
        public Task<IReadOnlySet<Guid>> ExcludeHeldAsync(RetentionRecordClass recordClass, RetentionSubjectKind subjectKind, IReadOnlyCollection<Guid> candidateSubjectIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<Guid>>(candidateSubjectIds.Contains(heldId) ? new HashSet<Guid> { heldId } : new HashSet<Guid>());

        public Task<bool> IsHeldAsync(RetentionRecordClass recordClass, RetentionSubjectKind subjectKind, Guid subjectId, CancellationToken cancellationToken = default) =>
            Task.FromResult(subjectId == heldId);
    }

    private sealed class FakeEvidencePort(bool hasEvidence) : IConsumerRetentionEvidencePort
    {
        public Task<bool> HasProtectedEvidenceAsync(Guid customerId, string subject, CancellationToken cancellationToken = default) =>
            Task.FromResult(hasEvidence);
    }

    private sealed class RecordingReceiptRecorder : IRetentionReceiptRecorder
    {
        public Task RecordAsync(RetentionRecordClass recordClass, int policyVersion, string action, int successCount, int failureCount, DateTimeOffset completedAtUtc, string? failureSummary, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RecordOperationalFailureAsync(RetentionRecordClass recordClass, string scope, string reason, DateTimeOffset nowUtc, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class DatabaseFixture(SqliteConnection connection, ConsumerDbContext context) : IAsyncDisposable
    {
        public ConsumerDbContext Context { get; } = context;

        public static async Task<DatabaseFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ConsumerDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new ConsumerDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new DatabaseFixture(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
