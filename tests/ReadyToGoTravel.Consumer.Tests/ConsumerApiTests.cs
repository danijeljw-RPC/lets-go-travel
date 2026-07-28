using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReadyToGoTravel.Consumer.Http;

namespace ReadyToGoTravel.Consumer.Tests;

public sealed class ConsumerApiTests
{
    [Fact]
    public async Task ProtectedRoutesRejectAnonymousRequests()
    {
        await using var application = await TestApplication.CreateAsync();

        var response = await application.Client.GetAsync("/api/v1/trips");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProfileProvisioningIsIdempotent()
    {
        await using var application = await TestApplication.CreateAsync("customer-one");

        var first = await application.Client.PutAsJsonAsync(
            "/api/v1/me",
            new { preferredLocale = "en-AU", adultConfirmed = true });
        var second = await application.Client.PutAsJsonAsync(
            "/api/v1/me",
            new { preferredLocale = "en-AU", adultConfirmed = true });

        first.EnsureSuccessStatusCode();
        second.EnsureSuccessStatusCode();
        var firstProfile = await first.Content.ReadFromJsonAsync<ProfileResponse>();
        var secondProfile = await second.Content.ReadFromJsonAsync<ProfileResponse>();
        Assert.Equal(firstProfile!.Id, secondProfile!.Id);
        Assert.Equal("AUD", firstProfile.DisplayCurrency);
    }

    [Fact]
    public async Task UnsupportedLocaleReturnsStableProblemCode()
    {
        await using var application = await TestApplication.CreateAsync("customer-one");

        var response = await application.Client.PutAsJsonAsync(
            "/api/v1/me",
            new { preferredLocale = "fr-FR", adultConfirmed = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("unsupported_locale", problem!.Code);
        Assert.False(string.IsNullOrWhiteSpace(problem.CorrelationId));
    }

    [Fact]
    public async Task AnotherCustomerCannotDiscoverATrip()
    {
        await using var application = await TestApplication.CreateAsync("owner");
        await application.ActivateProfileAsync();
        var created = await application.Client.PostAsJsonAsync(
            "/api/v1/trips",
            new { title = "Japan", primaryDestination = "Tokyo", startDate = "2026-10-10", endDate = "2026-10-18" });
        created.EnsureSuccessStatusCode();
        var trip = await created.Content.ReadFromJsonAsync<TripResponse>();
        application.SetSubject("stranger");
        await application.ActivateProfileAsync();

        var response = await application.Client.GetAsync($"/api/v1/trips/{trip!.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("trip_not_found", problem!.Code);
    }

    [Fact]
    public async Task MinorTravellerRequiresGuardianAuthority()
    {
        await using var application = await TestApplication.CreateAsync("owner");
        await application.ActivateProfileAsync();

        var response = await application.Client.PostAsJsonAsync(
            "/api/v1/travellers",
            new
            {
                givenName = "Sam",
                familyName = "Taylor",
                relationshipLabel = "Child",
                isMinor = true,
                guardianAuthorityConfirmed = false,
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("guardian_authority_required", problem!.Code);
    }

    [Fact]
    public async Task SensitiveTravellerJsonFieldsAreRejected()
    {
        await using var application = await TestApplication.CreateAsync("owner");
        await application.ActivateProfileAsync();
        using var content = new StringContent(
            """{"givenName":"Sam","familyName":"Taylor","isMinor":false,"guardianAuthorityConfirmed":false,"passportNumber":"N123"}""",
            Encoding.UTF8,
            "application/json");

        var response = await application.Client.PostAsync("/api/v1/travellers", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SensitiveTravellerStorageCapabilityIsPublicAndDisabled()
    {
        await using var application = await TestApplication.CreateAsync();

        var capability = await application.Client.GetFromJsonAsync<SensitiveStorageResponse>(
            "/api/v1/privacy/sensitive-traveller-storage");

        Assert.NotNull(capability);
        Assert.False(capability.Enabled);
        Assert.Contains("dateOfBirth", capability.Categories);
        Assert.Contains("identityDocuments", capability.Categories);
    }

    private sealed record ProfileResponse(Guid Id, string DisplayCurrency);

    private sealed record TripResponse(Guid Id);

    private sealed record ProblemResponse(string Code, string CorrelationId);

    private sealed record SensitiveStorageResponse(bool Enabled, string[] Categories);

    private sealed class TestApplication : IAsyncDisposable
    {
        private readonly WebApplication application;
        private readonly SqliteConnection connection;

        private TestApplication(WebApplication application, SqliteConnection connection, HttpClient client)
        {
            this.application = application;
            this.connection = connection;
            Client = client;
        }

        public HttpClient Client { get; }

        public static async Task<TestApplication> CreateAsync(string? subject = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Testing",
            });
            builder.WebHost.UseTestServer();
            builder.Services.ConfigureHttpJsonOptions(options =>
                options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
            builder.Services.AddProblemDetails();
            builder.Services.AddAuthentication(TestAuthenticationHandler.AuthenticationScheme)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.AuthenticationScheme,
                    _ => { });
            builder.Services.AddAuthorization(options =>
                options.AddPolicy("consumer", policy =>
                    policy.RequireAuthenticatedUser().RequireClaim("sub")));
            builder.Services.AddConsumerModule((_, options) => options.UseSqlite(connection));

            var application = builder.Build();
            application.UseExceptionHandler();
            application.UseAuthentication();
            application.UseAuthorization();
            application.MapGroup("/api/v1").MapConsumerEndpoints();
            await application.StartAsync();

            await using (var scope = application.Services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ReadyToGoTravel.Consumer.Persistence.ConsumerDbContext>();
                await context.Database.EnsureCreatedAsync();
            }

            var client = application.GetTestClient();
            var result = new TestApplication(application, connection, client);
            if (subject is not null)
            {
                result.SetSubject(subject);
            }

            return result;
        }

        public void SetSubject(string subject)
        {
            Client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.SubjectHeader);
            Client.DefaultRequestHeaders.Add(TestAuthenticationHandler.SubjectHeader, subject);
        }

        public async Task ActivateProfileAsync()
        {
            var response = await Client.PutAsJsonAsync(
                "/api/v1/me",
                new { preferredLocale = "en-AU", adultConfirmed = true });
            response.EnsureSuccessStatusCode();
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await application.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
