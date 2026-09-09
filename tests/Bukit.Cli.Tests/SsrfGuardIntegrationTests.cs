using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Bukit.Shared;
using Xunit;

namespace Bukit.Cli.Tests;

public class SsrfGuardIntegrationTests
{
    [Fact]
    public async Task HttpClient_WithSsrfGuard_RejectsPrivateNetworkConnection()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var url = $"http://127.0.0.1:{endpoint.Port}/";
        var acceptedConnections = 0;
        var server = ServeAsync();
        try
        {
            using var ordinaryClient = new HttpClient(new SocketsHttpHandler { UseProxy = false });
            using var response = await ordinaryClient.GetAsync(url, deadline.Token);
            response.EnsureSuccessStatusCode();
            Assert.Equal("OK", await response.Content.ReadAsStringAsync(deadline.Token));
            Assert.Equal(1, Volatile.Read(ref acceptedConnections));

            var safeHandler = SsrfGuard.CreateSafeHandler();
            safeHandler.UseProxy = false;
            using var safeClient = new HttpClient(safeHandler);
            var rejection = await Assert.ThrowsAsync<HttpRequestException>(
                () => safeClient.GetAsync(url, deadline.Token));
            var blocked = false;
            for (Exception? error = rejection; error is not null; error = error.InnerException)
            {
                if (error is HttpRequestException &&
                    error.Message.StartsWith("SSRF blocked:", StringComparison.Ordinal))
                    blocked = true;
            }
            Assert.True(blocked, $"Expected SSRF rejection, got: {rejection}");
            Assert.False(listener.Pending());
        }
        finally
        {
            await deadline.CancelAsync();
            listener.Stop();
            await server.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(1, Volatile.Read(ref acceptedConnections));

        async Task ServeAsync()
        {
            try
            {
                while (true)
                {
                    using var connection = await listener.AcceptTcpClientAsync(deadline.Token);
                    Interlocked.Increment(ref acceptedConnections);
                    await using var stream = connection.GetStream();
                    using var reader = new StreamReader(stream, leaveOpen: true);
                    while (await reader.ReadLineAsync(deadline.Token) is { Length: > 0 }) { }
                    await stream.WriteAsync(
                        "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nOK"u8.ToArray(),
                        deadline.Token);
                }
            }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested) { }
            catch (SocketException) when (deadline.IsCancellationRequested) { }
            catch (ObjectDisposedException) when (deadline.IsCancellationRequested) { }
        }
    }
}
