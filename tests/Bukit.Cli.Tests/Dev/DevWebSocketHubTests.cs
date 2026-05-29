using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using Bukit.Cli.Commands.Dev;
using Bukit.Shared;
using Xunit;

namespace Bukit.Cli.Tests.Dev;

public sealed class DevWebSocketHubTests
{
    [Fact]
    public void TwoHubInstances_HaveIndependentClientCount()
    {
        var logger = new TestLogger();
        var hub1 = new DevWebSocketHub(logger);
        var hub2 = new DevWebSocketHub(logger);

        Assert.Equal(0, hub1.ClientCount);
        Assert.Equal(0, hub2.ClientCount);
    }

    [Fact]
    public async Task BroadcastReloadAsync_NoClients_DoesNotThrow()
    {
        var logger = new TestLogger();
        var hub = new DevWebSocketHub(logger);

        var exception = await Record.ExceptionAsync(() => hub.BroadcastReloadAsync());

        Assert.Null(exception);
        Assert.Equal(0, hub.ClientCount);
    }

    [Fact]
    public async Task HandleUpgradeAsync_NonWebSocketRequest_LogsWarn()
    {
        var logger = new TestLogger();
        var hub = new DevWebSocketHub(logger);

        var port = GetFreePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var requestTask = Task.Run(async () =>
        {
            try { await httpClient.GetAsync($"http://localhost:{port}/test"); }
            catch { }
        });

        var context = await listener.GetContextAsync();
        await hub.HandleUpgradeAsync(context, CancellationToken.None);

        await requestTask;

        Assert.Contains(logger.Warns, w => w.StartsWith("dev.ws.upgrade:"));
        Assert.Equal(0, hub.ClientCount);
    }

    [Fact]
    public async Task BroadcastReloadAsync_DeadClient_CleanedUp()
    {
        var logger = new TestLogger();
        var hub = new DevWebSocketHub(logger);

        var port = GetFreePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();

        using var wsClient = new ClientWebSocket();
        var connectTask = wsClient.ConnectAsync(new Uri($"ws://localhost:{port}/"), CancellationToken.None);

        var serverContext = await listener.GetContextAsync();
        var handleTask = hub.HandleUpgradeAsync(serverContext, CancellationToken.None);

        await connectTask;
        Assert.Equal(1, hub.ClientCount);

        await wsClient.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
        await handleTask;

        Assert.Equal(0, hub.ClientCount);
    }

    private static int GetFreePort()
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
