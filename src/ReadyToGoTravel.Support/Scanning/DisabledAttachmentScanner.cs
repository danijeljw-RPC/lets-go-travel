namespace ReadyToGoTravel.Support.Scanning;

internal sealed class DisabledAttachmentScanner : IAttachmentScanner
{
    public Task<AttachmentScanOutcome> ScanAsync(Stream content, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(AttachmentScanOutcome.Unavailable);
    }
}
