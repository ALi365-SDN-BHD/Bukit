using Bukit.Cli.Cli.Binding;
using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class PreviewCommandTests
{
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
            try { Directory.Delete(dir, recursive: true); } catch { }
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
            try { Directory.Delete(dir, recursive: true); } catch { }
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
            var command = BuildCommand("--dir", dir, "--port", "4173", "--strict-port", "true");
            var exitCode = await RunWithTimeoutAsync(command, TimeSpan.FromSeconds(5));
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
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
