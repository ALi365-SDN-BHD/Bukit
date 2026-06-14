using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using Bukit.Shared;

namespace Bukit.Cli.Commands.Dev;

internal sealed class DevWebSocketHub : IDevWebSocketHub
{
    private const int DefaultMaxConnections = 64;

    private readonly ConcurrentDictionary<string, WebSocket> _clients = new();
    private readonly ILogger _logger;
    private readonly DevWebSocketAccessPolicy _accessPolicy;
    private readonly int _maxConnections;

    public DevWebSocketHub(ILogger logger, DevWebSocketAccessPolicy? accessPolicy = null, int maxConnections = DefaultMaxConnections)
    {
        _logger = logger;
        _accessPolicy = accessPolicy ?? DevWebSocketAccessPolicy.Loopback(port: null);
        _maxConnections = maxConnections;
    }

    public int ClientCount => _clients.Count;

    public async Task HandleUpgradeAsync(HttpListenerContext context, CancellationToken ct)
    {
        if (!_accessPolicy.IsAllowed(context.Request, out var reason))
        {
            Reject(context, 403, reason);
            return;
        }

        if (_clients.Count >= _maxConnections)
        {
            Reject(context, 429, "too many dev WebSocket clients");
            return;
        }

        var clientId = Guid.NewGuid().ToString("N");
        WebSocket? ws = null;
        try
        {
            var wsCtx = await context.AcceptWebSocketAsync(null);
            ws = wsCtx.WebSocket;
            _clients[clientId] = ws;

            var buffer = new byte[256];
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                try
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, ct);
                        break;
                    }
                }
                catch (WebSocketException)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"dev.ws.upgrade: {ex.Message}");
        }
        finally
        {
            _clients.TryRemove(clientId, out _);
        }
    }

    private void Reject(HttpListenerContext context, int statusCode, string reason)
    {
        _logger.Warn($"dev.ws.reject: {reason}");
        try
        {
            context.Response.StatusCode = statusCode;
            context.Response.Close();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    public async Task BroadcastReloadAsync()
    {
        var payload = Encoding.UTF8.GetBytes("reload");
        var deadClients = new List<string>();

        foreach (var (id, ws) in _clients)
        {
            try
            {
                if (ws.State == WebSocketState.Open)
                {
                    await ws.SendAsync(
                        new ArraySegment<byte>(payload),
                        WebSocketMessageType.Text,
                        endOfMessage: true,
                        CancellationToken.None);
                }
                else
                {
                    deadClients.Add(id);
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"dev.ws.broadcast: {ex.Message}");
                deadClients.Add(id);
            }
        }

        foreach (var id in deadClients)
        {
            _clients.TryRemove(id, out _);
        }
    }
}

internal sealed class DevWebSocketAccessPolicy
{
    private readonly string _bindHost;
    private readonly int? _port;
    private readonly bool _allowLan;

    public DevWebSocketAccessPolicy(string bindHost, int? port, bool allowLan)
    {
        _bindHost = string.IsNullOrWhiteSpace(bindHost) ? "localhost" : bindHost.Trim();
        _port = port;
        _allowLan = allowLan;
    }

    public static DevWebSocketAccessPolicy Loopback(int? port)
        => new("localhost", port, allowLan: false);

    public bool IsAllowed(HttpListenerRequest request, out string reason)
    {
        return IsAllowed(request.Headers["Host"], request.Headers["Origin"], out reason);
    }

    internal bool IsAllowed(string? hostHeader, string? originHeader, out string reason)
    {
        if (!TryParseAuthority(hostHeader, out var host, out var hostPort))
        {
            reason = "missing or invalid Host header";
            return false;
        }

        if (_port is not null && hostPort != _port)
        {
            reason = "Host port does not match dev server port";
            return false;
        }

        if (!_allowLan && !IsLoopbackHost(host))
        {
            reason = "Host is not loopback";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(_bindHost) &&
            !_allowLan &&
            !IsWildcardHost(_bindHost) &&
            !IsLoopbackHost(_bindHost) &&
            !string.Equals(host, _bindHost, StringComparison.OrdinalIgnoreCase))
        {
            reason = "Host does not match dev server bind host";
            return false;
        }

        if (!TryParseOrigin(originHeader, out var originHost, out var originPort))
        {
            reason = "missing or invalid Origin header";
            return false;
        }

        if (!string.Equals(originHost, host, StringComparison.OrdinalIgnoreCase))
        {
            reason = "Origin host does not match Host";
            return false;
        }

        var expectedPort = hostPort ?? _port;
        if (expectedPort is not null && originPort is not null && originPort != expectedPort)
        {
            reason = "Origin port does not match Host";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    internal static bool IsLoopbackHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out var ip) && IPAddress.IsLoopback(ip);
    }

    internal static bool IsWildcardHost(string host)
        => string.Equals(host, "*", StringComparison.Ordinal) ||
           string.Equals(host, "+", StringComparison.Ordinal) ||
           string.Equals(host, "0.0.0.0", StringComparison.Ordinal) ||
           string.Equals(host, "::", StringComparison.Ordinal);

    private static bool TryParseOrigin(string? originHeader, out string host, out int? port)
    {
        host = string.Empty;
        port = null;

        if (string.IsNullOrWhiteSpace(originHeader) ||
            !Uri.TryCreate(originHeader, UriKind.Absolute, out var uri) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        host = uri.Host;
        port = uri.IsDefaultPort ? null : uri.Port;
        return true;
    }

    private static bool TryParseAuthority(string? authority, out string host, out int? port)
    {
        host = string.Empty;
        port = null;

        if (string.IsNullOrWhiteSpace(authority))
        {
            return false;
        }

        if (!Uri.TryCreate("http://" + authority.Trim(), UriKind.Absolute, out var uri) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        host = uri.Host;
        port = uri.IsDefaultPort ? null : uri.Port;
        return true;
    }
}
