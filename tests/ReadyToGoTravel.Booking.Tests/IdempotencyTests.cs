using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ReadyToGoTravel.Booking.Idempotency;
using ReadyToGoTravel.Booking.Persistence;

namespace ReadyToGoTravel.Booking.Tests;

public sealed class IdempotencyTests
{
    [Fact]
    public async Task SameKeyAndFingerprintReturnsRecordedResponseWithoutRunningActionAgain()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var service = fixture.Idempotency;
        var customerId = Guid.CreateVersion7();
        var executions = 0;
        Task<IdempotentResponse<string>> Run(CancellationToken _)
        {
            executions++;
            return Task.FromResult(IdempotentResponse.Completed(200, "payment-response"));
        }

        var first = await service.ExecuteAsync(
            customerId,
            "payment-session",
            "key-1",
            "hash-a",
            IdempotentResponse.InProgress(202, "payment-pending"),
            Run,
            default);
        fixture.Context.ChangeTracker.Clear();
        var replay = await service.ExecuteAsync(
            customerId,
            "payment-session",
            "key-1",
            "hash-a",
            IdempotentResponse.InProgress(202, "payment-pending"),
            Run,
            default);

        Assert.Equal(first.Value, replay.Value);
        Assert.Equal(IdempotencyOutcome.Completed, replay.Outcome);
        Assert.Equal(1, executions);
    }

    [Fact]
    public async Task SameKeyWithDifferentFingerprintConflicts()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var service = fixture.Idempotency;
        var customerId = Guid.CreateVersion7();
        Task<IdempotentResponse<string>> Run(CancellationToken _) =>
            Task.FromResult(IdempotentResponse.Completed(200, "booking-response"));

        await service.ExecuteAsync(
            customerId,
            "book",
            "key-1",
            "hash-a",
            IdempotentResponse.InProgress(202, "booking-pending"),
            Run,
            default);
        var conflict = await service.ExecuteAsync(
            customerId,
            "book",
            "key-1",
            "hash-b",
            IdempotentResponse.InProgress(202, "booking-pending"),
            Run,
            default);

        Assert.Equal(IdempotencyOutcome.Conflict, conflict.Outcome);
    }

    [Fact]
    public void CanonicalFingerprintIgnoresJsonPropertyOrder()
    {
        var first = IdempotencyFingerprint.FromJson("{\"operation\":\"book\",\"tripId\":\"trip-1\"}");
        var second = IdempotencyFingerprint.FromJson("{\"tripId\":\"trip-1\",\"operation\":\"book\"}");

        Assert.Equal("70B8ABB3DB87ED8F6A3F9377336EBA81A463FED19108AAE018ACADD23D6630C5", first);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task MatchingInProgressRequestReturnsPersistedCurrentResourceWithoutRunningAction()
    {
        await using var fixture = await SharedBookingDatabaseFixture.CreateAsync();
        var customerId = Guid.CreateVersion7();
        var currentResource = new CurrentResource("checkout-123", "payment_pending");
        var actionEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseAction = new TaskCompletionSource<IdempotentResponse<CurrentResource>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var firstExecutions = 0;
        var replayExecutions = 0;

        async Task<IdempotentResponse<CurrentResource>> FirstAction(CancellationToken cancellationToken)
        {
            firstExecutions++;
            actionEntered.SetResult();
            return await releaseAction.Task.WaitAsync(cancellationToken);
        }

        var first = fixture.FirstService.ExecuteAsync(
            customerId,
            "payment-session",
            "key-1",
            "hash-a",
            IdempotentResponse.InProgress(202, currentResource),
            FirstAction,
            default);
        await actionEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var replay = await fixture.SecondService.ExecuteAsync(
            customerId,
            "payment-session",
            "key-1",
            "hash-a",
            IdempotentResponse.InProgress(202, currentResource),
            _ =>
            {
                replayExecutions++;
                return Task.FromResult(IdempotentResponse.Completed(200, currentResource));
            },
            default);

        Assert.Equal(IdempotencyOutcome.InProgress, replay.Outcome);
        Assert.Equal(202, replay.StatusCode);
        Assert.Equal(currentResource, replay.Value);
        Assert.Equal(1, firstExecutions);
        Assert.Equal(0, replayExecutions);

        releaseAction.SetResult(IdempotentResponse.Completed(200, currentResource));
        await first;
    }

    [Fact]
    public async Task ConcurrentRecordCreationReplaysWinningInProgressResourceWithoutRunningLosingAction()
    {
        using var queryBarrier = new IdempotencyReadBarrier();
        await using var fixture = await SharedBookingDatabaseFixture.CreateAsync(queryBarrier);
        var customerId = Guid.CreateVersion7();
        var currentResource = new CurrentResource("checkout-456", "payment_pending");
        var actionEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseAction = new TaskCompletionSource<IdempotentResponse<CurrentResource>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var executions = 0;

        async Task<IdempotentResponse<CurrentResource>> Action(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref executions);
            actionEntered.TrySetResult();
            return await releaseAction.Task.WaitAsync(cancellationToken);
        }

        var first = Task.Run(() => fixture.FirstService.ExecuteAsync(
            customerId,
            "payment-session",
            "key-1",
            "hash-a",
            IdempotentResponse.InProgress(202, currentResource),
            Action,
            default));
        var second = Task.Run(() => fixture.SecondService.ExecuteAsync(
            customerId,
            "payment-session",
            "key-1",
            "hash-a",
            IdempotentResponse.InProgress(202, currentResource),
            Action,
            default));

        await actionEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var losingTask = await Task.WhenAny(first, second).WaitAsync(TimeSpan.FromSeconds(10));
        var replay = await losingTask;

        Assert.Equal(IdempotencyOutcome.InProgress, replay.Outcome);
        Assert.Equal(currentResource, replay.Value);
        Assert.Equal(1, executions);
        Assert.Equal(2, queryBarrier.CompletedNoRowSelects);
        Assert.Equal(2, queryBarrier.NoRowSelectsAtFirstInsert);
        Assert.Equal(2, queryBarrier.InsertAttempts);

        releaseAction.SetResult(IdempotentResponse.Completed(200, currentResource));
        var winningTask = ReferenceEquals(losingTask, first) ? second : first;
        var winner = await winningTask;
        Assert.Equal(IdempotencyOutcome.Completed, winner.Outcome);
        Assert.Equal(1, executions);
    }

    [Fact]
    public async Task StaleRetryInProgressCannotOverwriteCompletedReplay()
    {
        await using var fixture = await SharedBookingDatabaseFixture.CreateAsync();
        var customerId = Guid.CreateVersion7();
        var pending = new CurrentResource("checkout-race", "payment_pending");
        var completed = new CurrentResource("checkout-race", "captured");
        var staleActionEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStaleAction = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var stale = fixture.FirstService.ExecuteAsync(
            customerId,
            "payment-return",
            "same-key",
            "same-fingerprint",
            IdempotentResponse.InProgress(202, pending),
            async cancellationToken =>
            {
                staleActionEntered.SetResult();
                await releaseStaleAction.Task.WaitAsync(cancellationToken);
                return IdempotentResponse.InProgress(202, pending);
            },
            default);
        await staleActionEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var winner = await fixture.SecondService.ExecuteAsync(
            customerId,
            "payment-return",
            "same-key",
            "same-fingerprint",
            IdempotentResponse.InProgress(202, pending),
            _ => Task.FromResult(IdempotentResponse.Completed(200, completed)),
            default,
            retryInProgress: true);
        releaseStaleAction.SetResult();
        var staleResult = await stale;

        Assert.Equal(IdempotencyOutcome.Completed, winner.Outcome);
        Assert.Equal(completed, winner.Value);
        Assert.Equal(IdempotencyOutcome.Completed, staleResult.Outcome);
        Assert.Equal(completed, staleResult.Value);
        Assert.True(staleResult.IsReplay);

        await using var verification = new BookingDbContext(
            new DbContextOptionsBuilder<BookingDbContext>()
                .UseSqlite(fixture.ConnectionString)
                .Options);
        var record = await verification.IdempotencyRecords.SingleAsync();
        Assert.Equal("Completed", record.Status.ToString());
        Assert.Contains("captured", record.ResponseBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("pan")]
    [InlineData("cvv")]
    [InlineData("cvc")]
    [InlineData("sensitiveAuthenticationData")]
    [InlineData("passport")]
    [InlineData("identityDocument")]
    [InlineData("supplierCredentials")]
    [InlineData("apiKey")]
    [InlineData("reusablePaymentToken")]
    public async Task CompletedResponseRejectsNestedSensitiveValue(string sensitiveField)
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var actionExecutions = 0;
        var response = new Dictionary<string, object?>
        {
            ["resource"] = new Dictionary<string, string>
            {
                [sensitiveField] = "sensitive-value",
            },
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Idempotency.ExecuteAsync(
            Guid.CreateVersion7(),
            "payment-session",
            $"key-{sensitiveField}",
            "hash-a",
            IdempotentResponse.InProgress(202, new Dictionary<string, object?>
            {
                ["checkoutId"] = "checkout-789",
                ["paymentStatus"] = "payment_pending",
            }),
            _ =>
            {
                actionExecutions++;
                return Task.FromResult(IdempotentResponse.Completed(200, response));
            },
            default));

        Assert.Equal(1, actionExecutions);
    }

    [Fact]
    public async Task BenignCompanyNameFieldIsAllowed()
    {
        await using var fixture = await BookingDatabaseFixture.CreateAsync();
        var response = await fixture.Idempotency.ExecuteAsync(
            Guid.CreateVersion7(), "checkout", "company", "hash",
            IdempotentResponse.InProgress(202, new { companyName = "ReadyToGoTravel" }),
            _ => Task.FromResult(IdempotentResponse.Completed(200, new { companyName = "ReadyToGoTravel" })),
            default);
        Assert.Equal(IdempotencyOutcome.Completed, response.Outcome);
    }

    private sealed record CurrentResource(string CheckoutId, string PaymentStatus);
}

