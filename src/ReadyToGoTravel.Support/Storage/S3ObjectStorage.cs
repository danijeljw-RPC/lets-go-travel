using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace ReadyToGoTravel.Support.Storage;

internal sealed class S3ObjectStorage : IObjectStorage, IDisposable
{
    private readonly AmazonS3Client client;
    private readonly string bucketName;

    public S3ObjectStorage(IOptions<SupportStorageOptions> optionsAccessor)
    {
        var options = optionsAccessor.Value;
        bucketName = options.BucketName;
        var config = new AmazonS3Config
        {
            ServiceURL = options.ServiceUrl,
            ForcePathStyle = options.ForcePathStyle,
            AuthenticationRegion = options.Region,
        };
        client = new AmazonS3Client(options.AccessKey, options.SecretKey, config);
    }

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
        };
        await client.PutObjectAsync(request, cancellationToken);
    }

    public async Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var response = await client.GetObjectAsync(bucketName, key, cancellationToken);
        return response.ResponseStream;
    }

    public Task<Uri> CreateDownloadUrlAsync(string key, TimeSpan validFor, CancellationToken cancellationToken = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucketName,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(validFor),
        };
        var url = client.GetPreSignedURL(request);
        return Task.FromResult(new Uri(url));
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        await client.DeleteObjectAsync(bucketName, key, cancellationToken);
    }

    public void Dispose() => client.Dispose();
}
