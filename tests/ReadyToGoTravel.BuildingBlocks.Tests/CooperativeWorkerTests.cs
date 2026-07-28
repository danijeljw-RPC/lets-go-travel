using ReadyToGoTravel.BuildingBlocks.Hosting;

namespace ReadyToGoTravel.BuildingBlocks.Tests;

public sealed class CooperativeWorkerTests
{
    [Fact]
    public async Task WorkerStopsPromptlyWhenCancellationIsRequested()
    {
        var cycleCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = new RecordingWorker(cycleCompleted);

        await worker.StartAsync(CancellationToken.None);
        await cycleCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(worker.CycleCount >= 1);
    }

    private sealed class RecordingWorker(TaskCompletionSource cycleCompleted)
        : CooperativeWorker(TimeProvider.System, TimeSpan.FromHours(1))
    {
        public int CycleCount { get; private set; }

        protected override Task ExecuteCycleAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CycleCount++;
            cycleCompleted.TrySetResult();
            return Task.CompletedTask;
        }
    }
}
