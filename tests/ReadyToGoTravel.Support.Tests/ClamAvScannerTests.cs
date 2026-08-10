using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using ReadyToGoTravel.Support.Scanning;

namespace ReadyToGoTravel.Support.Tests;

public sealed class ClamAvScannerTests
{
    [Fact]
    public async Task APeerThatAcceptsTheConnectionButNeverReadsTimesOutInsteadOfHangingTheWorker()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var acceptTask = listener.AcceptTcpClientAsync();

        var scanner = new ClamAvScanner(Options.Create(new ClamAvOptions
        {
            Enabled = true,
            Host = "127.0.0.1",
            Port = port,
            ConnectTimeoutMilliseconds = 2000,
            ReadTimeoutMilliseconds = 300,
        }));

        // Large enough that, with the peer never draining its receive buffer, the writes below
        // block on TCP backpressure well before the peer would ever be read from — reproducing
        // "accepts the connection but stops reading" rather than merely a slow response.
        using var content = new MemoryStream(new byte[8 * 1024 * 1024]);

        var stopwatch = Stopwatch.StartNew();
        var scanTask = scanner.ScanAsync(content);
        var winner = await Task.WhenAny(scanTask, Task.Delay(TimeSpan.FromSeconds(10)));
        stopwatch.Stop();

        Assert.Same(scanTask, winner);
        Assert.Equal(AttachmentScanOutcome.Unavailable, await scanTask);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Expected the scan to time out near ReadTimeoutMilliseconds, took {stopwatch.Elapsed}.");

        using var accepted = await acceptTask;
    }

    [Fact]
    public async Task APeerThatNeverAcceptsTheConnectionFailsWithoutHangingTheWorker()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var scanner = new ClamAvScanner(Options.Create(new ClamAvOptions
        {
            Enabled = true,
            Host = "127.0.0.1",
            Port = port,
            ConnectTimeoutMilliseconds = 300,
            ReadTimeoutMilliseconds = 300,
        }));

        using var content = new MemoryStream("not-a-real-file"u8.ToArray());

        var outcome = await scanner.ScanAsync(content);

        Assert.Equal(AttachmentScanOutcome.Unavailable, outcome);
    }

    [Fact]
    public async Task CancellingTheCallerTokenPropagatesRatherThanReportingUnavailable()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var acceptTask = listener.AcceptTcpClientAsync();

        var scanner = new ClamAvScanner(Options.Create(new ClamAvOptions
        {
            Enabled = true,
            Host = "127.0.0.1",
            Port = port,
            ConnectTimeoutMilliseconds = 5000,
            ReadTimeoutMilliseconds = 5000,
        }));

        using var content = new MemoryStream(new byte[8 * 1024 * 1024]);
        using var callerCancellation = new CancellationTokenSource();
        callerCancellation.CancelAfter(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => scanner.ScanAsync(content, callerCancellation.Token));

        using var accepted = await acceptTask;
    }
}
