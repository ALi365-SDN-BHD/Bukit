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
    private readonly object _lifecycleLock = new();
    private readonly TaskCompletionSource<bool> _acceptLoopCompleted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _acceptLoopStarted;
    private bool _requestGateDisposed;
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
        lock (_lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_acceptLoopStarted)
            {
                throw new InvalidOperationException("The dev server accept loop is already running.");
            }

            _acceptLoopStarted = true;
        }

        var activeRequests = new HashSet<Task>();
        try
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
                catch (HttpListenerException) when (ct.IsCancellationRequested || IsDisposed())
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

                activeRequests.RemoveWhere(task => task.IsCompleted);
                activeRequests.Add(Task.Run(
                    () => DispatchRequestAsync(dispatchAsync, context, ct),
                    CancellationToken.None));
            }
        }
        finally
        {
            await Task.WhenAll(activeRequests).ConfigureAwait(false);
            DisposeRequestGate();
            _acceptLoopCompleted.TrySetResult(true);
        }
    }

    public void Dispose()
    {
        Task? acceptLoopCompletion = null;
        lock (_lifecycleLock)
        {
            if (_disposed) return;
            _disposed = true;
            if (_acceptLoopStarted)
            {
                acceptLoopCompletion = _acceptLoopCompleted.Task;
            }
        }

        try
        {
            _listener.Stop();
        }
        catch (ObjectDisposedException)
        {
        }

        if (acceptLoopCompletion is not null)
        {
            acceptLoopCompletion.GetAwaiter().GetResult();
        }
        else
        {
            DisposeRequestGate();
        }

        _listener.Close();
    }

    private async Task DispatchRequestAsync(
        Func<HttpListenerContext, Task> dispatchAsync,
        HttpListenerContext context,
        CancellationToken ct)
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
    }

    private bool IsDisposed()
    {
        lock (_lifecycleLock)
        {
            return _disposed;
        }
    }

    private void DisposeRequestGate()
    {
        lock (_lifecycleLock)
        {
            if (_requestGateDisposed)
            {
                return;
            }

            _requestGate.Dispose();
            _requestGateDisposed = true;
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
