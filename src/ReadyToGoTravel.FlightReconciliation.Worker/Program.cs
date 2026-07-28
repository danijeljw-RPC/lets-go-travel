using Microsoft.Extensions.Options;
using ReadyToGoTravel.BuildingBlocks.Hosting;
using ReadyToGoTravel.FlightReconciliation.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddOptions<CooperativeWorkerOptions>()
    .BindConfiguration("Worker")
    .Validate(options => options.IdleDelay > TimeSpan.Zero, "Worker:IdleDelay must be positive.")
    .ValidateOnStart();
builder.Services.AddWorkerHealthPublishing();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
