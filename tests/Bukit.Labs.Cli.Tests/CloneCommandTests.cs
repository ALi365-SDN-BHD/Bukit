using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Labs.Cli.Commands;
using Xunit;

namespace Bukit.Labs.Cli.Tests;

[Collection("Console")]
public sealed class CloneCommandTests : IDisposable
{
    private readonly string _rootDir;

    public CloneCommandTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-labs-clone-command-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_rootDir, recursive: true);
    }

    [Fact]
    public async Task RunAsync_MissingTokens_ReturnsTwo()
    {
        using var scope = new CommandTestSupport.CurrentDirectoryScope(_rootDir);

        var result = await CommandTestSupport.CaptureAsync(() =>
            CloneCommand.RunAsync(new CliBoundCommand(new Dictionary<string, string?>(), [])));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Missing required option: --tokens <file>", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WhenThemeAlreadyExistsWithoutForce_ReturnsTwo()
    {
        using var scope = new CommandTestSupport.CurrentDirectoryScope(_rootDir);
        Directory.CreateDirectory(Path.Combine(_rootDir, "themes", "existing"));

        var result = await CommandTestSupport.CaptureAsync(() =>
            CloneCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?>
                {
                    ["--theme"] = "existing",
                    ["--tokens"] = "tokens.json"
                },
                [])));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Theme already exists: existing. Use --force to overwrite.", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_InvalidVisualThreshold_ReturnsTwo()
    {
        using var scope = new CommandTestSupport.CurrentDirectoryScope(_rootDir);

        var result = await CommandTestSupport.CaptureAsync(() =>
            CloneCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?> { ["--visual-threshold"] = "2" },
                [])));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Invalid --visual-threshold value.", result.StdErr, StringComparison.Ordinal);
    }
}