internal sealed class SharedBookingDatabaseFixture(
    string databasePath,
    BookingDbContext firstContext,
    BookingDbContext secondContext) : IAsyncDisposable
{
    public string ConnectionString => $"Data Source={databasePath};Foreign Keys=True;Default Timeout=30";

    public IIdempotencyService FirstService => new IdempotencyService(firstContext, TimeProvider.System);

    public IIdempotencyService SecondService => new IdempotencyService(secondContext, TimeProvider.System);

    public static async Task<SharedBookingDatabaseFixture> CreateAsync(IInterceptor? interceptor = null)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"ready-to-go-travel-booking-{Guid.CreateVersion7()}.db");
        var connectionString = $"Data Source={databasePath};Foreign Keys=True;Default Timeout=30";
        var setup = new BookingDbContext(CreateOptions(connectionString));
        await using (setup)
        {
            await setup.Database.EnsureCreatedAsync();
        }

        return new SharedBookingDatabaseFixture(
            databasePath,
            new BookingDbContext(CreateOptions(connectionString, interceptor)),
            new BookingDbContext(CreateOptions(connectionString, interceptor)));
    }

    public async ValueTask DisposeAsync()
    {
        await firstContext.DisposeAsync();
        await secondContext.DisposeAsync();
        File.Delete(databasePath);
    }

    private static DbContextOptions<BookingDbContext> CreateOptions(string connectionString, IInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<BookingDbContext>().UseSqlite(connectionString);
        if (interceptor is not null)
        {
            builder.AddInterceptors(interceptor);
        }

        return builder.Options;
    }
}

