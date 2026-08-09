using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReadyToGoTravel.Booking;
using ReadyToGoTravel.BuildingBlocks.Hosting;
using ReadyToGoTravel.FlightReconciliation.Worker;
using ReadyToGoTravel.Search.Capabilities;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddOptions<CooperativeWorkerOptions>()
    .BindConfiguration("Worker")
    .Validate(options => options.IdleDelay > TimeSpan.Zero, "Worker:IdleDelay must be positive.")
    .ValidateOnStart();
builder.Services.AddWorkerHealthPublishing();
var connectionString = builder.Configuration.GetConnectionString("Booking")
    ?? throw new InvalidOperationException("ConnectionStrings:Booking is required.");
var environmentValue = builder.Configuration["Booking:Environment"] ?? "Production";
if (!Enum.TryParse<SearchEnvironment>(environmentValue, true, out var environment))
{
    throw new InvalidOperationException("Booking:Environment must be Sandbox or Production.");
}

builder.Services.AddBookingModule(
    (_, options) => options.UseNpgsql(connectionString),
    environment,
    builder.Configuration.GetValue<bool>("Booking:EnableFixtures"));
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
