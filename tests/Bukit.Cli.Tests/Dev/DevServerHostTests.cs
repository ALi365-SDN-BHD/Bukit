using System.Net;
using System.Net.Sockets;
using Bukit.Cli.Commands.Dev;
using Bukit.Shared;
using Xunit;

namespace Bukit.Cli.Tests.Dev;

public sealed class DevServerHostTests
{
    [Fact]
    public void Start_RequestedPortZero_ReturnsNonZeroPort()
    {
        var logger = new TestLogger();
        using var host = DevServerHost.Start("localhost", 0, logger);

        Assert.True(host.Port > 0);
        Assert.True(host.Port <= 65535);
    }

    [Fact]
    public void Start_OccupiedPort_UsesNextAvailable()
    {
        var logger = new TestLogger();
        var chosen = GetFreePort();

        using var occupier = new HttpListener();
        occupier.Prefixes.Add($"http://localhost:{chosen}/");
        occupier.Start();

        using var host = DevServerHost.Start("localhost", chosen, logger);

        Assert.True(host.Port > 0);
        Assert.NotEqual(chosen, host.Port);
        Assert.Contains(logger.Infos, i => i.Contains($"Port {chosen} unavailable"));
    }

    [Fact]
    public void Prefix_HasCorrectFormat()
    {
        var logger = new TestLogger();
        var port = GetFreePort();
        using var host = DevServerHost.Start("localhost", port, logger);

        Assert.Equal($"http://localhost:{host.Port}/", host.Prefix);
        Assert.StartsWith("http://", host.Prefix);
        Assert.EndsWith("/", host.Prefix);
    }

    [Fact]
    public void Dispose_PortStillAccessible()
    {
        var logger = new TestLogger();
        var port = GetFreePort();
        var host = DevServerHost.Start("localhost", port, logger);
        var assignedPort = host.Port;

        host.Dispose();

        Assert.Equal(assignedPort, host.Port);
    }

    [Fact]
    public async Task RunAcceptLoopAsync_Cancellation_ExitsCleanly()
    {
        var logger = new TestLogger();
        var port = GetFreePort();
        using var host = DevServerHost.Start("localhost", port, logger);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        await host.RunAcceptLoopAsync(_ => Task.CompletedTask, cts.Token);
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

    private sealed class TestLogger : ILogger
    {
        public List<string> Infos { get; } = new();
        public List<string> Errors { get; } = new();
        public List<string> Warns { get; } = new();
        public List<string> Debugs { get; } = new();

        public void Info(string message) => Infos.Add(message);
        public void Error(string message) => Errors.Add(message);
        public void Warn(string message) => Warns.Add(message);
        public void Debug(string message) => Debugs.Add(message);
    }
}
