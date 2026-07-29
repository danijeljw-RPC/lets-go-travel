using ReadyToGoTravel.Booking.Idempotency;

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

        var first = await service.ExecuteAsync(customerId, "payment-session", "key-1", "hash-a", Run, default);
        fixture.Context.ChangeTracker.Clear();
        var replay = await service.ExecuteAsync(customerId, "payment-session", "key-1", "hash-a", Run, default);

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

        await service.ExecuteAsync(customerId, "book", "key-1", "hash-a", Run, default);
        var conflict = await service.ExecuteAsync(customerId, "book", "key-1", "hash-b", Run, default);

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
}
