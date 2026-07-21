using System.Net;
using System.Net.Sockets;
using Bukit.Cli.Commands;
using Bukit.Cli.Shared.Cli.Binding;
using Xunit;
using Xunit.Sdk;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class PreviewAnalyticsPolicyFailureTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(),
        "bukit-preview-analytics-policy-" + Guid.NewGuid().ToString("N"));

    public PreviewAnalyticsPolicyFailureTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_tempDir, recursive: true);
    }

    [Fact]
    public async Task RunAsync_WhenFallbackConfigIsMalformed_ExitsBeforeServingAndReportsSource()
    {
        var outputDir = CreateOutput();
        var configPath = Path.Combine(_tempDir, "site.yaml");
        File.WriteAllText(configPath, "site:\n  analytics: [\n");

        var result = await RunCapturingErrorAsync(outputDir, cancellationAfter: TimeSpan.FromMilliseconds(250));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Preview analytics policy error", result.Error, StringComparison.Ordinal);
        Assert.Contains(configPath, result.Error, StringComparison.Ordinal);
        Assert.Contains("Config", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WhenFallbackConfigCannotBeRead_ExitsBeforeServingAndReportsSource()
    {
        if (OperatingSystem.IsWindows())
        {
            throw SkipException.ForSkip("Unix file modes are unavailable on Windows.");
        }

        var outputDir = CreateOutput();
        var configPath = Path.Combine(_tempDir, "site.yaml");
        File.WriteAllText(configPath, MinimalConfigYaml());
        File.SetUnixFileMode(configPath, UnixFileMode.None);

        try
        {
            if (CanRead(configPath))
            {
                throw SkipException.ForSkip(
                    "The current Unix identity can still read a mode-000 file; unreadable fallback cannot be exercised.");
            }

            var result = await RunCapturingErrorAsync(outputDir, cancellationAfter: TimeSpan.FromMilliseconds(250));

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("Preview analytics policy error", result.Error, StringComparison.Ordinal);
            Assert.Contains(configPath, result.Error, StringComparison.Ordinal);
        }
        finally
        {
            File.SetUnixFileMode(configPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Fact]
    public async Task RunAsync_WhenNoFallbackConfigExists_WarnsAndKeepsManagedAnalytics()
    {
        var outputDir = CreateOutput();
        var port = PickFreePort();
        var command = BuildCommand(outputDir, port);
        var originalError = Console.Error;
        using var error = new StringWriter();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        Console.SetError(error);

        var previewTask = PreviewCommand.RunAsync(command, cancellation.Token);
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var response = await RequestWithRetryAsync(client, port, previewTask, cancellation.Token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("managed-analytics", response.Body, StringComparison.Ordinal);
            Assert.Contains("No site.yaml found", error.ToString(), StringComparison.Ordinal);
            Assert.Contains("managed Analytics blocks will be kept", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            cancellation.Cancel();
            Assert.Equal(0, await previewTask.WaitAsync(TimeSpan.FromSeconds(5)));
            Console.SetError(originalError);
        }
    }

    private async Task<(int ExitCode, string Error)> RunCapturingErrorAsync(
        string outputDir,
        TimeSpan cancellationAfter)
    {
        var originalError = Console.Error;
        using var error = new StringWriter();
        using var cancellation = new CancellationTokenSource(cancellationAfter);
        Console.SetError(error);

        try
        {
            var exitCode = await PreviewCommand.RunAsync(
                BuildCommand(outputDir, PickFreePort()),
                cancellation.Token);
            return (exitCode, error.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    private string CreateOutput()
    {
        var outputDir = Path.Combine(_tempDir, "dist");
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(outputDir, "index.html"), """
            <html><head>
            <!-- bukit:analytics:google-analytics:G-ABCDE123:head:start -->
            <script>managed-analytics</script>
            <!-- bukit:analytics:google-analytics:G-ABCDE123:head:end -->
            </head><body>preview</body></html>
            """);
        return outputDir;
    }

    private static CliBoundCommand BuildCommand(string outputDir, int port)
        => new(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--dir"] = outputDir,
                ["--host"] = IPAddress.Loopback.ToString(),
                ["--port"] = port.ToString(),
                ["--strict-port"] = "true"
            },
            Array.Empty<string>());

    private static async Task<(HttpStatusCode StatusCode, string Body)> RequestWithRetryAsync(
        HttpClient client,
        int port,
        Task<int> previewTask,
        CancellationToken cancellationToken)
    {
        var uri = new Uri($"http://{IPAddress.Loopback}:{port}/");
        for (var attempt = 0; attempt < 40; attempt++)
        {
            if (previewTask.IsCompleted)
            {
                throw new InvalidOperationException($"Preview stopped before serving: {await previewTask}");
            }

            try
            {
                using var response = await client.GetAsync(uri, cancellationToken);
                return (
                    response.StatusCode,
                    await response.Content.ReadAsStringAsync(cancellationToken));
            }
            catch (HttpRequestException)
            {
                await Task.Delay(25, cancellationToken);
            }
        }

        throw new TimeoutException("Preview did not start before the request deadline.");
    }

    private static int PickFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static bool CanRead(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static string MinimalConfigYaml()
        => """
            site:
              name: preview-policy
              title: Preview Policy
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            """;
}
