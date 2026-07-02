using System.Net;
using System.Net.Sockets;
using Bukit.Shared;

namespace Bukit.Cli.Commands.Dev;

internal sealed class DevServerHost : IDevServerHost
{
    private const int MaxPortAttempts = 20;
    private const int MaxConcurrentRequests = 64;

    private readonly HttpListener _listener;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _requestGate = new(MaxConcurrentRequests, MaxConcurrentRequests);
    private bool _disposed;

    private DevServerHost(HttpListener listener, string host, int port, ILogger logger)
    {
        _listener = listener;
        _logger = logger;
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
                return new DevServerHost(listener, host, candidate, logger);
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

            try
            {
                await _requestGate.WaitAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                CloseResponseBestEffort(context);
                break;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await dispatchAsync(context).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    _logger.Error($"dev.request.dispatch: {ex.Message}");
                    CloseResponseBestEffort(context);
                }
                finally
                {
                    _requestGate.Release();
                }
            }, CancellationToken.None);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            _listener.Stop();
        }
        catch (ObjectDisposedException)
        {
        }

        _requestGate.Dispose();
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

    private static void CloseResponseBestEffort(HttpListenerContext context)
    {
        try
        {
            if (context.Response.OutputStream.CanWrite)
            {
                context.Response.StatusCode = 500;
            }

            context.Response.Close();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }
}
