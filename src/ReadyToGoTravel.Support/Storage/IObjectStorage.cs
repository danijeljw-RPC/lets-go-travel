namespace ReadyToGoTravel.Support.Storage;

public interface IObjectStorage
{
    Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default);

    Task<Uri> CreateDownloadUrlAsync(string key, TimeSpan validFor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
