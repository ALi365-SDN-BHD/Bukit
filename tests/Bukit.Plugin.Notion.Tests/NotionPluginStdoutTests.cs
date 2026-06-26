using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace Bukit.Plugin.Notion.Tests;

public sealed class NotionPluginStdoutTests
{
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
}
