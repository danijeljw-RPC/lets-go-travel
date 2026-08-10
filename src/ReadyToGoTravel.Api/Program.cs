using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Api.Endpoints;
using ReadyToGoTravel.Api.Infrastructure;
using ReadyToGoTravel.Booking;
using ReadyToGoTravel.Booking.Http;
using ReadyToGoTravel.Booking.Webhooks;
using ReadyToGoTravel.Consumer;
using ReadyToGoTravel.Consumer.Http;
using ReadyToGoTravel.Search;
using ReadyToGoTravel.Search.Capabilities;
using ReadyToGoTravel.Search.Http;
using ReadyToGoTravel.Support;
using ReadyToGoTravel.Support.Guest;
using ReadyToGoTravel.Support.Http;
using ReadyToGoTravel.Support.Notifications;
using ReadyToGoTravel.Support.Scanning;
using ReadyToGoTravel.Support.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
builder.Services.AddPlatformAuthentication(builder.Configuration);
var consumerConnectionString = builder.Configuration.GetConnectionString("Consumer")
    ?? throw new InvalidOperationException("ConnectionStrings:Consumer is required.");
builder.Services.AddConsumerModule((_, options) => options.UseNpgsql(consumerConnectionString));
var bookingEnvironmentValue = builder.Configuration["Booking:Environment"] ?? "Production";
if (!Enum.TryParse<SearchEnvironment>(bookingEnvironmentValue, true, out var bookingEnvironment))
{
    throw new InvalidOperationException("Booking:Environment must be Sandbox or Production.");
}

builder.Services.AddBookingModule(
    (_, options) => options.UseNpgsql(consumerConnectionString),
    bookingEnvironment,
    builder.Configuration.GetValue<bool>("Booking:EnableFixtures"));
builder.Services.Configure<LiteApiWebhookOptions>(
    builder.Configuration.GetSection(LiteApiWebhookOptions.SectionName));
var searchEnvironmentValue = builder.Configuration["Search:Environment"] ?? "Production";
if (!Enum.TryParse<SearchEnvironment>(searchEnvironmentValue, true, out var searchEnvironment))
{
    throw new InvalidOperationException("Search:Environment must be Sandbox or Production.");
}

builder.Services.AddSearchModule(
    searchEnvironment,
    builder.Configuration.GetValue<bool>("Search:EnableFixtures"));
var supportConnectionString = builder.Configuration.GetConnectionString("Support") ?? consumerConnectionString;
builder.Services.AddSupportModule((_, options) => options.UseNpgsql(supportConnectionString));
builder.Services.Configure<SupportStorageOptions>(
    builder.Configuration.GetSection(SupportStorageOptions.SectionName));
builder.Services.Configure<ClamAvOptions>(
    builder.Configuration.GetSection(ClamAvOptions.SectionName));
builder.Services.Configure<GuestTokenOptions>(
    builder.Configuration.GetSection(GuestTokenOptions.SectionName));
builder.Services.Configure<SupportNotificationSenderOptions>(
    builder.Configuration.GetSection(SupportNotificationSenderOptions.SectionName));
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions.TryAdd("code", "request_failed");
        context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;
    };
});
var internalCallerSecret = builder.Configuration["Support:InternalCallerSecret"];
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("public-api", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1),
            }));
    options.AddPolicy("search", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1),
            }));
    options.AddPolicy("checkout", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1),
            }));
    options.AddPolicy("webhook", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1),
            }));
    options.AddPolicy("support", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            SupportRateLimitPartitions.AuthenticatedKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1),
            }));
    options.AddPolicy("support-guest", SupportRateLimitPartitions.GuestPartition);
    options.AddPolicy("support-ticket-create", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            SupportRateLimitPartitions.TicketCreationKey(context, internalCallerSecret),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1),
            }));
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
});
app.MapHealthChecks("/health/ready");

var api = app.MapGroup("/api/v1")
    .RequireRateLimiting("public-api");
api.MapPlatformEndpoints();
api.MapConsumerEndpoints();
api.MapSearchEndpoints();
api.MapBookingEndpoints();
api.MapWebhookEndpoints();
api.MapSupportEndpoints();
api.MapSupportGuestEndpoints();
api.MapSupportStaffEndpoints();

app.MapFallback("/api/{**path}", (HttpContext context) =>
    Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Route not found",
        extensions: new Dictionary<string, object?>
        {
            ["code"] = "route_not_found",
            ["correlationId"] = context.TraceIdentifier,
        }));

app.Run();

public partial class Program;
