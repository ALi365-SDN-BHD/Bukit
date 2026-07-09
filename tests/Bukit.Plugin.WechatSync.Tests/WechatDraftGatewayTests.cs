using System.Net;
using System.Net.Sockets;
using System.Text;
using Bukit.WechatSyncing;
using Xunit;

namespace Bukit.Plugin.WechatSync.Tests;

public sealed class WechatDraftGatewayTests
{
    [Fact]
    public async Task DefaultDownloadImageAsync_RejectsResponseLargerThanWechatImageLimit()
    {
        var body = new byte[ImageConverter.MaterialImageMaxBytes + 1];
        using var server = new LoopbackImageServer(body);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            WechatDraftGateway.DefaultDownloadImageAsync(server.Url, CancellationToken.None));

        Assert.Contains("too large", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class LoopbackImageServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task _serveTask;

        public LoopbackImageServer(byte[] body)
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();

            var endpoint = (IPEndPoint)_listener.LocalEndpoint;
            Url = $"http://127.0.0.1:{endpoint.Port}/image.jpg";
            _serveTask = Task.Run(() => ServeAsync(body));
        }

        public string Url { get; }

        public void Dispose()
        {
            _listener.Stop();
            try
            {
                _serveTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // Test cleanup only.
            }
        }

        private async Task ServeAsync(byte[] body)
        {
            try
            {
                using var client = await _listener.AcceptTcpClientAsync();
                await using var stream = client.GetStream();
                await ReadRequestHeadersAsync(stream);

                var headers = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 200 OK\r\n" +
                    "Content-Type: image/jpeg\r\n" +
                    $"Content-Length: {body.Length}\r\n" +
                    "Connection: close\r\n\r\n");
                await stream.WriteAsync(headers);
                await stream.WriteAsync(body);
            }
            catch
            {
                // The client may close early after rejecting Content-Length.
            }
        }

        private static async Task ReadRequestHeadersAsync(NetworkStream stream)
        {
            var buffer = new byte[1024];
            var request = new StringBuilder();
            while (true)
            {
                var read = await stream.ReadAsync(buffer);
                if (read == 0)
                {
                    return;
                }

                request.Append(Encoding.ASCII.GetString(buffer, 0, read));
                if (request.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
                {
                    return;
                }
            }
        }
    }
}
