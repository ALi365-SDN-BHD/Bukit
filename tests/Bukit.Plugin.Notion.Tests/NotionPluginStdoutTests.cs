using System.Diagnostics;
using System.Text.Json;
using Bukit.Plugin.Abstractions;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Runtime;
using Bukit.Plugin.Abstractions.Security;
using Xunit;

namespace Bukit.Plugin.Notion.Tests;

public sealed class NotionPluginStdoutTests : IDisposable
{
    private readonly string _projectRoot;

    public NotionPluginStdoutTests()
    {
        _projectRoot = Path.Combine(Path.GetTempPath(), "bukit-notion-process-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_projectRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_projectRoot))
        {
            Directory.Delete(_projectRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Process_WritesJsonResponseToStdoutAndLogsToStderr()
    {
        string executable = Path.Combine(
            AppContext.BaseDirectory,
            OperatingSystem.IsWindows() ? "bukit-plugin-notion.exe" : "bukit-plugin-notion");

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start Notion plugin process.");
        await process.StandardInput.WriteAsync(
            """
            {"type":"handshake","protocol":"bukit-plugin-v1","requestId":"req-process","host":{"platform":"test-platform"}}
            """);
        process.StandardInput.Close();

        string stdout = await process.StandardOutput.ReadToEndAsync();
        string stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.Equal(0, process.ExitCode);
        using JsonDocument document = JsonDocument.Parse(stdout);
        Assert.Equal("handshakeResponse", document.RootElement.GetProperty("type").GetString());
        Assert.Equal("req-process", document.RootElement.GetProperty("requestId").GetString());
        Assert.DoesNotContain("bukit-plugin-notion invoked", stdout, StringComparison.Ordinal);
        Assert.Contains("bukit-plugin-notion invoked", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Process_UnexpectedExecutionFailureStillWritesJsonErrorResponseToStdout()
    {
        string seedDir = WriteValidSeed();
        string mapPath = WriteValidMap(seedDir);
        string reportDirectory = Directory.CreateDirectory(
            Path.Combine(_projectRoot, ".bukit", "reports", "plugin-output", "notion", "notion-push-report.json")).FullName;
        var request = new PluginInvokeRequest(
            Type: PluginProtocolConstants.Invoke,
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: "req-process-error",
            Host: new PluginHostInfo("Bukit", "1.0.0", "test-rid"),
            Command: new PluginInvokeCommand(
                Name: "notion",
                Path: ["notion", "push"],
                Arguments: [],
                Options: new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    ["--seed"] = JsonSerializer.SerializeToElement(seedDir),
                    ["--database-map"] = JsonSerializer.SerializeToElement(mapPath),
                    ["--mode"] = JsonSerializer.SerializeToElement("create"),
                    ["--dry-run"] = JsonSerializer.SerializeToElement(true),
                    ["--report"] = JsonSerializer.SerializeToElement(reportDirectory)
                }),
            Context: new PluginInvokeContext(_projectRoot, _projectRoot),
            Permissions: new PluginPermissionSet());

        (int exitCode, string stdout, string stderr) = await RunProcessAsync(
            JsonSerializer.Serialize(request, PluginJsonSerializerContext.Default.PluginInvokeRequest));

        Assert.Equal(1, exitCode);
        using JsonDocument document = JsonDocument.Parse(stdout);
        Assert.Equal("errorResponse", document.RootElement.GetProperty("type").GetString());
        Assert.Equal("req-process-error", document.RootElement.GetProperty("requestId").GetString());
        Assert.Equal("plugin.notion.executionFailed", document.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Contains("bukit-plugin-notion invoked", stderr, StringComparison.Ordinal);
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunProcessAsync(string input)
    {
        string executable = Path.Combine(
            AppContext.BaseDirectory,
            OperatingSystem.IsWindows() ? "bukit-plugin-notion.exe" : "bukit-plugin-notion");

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start Notion plugin process.");
        await process.StandardInput.WriteAsync(input);
        process.StandardInput.Close();

        string stdout = await process.StandardOutput.ReadToEndAsync();
        string stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, stdout, stderr);
    }

    private string WriteValidSeed()
    {
        string seedDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "notion-seed")).FullName;
        File.WriteAllText(Path.Combine(seedDir, "pages.json"), """
[
  {
    "title": "Home",
    "slug": "home"
  }
]
""");
        return seedDir;
    }

    private static string WriteValidMap(string seedDir)
    {
        string mapPath = Path.Combine(seedDir, "notion-database-map.yaml");
        File.WriteAllText(mapPath, """
databases:
  pages:
    seed: pages.json
    collection: page
    dataSourceId: ds-pages
    uniqueField: Slug
    properties:
      Title:
        source: title
        type: title
      Slug:
        source: slug
        type: rich_text
""");
        return mapPath;
    }
}
