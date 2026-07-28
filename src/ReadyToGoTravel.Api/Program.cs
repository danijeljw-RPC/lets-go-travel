using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using ReadyToGoTravel.Api.Endpoints;
using ReadyToGoTravel.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["code"] ??= "request_failed";
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
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
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

app.MapGroup("/api/v1")
    .RequireRateLimiting("public-api")
    .MapPlatformEndpoints();

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
