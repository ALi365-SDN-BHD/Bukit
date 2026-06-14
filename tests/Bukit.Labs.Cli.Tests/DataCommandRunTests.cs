using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Labs.Cli.Commands;
using Xunit;

namespace Bukit.Labs.Cli.Tests;

[Collection("Console")]
public sealed class DataCommandRunTests : IDisposable
{
    private readonly string _rootDir;

    public DataCommandRunTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-labs-data-run-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_rootDir, recursive: true);
    }

    [Fact]
    public async Task RunAsync_InspectAndDump_WorkOnInitializedLandingSite()
    {
        var siteDir = Path.Combine(_rootDir, "landing-site");

        await InitCommand.RunAsync(new CliBoundCommand(
            new Dictionary<string, string?> { ["--template"] = "landing" },
            [siteDir]));

        using var scope = new CommandTestSupport.CurrentDirectoryScope(siteDir);

        var inspectResult = await CommandTestSupport.CaptureAsync(() =>
            DataCommand.RunAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["inspect"])));

        Assert.Equal(0, inspectResult.ExitCode);
        Assert.Contains("Data modules:", inspectResult.StdOut, StringComparison.Ordinal);
        Assert.Contains("features", inspectResult.StdOut, StringComparison.Ordinal);
        Assert.Contains("call_to_action", inspectResult.StdOut, StringComparison.Ordinal);

        var dumpResult = await CommandTestSupport.CaptureAsync(() =>
            DataCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?> { ["--format"] = "json" },
                ["dump"])));

        Assert.Equal(0, dumpResult.ExitCode);
        Assert.Contains("\"modules\"", dumpResult.StdOut, StringComparison.Ordinal);
        Assert.Contains("\"features\"", dumpResult.StdOut, StringComparison.Ordinal);
    }
}
