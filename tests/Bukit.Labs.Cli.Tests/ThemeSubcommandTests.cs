using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Labs.Cli.Commands;
using Xunit;

namespace Bukit.Labs.Cli.Tests;

[Collection("Console")]
public sealed class ThemeSubcommandTests : IDisposable
{
    private readonly string _rootDir;

    public ThemeSubcommandTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-labs-theme-subcommands-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_rootDir, recursive: true);
    }

    [Fact]
    public async Task InitCommand_RunAsync_WithoutTargetDirectory_ReturnsTwo()
    {
        var result = await CommandTestSupport.CaptureAsync(() =>
            InitCommand.RunAsync(new CliBoundCommand(new Dictionary<string, string?>(), [])));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("init requires a target directory.", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InitCommand_RunAsync_UnknownTemplate_ReturnsTwo()
    {
        var result = await CommandTestSupport.CaptureAsync(() =>
            InitCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?> { ["--template"] = "mystery" },
                ["site-root"])));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Unknown template: mystery.", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TemplateCommand_RunAsync_WithoutSubcommand_ReturnsTwo()
    {
        var result = await CommandTestSupport.CaptureAsync(() =>
            TemplateCommand.RunAsync(new CliBoundCommand(new Dictionary<string, string?>(), [])));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Usage: bukit template <create|list|show|validate|snippets|hints|sync>", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThemePackCommand_RunAsync_WithoutThemeName_ReturnsTwo()
    {
        using var scope = new CommandTestSupport.CurrentDirectoryScope(_rootDir);

        var result = await CommandTestSupport.CaptureAsync(() =>
            ThemePackCommand.RunAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["pack"])));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Missing theme name. Usage: bukit theme pack <name>", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThemeInstallCommand_RunAsync_WithoutSource_ReturnsTwo()
    {
        using var scope = new CommandTestSupport.CurrentDirectoryScope(_rootDir);

        var result = await CommandTestSupport.CaptureAsync(() =>
            ThemeInstallCommand.RunAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["install"])));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Missing source. Usage: bukit theme install", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThemeRegistryCommand_SearchAsync_WithLoopbackRegistryUrl_ReturnsOne()
    {
        var isolatedHome = Path.Combine(_rootDir, "home");
        Directory.CreateDirectory(isolatedHome);
        using var home = new CommandTestSupport.EnvironmentVariableScope("HOME", isolatedHome);
        using var userProfile = new CommandTestSupport.EnvironmentVariableScope("USERPROFILE", isolatedHome);

        var result = await CommandTestSupport.CaptureAsync(() =>
            ThemeRegistryCommand.SearchAsync(new CliBoundCommand(
                new Dictionary<string, string?> { ["--registry-url"] = "http://127.0.0.1:8787/themes.yaml" },
                ["search"])));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Experimental: theme registry/search/install", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("BKT-THEME-REGISTRY-0002", result.StdErr, StringComparison.Ordinal);
        Assert.Contains("Failed to load theme registry.", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThemeWizardCommand_RunAsync_InvalidName_ReturnsTwo()
    {
        var result = await CommandTestSupport.CaptureAsync(() =>
            ThemeWizardCommand.RunAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["wizard", "../bad"])));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Missing or invalid theme name.", result.StdErr, StringComparison.Ordinal);
    }
}
