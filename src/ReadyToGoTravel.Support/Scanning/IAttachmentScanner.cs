namespace ReadyToGoTravel.Support.Scanning;

public enum AttachmentScanOutcome
{
    Clean,
    Infected,
    Unavailable,
}

public interface IAttachmentScanner
{
    Task<AttachmentScanOutcome> ScanAsync(Stream content, CancellationToken cancellationToken = default);
}
