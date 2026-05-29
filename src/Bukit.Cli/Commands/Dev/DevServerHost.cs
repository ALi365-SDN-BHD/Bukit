using System.Net;
using System.Net.Sockets;
using Bukit.Shared;

namespace Bukit.Cli.Commands.Dev;

internal sealed class DevServerHost : IDevServerHost
{
    private const int MaxPortAttempts = 20;

    private readonly HttpListener _listener;
    private bool _disposed;

    private DevServerHost(HttpListener listener, string host, int port)
    {
        _listener = listener;
        Port = port;
        Prefix = $"http://{host}:{port}/";
    }

    public int Port { get; }

    public string Prefix { get; }

    public static DevServerHost Start(string host, int requestedPort, ILogger logger)
    {
        var chosen = requestedPort == 0 ? PickFreePort() : requestedPort;

        for (var attempt = 0; attempt < MaxPortAttempts; attempt++)
        {
            var candidate = chosen + attempt;
            if (candidate > 65535) break;

            var listener = new HttpListener();
            try
            {
                listener.Prefixes.Add($"http://{host}:{candidate}/");
                listener.Start();
                if (attempt > 0)
                {
                    logger.Info($"Port {chosen} unavailable, using {candidate}.");
                }
                return new DevServerHost(listener, host, candidate);
            }
            catch (HttpListenerException)
            {
                listener.Close();
            }
            catch (SocketException)
            {
                listener.Close();
            }
        }

        throw new InvalidOperationException($"Failed to listen on {host}:{requestedPort}");
    }

    public async Task RunAcceptLoopAsync(Func<HttpListenerContext, Task> dispatchAsync, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            _ = Task.Run(() => dispatchAsync(context), ct);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            _listener.Close();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static int PickFreePort()
    {
        var tcp = new TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        try
        {
            return ((IPEndPoint)tcp.LocalEndpoint).Port;
        }
        finally
        {
            tcp.Stop();
        }
    }
}
