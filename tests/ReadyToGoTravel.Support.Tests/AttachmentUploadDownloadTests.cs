using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Support.Application;
using ReadyToGoTravel.Support.Domain;
using ReadyToGoTravel.Support.Persistence;

namespace ReadyToGoTravel.Support.Tests;

public sealed class AttachmentUploadDownloadTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task UploadingAValidSmallPdfSucceedsAndCreatesAPendingRow()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var (ticketId, messageId) = await CreateTicketAndMessageAsync(fixture);
        var storage = new InMemoryObjectStorage();
        var service = new SupportAttachmentService(fixture.Context, storage, new FixedTimeProvider(Now));

        var result = await service.UploadAsync(
            ticketId, messageId, "sub-1", null, "receipt.pdf", "application/pdf",
            new MemoryStream("%PDF-1.7 minimal file"u8.ToArray()));

        var accepted = Assert.IsType<SupportAttachmentUploadResult.Accepted>(result);
        Assert.Equal(AttachmentScanStatus.Pending, accepted.Attachment.ScanStatus);
        Assert.True(storage.Contains(accepted.Attachment.StorageKey));
    }

    [Fact]
    public async Task UploadingAFileOverTheSingleFileLimitIsRejectedBeforeAnyStorageCall()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var (ticketId, messageId) = await CreateTicketAndMessageAsync(fixture);
        var storage = new InMemoryObjectStorage();
        var service = new SupportAttachmentService(fixture.Context, storage, new FixedTimeProvider(Now));
        var oversized = new byte[SupportAttachmentService.MaxFileBytes + 1];

        var result = await service.UploadAsync(
            ticketId, messageId, "sub-1", null, "large.pdf", "application/pdf", new MemoryStream(oversized));

        var rejected = Assert.IsType<SupportAttachmentUploadResult.Rejected>(result);
        Assert.Equal("attachment_too_large", rejected.Reason);
        Assert.Empty(fixture.Context.Attachments);
    }

    [Fact]
    public async Task UploadingASixthFileToTheSameMessageIsRejected()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var (ticketId, messageId) = await CreateTicketAndMessageAsync(fixture);
        var storage = new InMemoryObjectStorage();
        var service = new SupportAttachmentService(fixture.Context, storage, new FixedTimeProvider(Now));

        for (var i = 0; i < SupportAttachmentService.MaxFilesPerMessage; i++)
        {
            var result = await service.UploadAsync(
                ticketId, messageId, "sub-1", null, $"file-{i}.txt", "text/plain", new MemoryStream("hello"u8.ToArray()));
            Assert.IsType<SupportAttachmentUploadResult.Accepted>(result);
        }

        var overflow = await service.UploadAsync(
            ticketId, messageId, "sub-1", null, "one-too-many.txt", "text/plain", new MemoryStream("hello"u8.ToArray()));

        var rejected = Assert.IsType<SupportAttachmentUploadResult.Rejected>(overflow);
        Assert.Equal("attachment_message_limit_exceeded", rejected.Reason);
    }

    [Fact]
    public async Task UploadingAFileThatWouldExceedTheTicketCumulativeLimitIsRejected()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var ticket = SupportTicket.Create("sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help", Now);
        fixture.Context.Tickets.Add(ticket);
        await fixture.Context.SaveChangesAsync();
        var storage = new InMemoryObjectStorage();
        var service = new SupportAttachmentService(fixture.Context, storage, new FixedTimeProvider(Now));

        // Five files just under the 10 MiB single-file cap, each in its own message, total ~45 MiB (under the 50 MiB ticket cap).
        var nineMebibytes = Combine("%PDF-"u8.ToArray(), new byte[9 * 1024 * 1024]);
        for (var i = 0; i < 5; i++)
        {
            var reply = ticket.Reply(SupportAuthorType.Support, "staff-1", $"Attachment slot {i}", Now.AddMinutes(i + 1));
            await fixture.Context.SaveChangesAsync();
            var result = await service.UploadAsync(
                ticket.Id, reply.Id, "sub-1", null, $"file-{i}.pdf", "application/pdf", new MemoryStream(nineMebibytes));
            Assert.IsType<SupportAttachmentUploadResult.Accepted>(result);
        }

        var lastMessage = ticket.Reply(SupportAuthorType.Support, "staff-1", "One more", Now.AddMinutes(10));
        await fixture.Context.SaveChangesAsync();
        var overflow = await service.UploadAsync(
            ticket.Id, lastMessage.Id, "sub-1", null, "overflow.pdf", "application/pdf",
            new MemoryStream(Combine("%PDF-"u8.ToArray(), new byte[6 * 1024 * 1024])));

        var rejected = Assert.IsType<SupportAttachmentUploadResult.Rejected>(overflow);
        Assert.Equal("attachment_ticket_limit_exceeded", rejected.Reason);
    }

    [Fact]
    public async Task TheStorageKeyNeverContainsTheOriginalFileName()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var (ticketId, messageId) = await CreateTicketAndMessageAsync(fixture);
        var storage = new InMemoryObjectStorage();
        var service = new SupportAttachmentService(fixture.Context, storage, new FixedTimeProvider(Now));

        var result = await service.UploadAsync(
            ticketId, messageId, "sub-1", null, "../../etc/passwd.pdf", "application/pdf",
            new MemoryStream("%PDF-1.7"u8.ToArray()));

        var accepted = Assert.IsType<SupportAttachmentUploadResult.Accepted>(result);
        Assert.Equal($"support/{ticketId:D}/{accepted.Attachment.Id:D}.pdf", accepted.Attachment.StorageKey);
        Assert.DoesNotContain("passwd", accepted.Attachment.StorageKey);
        Assert.DoesNotContain("..", accepted.Attachment.StorageKey);
    }

    [Fact]
    public async Task DownloadUrlIsNullWhileScanIsPending()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var (ticketId, messageId) = await CreateTicketAndMessageAsync(fixture);
        var storage = new InMemoryObjectStorage();
        var service = new SupportAttachmentService(fixture.Context, storage, new FixedTimeProvider(Now));
        var uploaded = (SupportAttachmentUploadResult.Accepted)await service.UploadAsync(
            ticketId, messageId, "sub-1", null, "receipt.pdf", "application/pdf", new MemoryStream("%PDF-1.7"u8.ToArray()));

        var url = await service.CreateDownloadUrlAsync(ticketId, uploaded.Attachment.Id);

        Assert.Null(url);
    }

    [Fact]
    public async Task DownloadUrlIsReturnedOnlyAfterTheAttachmentIsMarkedClean()
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var (ticketId, messageId) = await CreateTicketAndMessageAsync(fixture);
        var storage = new InMemoryObjectStorage();
        var service = new SupportAttachmentService(fixture.Context, storage, new FixedTimeProvider(Now));
        var uploaded = (SupportAttachmentUploadResult.Accepted)await service.UploadAsync(
            ticketId, messageId, "sub-1", null, "receipt.pdf", "application/pdf", new MemoryStream("%PDF-1.7"u8.ToArray()));

        var attachment = fixture.Context.Attachments.Single(value => value.Id == uploaded.Attachment.Id);
        attachment.MarkClean();
        await fixture.Context.SaveChangesAsync();

        var url = await service.CreateDownloadUrlAsync(ticketId, uploaded.Attachment.Id);

        Assert.NotNull(url);
        Assert.Single(storage.DownloadUrlRequests);
        Assert.True(storage.DownloadUrlRequests[0].ValidFor <= TimeSpan.FromMinutes(5));
    }

    [Theory]
    [InlineData(AttachmentScanStatus.Infected)]
    [InlineData(AttachmentScanStatus.Failed)]
    public async Task DownloadUrlIsNullForInfectedOrFailedAttachments(AttachmentScanStatus status)
    {
        await using var fixture = await SupportDatabaseFixture.CreateAsync();
        var (ticketId, messageId) = await CreateTicketAndMessageAsync(fixture);
        var storage = new InMemoryObjectStorage();
        var service = new SupportAttachmentService(fixture.Context, storage, new FixedTimeProvider(Now));
        var uploaded = (SupportAttachmentUploadResult.Accepted)await service.UploadAsync(
            ticketId, messageId, "sub-1", null, "receipt.pdf", "application/pdf", new MemoryStream("%PDF-1.7"u8.ToArray()));
        var attachment = fixture.Context.Attachments.Single(value => value.Id == uploaded.Attachment.Id);
        if (status == AttachmentScanStatus.Infected)
        {
            attachment.MarkInfected();
        }
        else
        {
            attachment.MarkFailed();
        }

        await fixture.Context.SaveChangesAsync();

        Assert.Null(await service.CreateDownloadUrlAsync(ticketId, uploaded.Attachment.Id));
    }

    [Fact]
    public async Task AFailureAfterAStorageWriteDeletesTheOrphanedObjectAndPropagatesTheOriginalException()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var context = new SupportDbContext(new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var ticket = SupportTicket.Create("sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help", Now);
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        var storage = new DisposesConnectionAfterPutObjectStorage(connection);
        var service = new SupportAttachmentService(context, storage, new FixedTimeProvider(Now));

        await Assert.ThrowsAnyAsync<Exception>(() => service.UploadAsync(
            ticket.Id, ticket.Messages[0].Id, "sub-1", null, "receipt.pdf", "application/pdf",
            new MemoryStream("%PDF-1.7"u8.ToArray())));

        Assert.Single(storage.DeletedKeys);
        Assert.Empty(storage.Contents);
    }

    [Fact]
    public async Task ACompensatingDeleteAfterRequestCancellationStillDeletesTheOrphanedObject()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var context = new SupportDbContext(new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var ticket = SupportTicket.Create("sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help", Now);
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        // Simulates the request being cancelled (e.g. the client disconnected) at the exact moment
        // between the storage write succeeding and the database work that follows it - the request
        // token used for everything after PutAsync is already cancelled by the time the failure is
        // caught and compensating cleanup runs.
        using var cts = new CancellationTokenSource();
        var storage = new CancelsAfterPutObjectStorage(cts);
        var service = new SupportAttachmentService(context, storage, new FixedTimeProvider(Now));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.UploadAsync(
            ticket.Id, ticket.Messages[0].Id, "sub-1", null, "receipt.pdf", "application/pdf",
            new MemoryStream("%PDF-1.7"u8.ToArray()), cts.Token));

        Assert.Single(storage.DeletedKeys);
        Assert.Empty(storage.Contents);
        Assert.False(storage.DeleteCalledWithCancellationRequested);
    }

    [Fact]
    public async Task ConcurrentUploadsToTheSameMessageNeverExceedTheFileLimit()
    {
        var connectionString = $"Data Source=file:attachment-race-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var keepAlive = new SqliteConnection(connectionString);
        await keepAlive.OpenAsync();
        await using (var setupContext = CreateContext(connectionString))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        Guid ticketId;
        Guid messageId;
        await using (var setupContext = CreateContext(connectionString))
        {
            var ticket = SupportTicket.Create("sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help", Now);
            setupContext.Tickets.Add(ticket);
            await setupContext.SaveChangesAsync();
            ticketId = ticket.Id;
            messageId = ticket.Messages[0].Id;
        }

        var storage = new InMemoryObjectStorage();

        async Task<SupportAttachmentUploadResult> UploadWithFreshContextAsync(int index)
        {
            await using var context = CreateContext(connectionString);
            var service = new SupportAttachmentService(context, storage, new FixedTimeProvider(Now));
            return await service.UploadAsync(
                ticketId, messageId, "sub-1", null, $"file-{index}.txt", "text/plain", new MemoryStream("hello"u8.ToArray()));
        }

        var results = await Task.WhenAll(Enumerable.Range(0, SupportAttachmentService.MaxFilesPerMessage + 3)
            .Select(index => Task.Run(() => UploadWithFreshContextAsync(index))));

        var acceptedCount = results.Count(result => result is SupportAttachmentUploadResult.Accepted);
        Assert.Equal(SupportAttachmentService.MaxFilesPerMessage, acceptedCount);

        await using var verifyContext = CreateContext(connectionString);
        var storedCount = await verifyContext.Attachments.CountAsync(value => value.MessageId == messageId);
        Assert.Equal(SupportAttachmentService.MaxFilesPerMessage, storedCount);
    }

    private static SupportDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(connectionString).Options);

    private static async Task<(Guid TicketId, Guid MessageId)> CreateTicketAndMessageAsync(SupportDatabaseFixture fixture)
    {
        var ticket = SupportTicket.Create("sub-1", "Ari", "ari@example.test", SupportTicketCategory.General, null, "Help", Now);
        fixture.Context.Tickets.Add(ticket);
        await fixture.Context.SaveChangesAsync();
        return (ticket.Id, ticket.Messages[0].Id);
    }

    private static byte[] Combine(byte[] first, byte[] second)
    {
        var result = new byte[first.Length + second.Length];
        Buffer.BlockCopy(first, 0, result, 0, first.Length);
        Buffer.BlockCopy(second, 0, result, first.Length, second.Length);
        return result;
    }
}

