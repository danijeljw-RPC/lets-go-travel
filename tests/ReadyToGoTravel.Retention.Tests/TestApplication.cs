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
using ReadyToGoTravel.Retention.Http;
using ReadyToGoTravel.Retention.Persistence;

namespace ReadyToGoTravel.Retention.Tests;

/// <summary>Mirrors ReadyToGoTravel.Support.Tests.TestApplication's self-contained HTTP test host shape.</summary>
internal sealed class TestApplication : IAsyncDisposable
{
    private readonly WebApplication application;
    private readonly string databasePath;

    private TestApplication(WebApplication application, string databasePath, HttpClient client)
    {
        this.application = application;
        this.databasePath = databasePath;
        Client = client;
    }

    public HttpClient Client { get; }

    public IServiceProvider Services => application.Services;

    public static async Task<TestApplication> CreateAsync()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"ready-to-go-travel-retention-api-{Guid.CreateVersion7():N}.db");
        var connectionString = $"Data Source={databasePath};Foreign Keys=True;Default Timeout=30";

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
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
        builder.Services.AddSingleton<IAuthorizationHandler, LegalHoldOfficerAuthorizationHandler>();
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("consumer", policy => policy.RequireAuthenticatedUser().RequireClaim("sub"));
            options.AddPolicy("legal-hold-officer", policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new LegalHoldOfficerRequirement()));
        });
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("public-api", context => RateLimitPartition.GetNoLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "test"));
        });

        builder.Services.AddRetentionModule((_, options) => options.UseSqlite(connectionString));

        var application = builder.Build();
        application.UseExceptionHandler();
        application.UseAuthentication();
        application.UseAuthorization();
        application.UseRateLimiter();
        var api = application.MapGroup("/api/v1").RequireRateLimiting("public-api");
        api.MapLegalHoldEndpoints();
        await application.StartAsync();

        await using (var scope = application.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<RetentionDbContext>();
            var creator = database.GetService<IRelationalDatabaseCreator>();
            await creator.CreateTablesAsync();
        }

        return new TestApplication(application, databasePath, application.GetTestClient());
    }

    public void SetStaffSubject(string subject)
    {
        Client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.SubjectHeader);
        Client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.RolesHeader);
        Client.DefaultRequestHeaders.Add(TestAuthenticationHandler.SubjectHeader, subject);
        Client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RolesHeader, "legal-hold-officer");
    }

    public void SetConsumerSubject(string subject)
    {
        Client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.SubjectHeader);
        Client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.RolesHeader);
        Client.DefaultRequestHeaders.Add(TestAuthenticationHandler.SubjectHeader, subject);
    }

    public void ClearSubject()
    {
        Client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.SubjectHeader);
        Client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.RolesHeader);
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
