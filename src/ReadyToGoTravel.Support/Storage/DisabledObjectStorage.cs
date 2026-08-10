namespace ReadyToGoTravel.Support.Storage;

public sealed class ObjectStorageUnavailableException()
    : InvalidOperationException("Object storage is not configured.");

internal sealed class DisabledObjectStorage : IObjectStorage
{
    public Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default) =>
        throw new ObjectStorageUnavailableException();

    public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default) =>
        throw new ObjectStorageUnavailableException();

    public Task<Uri> CreateDownloadUrlAsync(string key, TimeSpan validFor, CancellationToken cancellationToken = default) =>
        throw new ObjectStorageUnavailableException();

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default) =>
        throw new ObjectStorageUnavailableException();
}
