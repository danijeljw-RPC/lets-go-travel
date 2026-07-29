using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Api.Endpoints;
using ReadyToGoTravel.Api.Infrastructure;
using ReadyToGoTravel.Consumer;
using ReadyToGoTravel.Consumer.Http;
using ReadyToGoTravel.Search;
using ReadyToGoTravel.Search.Capabilities;
using ReadyToGoTravel.Search.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
builder.Services.AddPlatformAuthentication(builder.Configuration);
var consumerConnectionString = builder.Configuration.GetConnectionString("Consumer")
    ?? throw new InvalidOperationException("ConnectionStrings:Consumer is required.");
builder.Services.AddConsumerModule((_, options) => options.UseNpgsql(consumerConnectionString));
var searchEnvironmentValue = builder.Configuration["Search:Environment"] ?? "Production";
if (!Enum.TryParse<SearchEnvironment>(searchEnvironmentValue, true, out var searchEnvironment))
{
    throw new InvalidOperationException("Search:Environment must be Sandbox or Production.");
}

builder.Services.AddSearchModule(
    searchEnvironment,
    builder.Configuration.GetValue<bool>("Search:EnableFixtures"));
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions.TryAdd("code", "request_failed");
        context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;
    };
});
builder.Services.AddRateLimiter(options =>
{
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
