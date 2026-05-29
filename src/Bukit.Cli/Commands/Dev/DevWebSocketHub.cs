using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using Bukit.Shared;

namespace Bukit.Cli.Commands.Dev;

internal sealed class DevWebSocketHub : IDevWebSocketHub
{
    private readonly ConcurrentDictionary<string, WebSocket> _clients = new();
    private readonly ILogger _logger;

    public DevWebSocketHub(ILogger logger)
    {
        _logger = logger;
    }

    public int ClientCount => _clients.Count;

    public async Task HandleUpgradeAsync(HttpListenerContext context, CancellationToken ct)
    {
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
