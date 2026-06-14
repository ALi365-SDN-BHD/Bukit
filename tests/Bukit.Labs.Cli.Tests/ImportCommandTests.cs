using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Labs.Cli.Commands;
using Xunit;

namespace Bukit.Labs.Cli.Tests;

[Collection("Console")]
public sealed class ImportCommandTests : IDisposable
{
    private readonly string _rootDir;

    public ImportCommandTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-labs-import-command-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_rootDir, recursive: true);
    }

    [Fact]
    public async Task RunAsync_UnknownSubcommand_ReturnsTwo()
    {
        var result = await CommandTestSupport.CaptureAsync(() =>
            ImportCommand.RunAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["mystery"])));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("未知的 import 子命令: mystery", result.StdErr, StringComparison.Ordinal);
        Assert.Contains("可用: html-demo", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_SeedWithoutInputDirectory_ReturnsTwo()
    {
        var result = await CommandTestSupport.CaptureAsync(() =>
            ImportCommand.RunAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["seed"])));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("缺少必填参数: <seed-dir>", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_SeedWithoutOutputOption_ReturnsTwo()
    {
        var seedDir = Path.Combine(_rootDir, "seed");
        Directory.CreateDirectory(seedDir);

        var result = await CommandTestSupport.CaptureAsync(() =>
            ImportCommand.RunAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["seed", seedDir])));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("缺少必填选项: --output <content-dir>", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_HtmlDemoWithoutDemoDirectory_ReturnsTwo()
    {
        var result = await CommandTestSupport.CaptureAsync(() =>
            ImportCommand.RunAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["html-demo"])));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("缺少必填参数: <demo-dir>", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_HtmlDemoWithInvalidThemeName_ReturnsTwo()
    {
        var demoDir = Path.Combine(_rootDir, "demo");
        Directory.CreateDirectory(demoDir);

        var result = await CommandTestSupport.CaptureAsync(() =>
            ImportCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?> { ["--theme"] = "../bad-theme" },
                ["html-demo", demoDir])));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("无效的主题名: ../bad-theme", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_HtmlDemo_PushNotionCannotBeCombinedWithDryRun()
    {
        using var scope = new CommandTestSupport.CurrentDirectoryScope(_rootDir);
        var demoDir = Path.Combine(_rootDir, "demo");
        Directory.CreateDirectory(demoDir);

        var result = await CommandTestSupport.CaptureAsync(() =>
            ImportCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?>
                {
                    ["--theme"] = "demo-theme",
                    ["--push-notion"] = "true",
                    ["--dry-run"] = "true"
                },
                ["html-demo", demoDir])));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("--push-notion 不能与 --dry-run 同时使用。", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_HtmlDemo_NotionBuildSourceRequiresNotionContentSource()
    {
        using var scope = new CommandTestSupport.CurrentDirectoryScope(_rootDir);
        var demoDir = Path.Combine(_rootDir, "demo");
        Directory.CreateDirectory(demoDir);

        var result = await CommandTestSupport.CaptureAsync(() =>
            ImportCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?>
                {
                    ["--theme"] = "demo-theme",
                    ["--build-source"] = "notion",
                    ["--content-source"] = "json"
                },
                ["html-demo", demoDir])));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("--build-source notion requires --content-source notion.", result.StdErr, StringComparison.Ordinal);
    }
}
