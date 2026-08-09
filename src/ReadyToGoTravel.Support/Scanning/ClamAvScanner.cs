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

            // NetworkStream.ReadTimeout/WriteTimeout only bound the synchronous Read/Write
            // methods, not ReadAsync/WriteAsync, which is all this class uses — so a single
            // linked CancellationTokenSource is the only thing that actually bounds every piece
            // of I/O below (reading the attachment, writing the request, reading the response).
            // Without it, a ClamAV daemon that accepts the connection but stops reading can hang
            // a write indefinitely and, with it, the worker processing this scan.
            using var scanCancellation = new CancellationTokenSource(options.ReadTimeoutMilliseconds);
            using var linkedScan = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, scanCancellation.Token);
            var scanToken = linkedScan.Token;

            var command = Encoding.ASCII.GetBytes("zINSTREAM\0");
            await stream.WriteAsync(command, scanToken);

            var lengthBuffer = new byte[4];
            var buffer = new byte[ChunkSize];
            int read;
            while ((read = await content.ReadAsync(buffer, scanToken)) > 0)
            {
                BinaryPrimitives.WriteUInt32BigEndian(lengthBuffer, (uint)read);
                await stream.WriteAsync(lengthBuffer, scanToken);
                await stream.WriteAsync(buffer.AsMemory(0, read), scanToken);
            }

            BinaryPrimitives.WriteUInt32BigEndian(lengthBuffer, 0);
            await stream.WriteAsync(lengthBuffer, scanToken);

            using var responseStream = new MemoryStream();
            var responseBuffer = new byte[256];
            int responseRead;
            while ((responseRead = await stream.ReadAsync(responseBuffer, scanToken)) > 0)
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
