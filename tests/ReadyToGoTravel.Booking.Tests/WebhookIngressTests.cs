using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ReadyToGoTravel.Booking.Http;
using ReadyToGoTravel.Booking.Persistence;
using ReadyToGoTravel.Booking.Webhooks;

namespace ReadyToGoTravel.Booking.Tests;

public sealed class WebhookIngressTests
{
    private const string Secret = "sandbox-webhook-secret-with-enough-entropy";

    [Fact]
    public async Task DisabledWebhookIngressFailsClosed()
    {
        await using var application = await WebhookTestApplication.CreateAsync(enabled: false);

        var response = await application.PostAsync(ValidBody("evt-disabled"), Secret);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, await application.CountInboxAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("incorrect-secret")]
    public async Task MissingOrInvalidSecretIsRejectedBeforePersistence(string? secret)
    {
        await using var application = await WebhookTestApplication.CreateAsync();

        var response = await application.PostAsync(ValidBody("evt-auth"), secret);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, await application.CountInboxAsync());
    }

    [Fact]
    public async Task PreviousRotationSecretIsAcceptedWithoutBeingPersisted()
    {
        await using var application = await WebhookTestApplication.CreateAsync(
            previousSecret: "previous-webhook-secret-with-enough-entropy");

        var response = await application.PostAsync(
            ValidBody("evt-rotation"),
            "previous-webhook-secret-with-enough-entropy");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var item = await application.SingleInboxAsync();
        Assert.DoesNotContain("secret", item.RawBody, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("client-correlation", item.CorrelationId);
    }

    [Fact]
    public async Task EnvironmentMismatchIsRejectedBeforePersistence()
    {
        await using var application = await WebhookTestApplication.CreateAsync();

        var response = await application.PostAsync(
            ValidBody("evt-environment", sandbox: false),
            Secret);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await application.CountInboxAsync());
    }

    [Fact]
    public async Task AuthenticatedEventIsDurableBeforeAcceptedResponseAndIdenticalDuplicateIsHarmless()
    {
        await using var application = await WebhookTestApplication.CreateAsync();
        var body = ValidBody("evt-duplicate");

        var first = await application.PostAsync(body, Secret);
        var duplicate = await application.PostAsync(body, Secret);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, duplicate.StatusCode);
        Assert.Equal(1, await application.CountInboxAsync());
        var item = await application.SingleInboxAsync();
        Assert.Equal(WebhookInboxStatus.Pending, item.Status);
        Assert.Equal("LiteAPI", item.Provider);
        Assert.Equal("Sandbox", item.Environment);
        Assert.Equal("evt-duplicate", item.EventId);
        Assert.Equal(64, item.PayloadHash.Length);
    }

    [Fact]
    public async Task ReusedEventIdentityWithDifferentBodyIsRejectedAndQuarantined()
    {
        await using var application = await WebhookTestApplication.CreateAsync();

        var first = await application.PostAsync(ValidBody("evt-conflict"), Secret);
        var conflict = await application.PostAsync(
            ValidBody("evt-conflict").Replace("booking.book", "booking.cancel", StringComparison.Ordinal),
            Secret);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(WebhookInboxStatus.Quarantined, (await application.SingleInboxAsync()).Status);
    }

    [Fact]
    public async Task NonJsonAndOversizedBodiesAreRejected()
    {
        await using var application = await WebhookTestApplication.CreateAsync(maxBodyBytes: 128);

        var nonJson = await application.PostAsync("not-json", Secret, "text/plain");
        var oversized = await application.PostAsync(
            ValidBody("evt-large") + new string(' ', 256),
            Secret);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, nonJson.StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversized.StatusCode);
        Assert.Equal(0, await application.CountInboxAsync());
    }

    [Fact]
    public async Task ConcurrentIdenticalInsertRaceConvergesToOneInboxItem()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"ready-to-go-travel-webhook-race-{Guid.CreateVersion7():N}.db");
        var connectionString = $"Data Source={databasePath}";
        var baseOptions = new DbContextOptionsBuilder<BookingDbContext>()
            .UseSqlite(connectionString)
            .Options;
        var body = ValidBody("evt-race");
        var input = new WebhookEnvelopeInput(
            "Sandbox",
            "evt-race",
            "booking.book",
            body,
            true,
            "correlation-race");

        try
        {
            await using (var setup = new BookingDbContext(baseOptions))
            {
                await setup.Database.EnsureCreatedAsync();
            }

            var interceptor = new CompetingWebhookInsertInterceptor(baseOptions, input);
            var racingOptions = new DbContextOptionsBuilder<BookingDbContext>()
                .UseSqlite(connectionString)
                .AddInterceptors(interceptor)
                .Options;
            await using var context = new BookingDbContext(racingOptions);
            var service = new WebhookInboxService(context, TimeProvider.System);

            var outcome = await service.AcceptAsync(input, default);

            Assert.Equal(WebhookAcceptanceOutcome.Duplicate, outcome);
            await using var verification = new BookingDbContext(baseOptions);
            Assert.Equal(1, await verification.WebhookInbox.CountAsync());
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    private static string ValidBody(string eventId, bool sandbox = true) => $$"""
        {
          "event_id": "{{eventId}}",
          "event_name": "booking.book",
          "request": "{\"booking_id\":\"hotel_123\"}",
          "response": "{\"booking_id\":\"hotel_123\"}",
          "sandbox": {{sandbox.ToString().ToLowerInvariant()}}
        }
        """;

    private sealed class CompetingWebhookInsertInterceptor(
        DbContextOptions<BookingDbContext> options,
        WebhookEnvelopeInput input) : SaveChangesInterceptor
    {
        private bool inserted;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (inserted)
            {
                return result;
            }

            inserted = true;
            var now = TimeProvider.System.GetUtcNow().ToUniversalTime();
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input.RawBody)));
            await using var competitor = new BookingDbContext(options);
            competitor.WebhookInbox.Add(new WebhookInboxItem(
                Guid.CreateVersion7(now),
                input.Environment,
                input.EventId,
                input.EventName,
                input.RawBody,
                hash,
                input.Sandbox,
                input.CorrelationId,
                now));
            await competitor.SaveChangesAsync(cancellationToken);
            return result;
        }
    }

    private sealed class WebhookTestApplication(
        WebApplication application,
        SqliteConnection connection) : IAsyncDisposable
    {
        public HttpClient Client { get; } = application.GetTestClient();

        public static async Task<WebhookTestApplication> CreateAsync(
            bool enabled = true,
            string? previousSecret = null,
            int maxBodyBytes = 1024)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter(
                "webhook",
                limiter =>
                {
                    limiter.PermitLimit = 20;
                    limiter.Window = TimeSpan.FromMinutes(1);
                    limiter.QueueLimit = 0;
                }));
            builder.Services.AddBookingModule((_, options) => options.UseSqlite(connection));
            builder.Services.Configure<LiteApiWebhookOptions>(options =>
            {
                options.Enabled = enabled;
                options.Environment = "Sandbox";
                options.CurrentSecret = Secret;
                options.PreviousSecret = previousSecret;
                options.MaximumBodyBytes = maxBodyBytes;
            });
            var application = builder.Build();
            application.UseRateLimiter();
            application.MapGroup("/api/v1").MapWebhookEndpoints();
            await application.StartAsync();
            await using (var scope = application.Services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<BookingDbContext>().Database.EnsureCreatedAsync();
            }

            return new WebhookTestApplication(application, connection);
        }

        public async Task<HttpResponseMessage> PostAsync(
            string body,
            string? secret,
            string contentType = "application/json")
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "/api/v1/webhooks/liteapi/sandbox");
            request.Content = new StringContent(body, Encoding.UTF8, contentType);
            request.Headers.TryAddWithoutValidation("authorization", secret);
            request.Headers.TryAddWithoutValidation("X-Correlation-ID", "client-correlation");
            return await Client.SendAsync(request);
        }

        public async Task<int> CountInboxAsync()
        {
            await using var scope = application.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<BookingDbContext>()
                .WebhookInbox.CountAsync();
        }

        public async Task<WebhookInboxItem> SingleInboxAsync()
        {
            await using var scope = application.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<BookingDbContext>()
                .WebhookInbox.AsNoTracking().SingleAsync();
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await application.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