internal sealed class IdempotencyReadBarrier : DbCommandInterceptor, IDisposable
{
    private readonly Barrier barrier = new(2);
    private int completedNoRowSelects;
    private int yieldedNoRowSelects;
    private int noRowSelectsAtFirstInsert;
    private int insertAttempts;

    public int CompletedNoRowSelects => Volatile.Read(ref completedNoRowSelects);

    public int InsertAttempts => Volatile.Read(ref insertAttempts);

    public int NoRowSelectsAtFirstInsert => Volatile.Read(ref noRowSelectsAtFirstInsert);

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (IsIdempotencyInsert(command) && Interlocked.Increment(ref insertAttempts) == 1)
        {
            Volatile.Write(ref noRowSelectsAtFirstInsert, Volatile.Read(ref yieldedNoRowSelects));
        }

        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        if (IsIdempotencySelect(command) && !result.HasRows)
        {
            if (Interlocked.Increment(ref completedNoRowSelects) <= 2)
            {
                if (!barrier.SignalAndWait(TimeSpan.FromSeconds(10), cancellationToken))
                {
                    throw new TimeoutException("Both idempotency reads did not complete before inserts began.");
                }

                Interlocked.Increment(ref yieldedNoRowSelects);
                if (!barrier.SignalAndWait(TimeSpan.FromSeconds(10), cancellationToken))
                {
                    throw new TimeoutException("Both idempotency reads did not publish their no-row results.");
                }
            }
        }

        return await base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public void Dispose() => barrier.Dispose();

    private static bool IsIdempotencySelect(DbCommand command) =>
        command.CommandText.Contains("SELECT", StringComparison.OrdinalIgnoreCase) &&
        command.CommandText.Contains("idempotency_records", StringComparison.OrdinalIgnoreCase);

    private static bool IsIdempotencyInsert(DbCommand command) =>
        command.CommandText.Contains("INSERT", StringComparison.OrdinalIgnoreCase) &&
        command.CommandText.Contains("idempotency_records", StringComparison.OrdinalIgnoreCase);
}
