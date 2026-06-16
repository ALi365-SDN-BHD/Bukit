using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Cli.Commands;
using System.Net;
using System.Net.Sockets;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class PreviewCommandTests
{
    [Fact]
    public void ApplyPreviewAnalyticsPolicy_WhenDisabled_RemovesGa4Scripts()
    {
        var html = """
            <html><head>
              <script async src="https://www.googletagmanager.com/gtag/js?id=G-ABC123"></script>
              <script>
                window.dataLayer = window.dataLayer || [];
                function gtag(){dataLayer.push(arguments);}
                gtag('js', new Date());
                gtag('config', 'G-ABC123');
              </script>
            </head><body>ok</body></html>
            """;

        var filtered = PreviewCommand.ApplyPreviewAnalyticsPolicy(html, disableAnalytics: true);

        Assert.DoesNotContain("googletagmanager.com/gtag/js", filtered, StringComparison.Ordinal);
        Assert.DoesNotContain("gtag('config'", filtered, StringComparison.Ordinal);
        Assert.Contains("<body>ok</body>", filtered, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyPreviewAnalyticsPolicy_WhenEnabled_LeavesHtmlUnchanged()
    {
        var html = "<script>gtag('config', 'G-ABC123');</script>";

        var filtered = PreviewCommand.ApplyPreviewAnalyticsPolicy(html, disableAnalytics: false);

        Assert.Equal(html, filtered);
    }

    [Fact]
    public async Task RunAsync_MissingDir_ReturnsExitCode2()
    {
        var command = BuildCommand("--dir", Path.Combine(Path.GetTempPath(), "bukit-preview-nonexistent"));
        var exitCode = await RunWithTimeoutAsync(command, TimeSpan.FromSeconds(5));
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_InvalidPort_ReturnsExitCode2()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bukit-test-preview");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "index.html"), "<html></html>");

        try
        {
            var command = BuildCommand("--dir", dir, "--port", "99999");
            var exitCode = await RunWithTimeoutAsync(command, TimeSpan.FromSeconds(5));
            Assert.Equal(2, exitCode);
        }
        finally
        {
            TestCleanup.DeleteDirectory(dir, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_NegativePort_ReturnsExitCode2()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bukit-test-preview-neg");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "index.html"), "<html></html>");

        try
        {
            var command = BuildCommand("--dir", dir, "--port", "-1");
            var exitCode = await RunWithTimeoutAsync(command, TimeSpan.FromSeconds(5));
            Assert.Equal(2, exitCode);
        }
        finally
        {
            TestCleanup.DeleteDirectory(dir, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_StrictPortUnavailable_Throws()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bukit-test-preview-strict");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "index.html"), "<html></html>");

        try
        {
            using var occupiedPort = new TcpListener(IPAddress.Loopback, 0);
            occupiedPort.Start();
            var port = ((IPEndPoint)occupiedPort.LocalEndpoint).Port;

            var command = BuildCommand("--dir", dir, "--port", port.ToString(), "--strict-port", "true");
            await Assert.ThrowsAnyAsync<HttpListenerException>(() => PreviewCommand.RunAsync(command));
        }
        finally
        {
            TestCleanup.DeleteDirectory(dir, recursive: true);
        }
    }

    private static CliBoundCommand BuildCommand(params string[] keyValues)
    {
        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < keyValues.Length; i += 2)
        {
            dict[keyValues[i]] = keyValues[i + 1];
        }
        return new CliBoundCommand(dict, Array.Empty<string>());
    }

    private static async Task<int> RunWithTimeoutAsync(CliBoundCommand command, TimeSpan timeout)
    {
        try
        {
            using var cts = new CancellationTokenSource(timeout);
            var task = PreviewCommand.RunAsync(command);
            var completed = await Task.WhenAny(task, Task.Delay(timeout));
            if (completed == task)
            {
                return await task;
            }
            return 0;
        }
        catch (Exception)
        {
            return 2;
        }
    }
}
