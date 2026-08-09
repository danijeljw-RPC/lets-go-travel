using ReadyToGoTravel.Support.Storage;

namespace ReadyToGoTravel.Support.Tests;

internal sealed class InMemoryObjectStorage : IObjectStorage
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, (byte[] Content, string ContentType)> objects = [];
    public List<(string Key, TimeSpan ValidFor)> DownloadUrlRequests { get; } = [];
    public List<string> DeletedKeys { get; } = [];
    public Exception? DeleteFailure { get; set; }

    public bool Contains(string key)
    {
        lock (gate)
        {
            return objects.ContainsKey(key);
        }
    }

    public Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        lock (gate)
        {
            objects[key] = (buffer.ToArray(), contentType);
        }

        return Task.CompletedTask;
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            if (!objects.TryGetValue(key, out var stored))
            {
                throw new FileNotFoundException(key);
            }

            return Task.FromResult<Stream>(new MemoryStream(stored.Content, writable: false));
        }
    }

    public Task<Uri> CreateDownloadUrlAsync(string key, TimeSpan validFor, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            DownloadUrlRequests.Add((key, validFor));
        }

        return Task.FromResult(new Uri($"https://storage.test/{key}?valid-for={validFor.TotalSeconds}"));
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        if (DeleteFailure is { } failure)
        {
            return Task.FromException(failure);
        }

        lock (gate)
        {
            objects.Remove(key);
            DeletedKeys.Add(key);
        }

        return Task.CompletedTask;
    }
}