// Simulates a database becoming unavailable immediately after a successful storage write, so
// tests can assert that the orphaned object is cleaned up and the original exception propagates.
internal sealed class DisposesConnectionAfterPutObjectStorage(SqliteConnection connection) : ReadyToGoTravel.Support.Storage.IObjectStorage
{
    public Dictionary<string, byte[]> Contents { get; } = [];

    public List<string> DeletedKeys { get; } = [];

    public Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        Contents[key] = buffer.ToArray();
        connection.Dispose();
        return Task.CompletedTask;
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(new MemoryStream(Contents[key], writable: false));

    public Task<Uri> CreateDownloadUrlAsync(string key, TimeSpan validFor, CancellationToken cancellationToken = default) =>
        Task.FromResult(new Uri($"https://storage.test/{key}"));

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        Contents.Remove(key);
        DeletedKeys.Add(key);
        return Task.CompletedTask;
    }
}

// Simulates a request that is cancelled at the exact moment its storage write succeeds, so tests
// can assert compensating cleanup still runs (and is not itself handed the now-cancelled token).
internal sealed class CancelsAfterPutObjectStorage(CancellationTokenSource cancelAfterPut) : ReadyToGoTravel.Support.Storage.IObjectStorage
{
    public Dictionary<string, byte[]> Contents { get; } = [];

    public List<string> DeletedKeys { get; } = [];

    public bool DeleteCalledWithCancellationRequested { get; private set; }

    public Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        Contents[key] = buffer.ToArray();
        cancelAfterPut.Cancel();
        return Task.CompletedTask;
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(new MemoryStream(Contents[key], writable: false));

    public Task<Uri> CreateDownloadUrlAsync(string key, TimeSpan validFor, CancellationToken cancellationToken = default) =>
        Task.FromResult(new Uri($"https://storage.test/{key}"));

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        DeleteCalledWithCancellationRequested = cancellationToken.IsCancellationRequested;
        Contents.Remove(key);
        DeletedKeys.Add(key);
        return Task.CompletedTask;
    }
}
