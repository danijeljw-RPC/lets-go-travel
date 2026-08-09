using ReadyToGoTravel.Support.Scanning;

namespace ReadyToGoTravel.Support.Tests;

internal sealed class RecordingAttachmentScanner(AttachmentScanOutcome outcome) : IAttachmentScanner
{
    public int CallCount { get; private set; }

    public Task<AttachmentScanOutcome> ScanAsync(Stream content, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(outcome);
    }
}
