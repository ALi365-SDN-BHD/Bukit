using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using Bukit.Shared;

namespace Bukit.Cli.Commands.Dev;

internal sealed class DevServerHost : IDevServerHost
{
    private const int MaxPortAttempts = 20;
    private const int MaxConcurrentRequests = 64;

    private readonly HttpListener _listener;
    private readonly ILogger _logger;
    private readonly Action<Task>? _onRequestTracked;
    private readonly SemaphoreSlim _requestGate = new(MaxConcurrentRequests, MaxConcurrentRequests);
    private readonly object _lifecycleLock = new();
    private readonly TaskCompletionSource<bool> _acceptLoopCompleted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _acceptLoopStarted;
    private bool _requestGateDisposed;
    private bool _disposed;

    private DevServerHost(
        HttpListener listener,
        string host,
        int port,
        ILogger logger,
        Action<Task>? onRequestTracked)
    {
        _listener = listener;
        _logger = logger;
        _onRequestTracked = onRequestTracked;
        Port = port;
        Prefix = $"http://{host}:{port}/";
    }

    public int Port { get; }

    public string Prefix { get; }

    public static DevServerHost Start(string host, int requestedPort, ILogger logger)
        => Start(host, requestedPort, logger, onRequestTracked: null);

    internal static DevServerHost Start(
        string host,
        int requestedPort,
        ILogger logger,
        Action<Task>? onRequestTracked)
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
                return new DevServerHost(listener, host, candidate, logger, onRequestTracked);
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
        Exception? requestFailure = null;
        Exception? completionFailure = null;
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

                var bypassRequestGate =
                    context.Request.IsWebSocketRequest &&
                    context.Request.Url?.AbsolutePath == "/__ws__";
                try
                {
                    // The WebSocket hub has its own capacity; every other route
                    // remains bounded by the ordinary request gate.
                    if (!bypassRequestGate)
                    {
                        await _requestGate.WaitAsync(ct).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    CloseResponseBestEffort(context);
                    break;
                }

                ObserveCompletedRequests(activeRequests, ref requestFailure);
                var requestTask = Task.Run(
                    () => bypassRequestGate
                        ? DispatchRequestAsync(dispatchAsync, context, ct)
                        : DispatchGatedRequestAsync(dispatchAsync, context, ct),
                    CancellationToken.None);
                activeRequests.Add(requestTask);
                _onRequestTracked?.Invoke(requestTask);
            }
        }
        finally
        {
            try
            {
                await Task.WhenAll(activeRequests).ConfigureAwait(false);
            }
            catch
            {
                // Observe every remaining request task below while retaining
                // only the first failure for bounded completion semantics.
            }
            finally
            {
                ObserveRequestFailures(activeRequests, ref requestFailure);
                completionFailure = requestFailure;
                DisposeRequestGate();
                if (completionFailure is not null)
                {
                    _acceptLoopCompleted.TrySetException(completionFailure);
                }
                else
                {
                    _acceptLoopCompleted.TrySetResult(true);
                }
            }
        }

        if (completionFailure is not null)
        {
            ExceptionDispatchInfo.Capture(completionFailure).Throw();
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

        Exception? completionFailure = null;
        try
        {
            if (acceptLoopCompletion is not null)
            {
                acceptLoopCompletion.GetAwaiter().GetResult();
            }
            else
            {
                DisposeRequestGate();
            }
        }
        catch (Exception ex)
        {
            completionFailure = ex;
        }
        finally
        {
            _listener.Close();
        }

        if (completionFailure is not null)
        {
            ExceptionDispatchInfo.Capture(completionFailure).Throw();
        }
    }

    private async Task DispatchGatedRequestAsync(
        Func<HttpListenerContext, Task> dispatchAsync,
        HttpListenerContext context,
        CancellationToken ct)
    {
        try
        {
            await DispatchRequestAsync(dispatchAsync, context, ct);
        }
        finally
        {
            _requestGate.Release();
        }
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
    }

    private bool IsDisposed()
    {
        lock (_lifecycleLock)
        {
            return _disposed;
        }
    }

    private static void ObserveCompletedRequests(
        HashSet<Task> activeRequests,
        ref Exception? requestFailure)
    {
        foreach (var task in activeRequests.Where(task => task.IsCompleted).ToArray())
        {
            activeRequests.Remove(task);
            ObserveRequestFailure(task, ref requestFailure);
        }
    }

    private static void ObserveRequestFailures(
        IEnumerable<Task> requests,
        ref Exception? requestFailure)
    {
        foreach (var task in requests)
        {
            ObserveRequestFailure(task, ref requestFailure);
        }
    }

    private static void ObserveRequestFailure(Task request, ref Exception? requestFailure)
    {
        if (request.Exception is { } failure && requestFailure is null)
        {
            requestFailure = failure.Flatten().InnerExceptions[0];
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
