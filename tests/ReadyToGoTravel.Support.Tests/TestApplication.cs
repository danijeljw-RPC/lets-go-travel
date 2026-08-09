using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReadyToGoTravel.Support.Guest;
using ReadyToGoTravel.Support.Http;
using ReadyToGoTravel.Support.Notifications;
using ReadyToGoTravel.Support.Persistence;
using ReadyToGoTravel.Support.Scanning;
using ReadyToGoTravel.Support.Storage;

namespace ReadyToGoTravel.Support.Tests;

internal sealed class TestApplication : IAsyncDisposable
{
    private static readonly string[] NoLimitPolicies = ["support", "support-guest", "support-ticket-create"];

    private readonly WebApplication application;
    private readonly string databasePath;

    private TestApplication(WebApplication application, string databasePath, HttpClient client)
    {
        this.application = application;
        this.databasePath = databasePath;
        Client = client;
    }

    public HttpClient Client { get; }

    public InMemoryObjectStorage Storage { get; private set; } = null!;

    public RecordingAttachmentScanner Scanner { get; private set; } = null!;

    public RecordingSupportNotificationSender Sender { get; private set; } = null!;

    public IServiceProvider Services => application.Services;

    public static async Task<TestApplication> CreateAsync(TimeProvider? clock = null)
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"ready-to-go-travel-support-api-{Guid.CreateVersion7():N}.db");
        var connectionString = $"Data Source={databasePath};Foreign Keys=True;Default Timeout=30";
        clock ??= new FixedTimeProvider(new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero));

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
        builder.Services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions.TryAdd("code", "request_failed");
                context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;
            });
        builder.Services.AddAuthentication(TestAuthenticationHandler.AuthenticationScheme)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.AuthenticationScheme,
                _ => { });
        builder.Services.AddSingleton<IAuthorizationHandler, SupportAgentAuthorizationHandler>();
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("consumer", policy => policy.RequireAuthenticatedUser().RequireClaim("sub"));
            options.AddPolicy("support-agent", policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new SupportAgentRequirement()));
        });
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            foreach (var policyName in NoLimitPolicies)
            {
                options.AddPolicy(policyName, context => RateLimitPartition.GetNoLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "test"));
            }
        });

        builder.Services.AddSupportModule((_, options) => options.UseSqlite(connectionString));
        var storage = new InMemoryObjectStorage();
        var scanner = new RecordingAttachmentScanner(AttachmentScanOutcome.Clean);
        var sender = new RecordingSupportNotificationSender();
        builder.Services.Replace(ServiceDescriptor.Singleton<IObjectStorage>(storage));
        builder.Services.Replace(ServiceDescriptor.Singleton<IAttachmentScanner>(scanner));
        builder.Services.Replace(ServiceDescriptor.Singleton<ISupportNotificationSender>(sender));

        var application = builder.Build();
        application.UseExceptionHandler();
        application.UseAuthentication();
        application.UseAuthorization();
        application.UseRateLimiter();
        var api = application.MapGroup("/api/v1");
        api.MapSupportEndpoints();
        api.MapSupportGuestEndpoints();
        api.MapSupportStaffEndpoints();
        await application.StartAsync();

        await using (var scope = application.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
            var creator = database.GetService<IRelationalDatabaseCreator>();
            await creator.CreateTablesAsync();
        }

        var result = new TestApplication(application, databasePath, application.GetTestClient())
        {
            Storage = storage,
            Scanner = scanner,
            Sender = sender,
        };
        return result;
    }

    public void SetSubject(string subject)
    {
        Client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.SubjectHeader);
        Client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.RolesHeader);
        Client.DefaultRequestHeaders.Add(TestAuthenticationHandler.SubjectHeader, subject);
    }

    public void SetStaffSubject(string subject)
    {
        Client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.SubjectHeader);
        Client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.RolesHeader);
        Client.DefaultRequestHeaders.Add(TestAuthenticationHandler.SubjectHeader, subject);
        Client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RolesHeader, "support-agent");
    }

    public void ClearSubject()
    {
        Client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.SubjectHeader);
        Client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.RolesHeader);
    }

    public async Task DeliverPendingNotificationsAsync()
    {
        await using var scope = application.Services.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<ISupportNotificationOutboxProcessor>();
        while (await processor.ProcessNextAsync("test-worker"))
        {
        }
    }

    public async Task DrainAttachmentScansAsync()
    {
        await using var scope = application.Services.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<IAttachmentScanProcessor>();
        while (await processor.ProcessNextAsync("test-worker"))
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await application.DisposeAsync();
        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }
    }
}

internal static class HttpResponseMessageExtensions
{
    public static async Task<T?> ReadAsAsync<T>(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>();
}
