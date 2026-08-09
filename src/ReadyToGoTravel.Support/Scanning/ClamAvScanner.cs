using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;

namespace ReadyToGoTravel.Support.Scanning;

internal sealed class ClamAvScanner(IOptions<ClamAvOptions> optionsAccessor) : IAttachmentScanner
{
    private const int ChunkSize = 2048;

    public async Task<AttachmentScanOutcome> ScanAsync(Stream content, CancellationToken cancellationToken = default)
    {
        var options = optionsAccessor.Value;
        try
        {
            using var client = new TcpClient();
            using var connectCancellation = new CancellationTokenSource(options.ConnectTimeoutMilliseconds);
            using var linkedConnect = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, connectCancellation.Token);
            await client.ConnectAsync(options.Host, options.Port, linkedConnect.Token);

            await using var stream = client.GetStream();
            stream.ReadTimeout = options.ReadTimeoutMilliseconds;
            var command = Encoding.ASCII.GetBytes("zINSTREAM\0");
            await stream.WriteAsync(command, cancellationToken);

            var lengthBuffer = new byte[4];
            var buffer = new byte[ChunkSize];
            int read;
            while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
            {
                BinaryPrimitives.WriteUInt32BigEndian(lengthBuffer, (uint)read);
                await stream.WriteAsync(lengthBuffer, cancellationToken);
                await stream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            BinaryPrimitives.WriteUInt32BigEndian(lengthBuffer, 0);
            await stream.WriteAsync(lengthBuffer, cancellationToken);

            using var responseCancellation = new CancellationTokenSource(options.ReadTimeoutMilliseconds);
            using var linkedResponse = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, responseCancellation.Token);
            using var responseStream = new MemoryStream();
            var responseBuffer = new byte[256];
            int responseRead;
            while ((responseRead = await stream.ReadAsync(responseBuffer, linkedResponse.Token)) > 0)
            {
                responseStream.Write(responseBuffer, 0, responseRead);
                if (responseBuffer.AsSpan(0, responseRead).IndexOf((byte)0) >= 0)
                {
                    break;
                }
            }

            var response = Encoding.ASCII.GetString(responseStream.ToArray());
            if (response.Contains("FOUND", StringComparison.Ordinal))
            {
                return AttachmentScanOutcome.Infected;
            }

            return response.Contains("OK", StringComparison.Ordinal)
                ? AttachmentScanOutcome.Clean
                : AttachmentScanOutcome.Unavailable;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is SocketException or IOException or OperationCanceledException)
        {
            return AttachmentScanOutcome.Unavailable;
        }
    }
}
