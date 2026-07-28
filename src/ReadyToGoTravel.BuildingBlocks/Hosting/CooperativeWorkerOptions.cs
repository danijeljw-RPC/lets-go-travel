namespace ReadyToGoTravel.BuildingBlocks.Hosting;

public sealed class CooperativeWorkerOptions
{
    public TimeSpan IdleDelay { get; init; } = TimeSpan.FromMinutes(1);
}
