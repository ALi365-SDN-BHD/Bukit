using Xunit;

namespace Bukit.Importing.Tests;

public sealed class ImportCommandWorkflowTests : IDisposable
{
    private readonly string _rootDir;

    public ImportCommandWorkflowTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-importing-workflow-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
            Directory.Delete(_rootDir, recursive: true);
    }

    [Fact]
    public async Task RunAsync_UnknownSubcommand_ReturnsCurrentErrorMessages()
    {
        var result = await ImportCommandWorkflow.RunAsync(BaseOptions("mystery"));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains(result.Messages, m => m.Level == "error" && m.Message == "未知的 import 子命令: mystery");
        Assert.Contains(result.Messages, m => m.Level == "error" && m.Message == "可用: html-demo");
    }

    [Fact]
    public async Task RunAsync_SeedWithoutOutput_ReturnsCurrentErrorMessage()
    {
        var seedDir = Path.Combine(_rootDir, "seed");
        Directory.CreateDirectory(seedDir);

        var result = await ImportCommandWorkflow.RunAsync(BaseOptions("seed") with
        {
            SeedDir = "seed"
        });

        Assert.Equal(2, result.ExitCode);
        Assert.Contains(result.Messages, m => m.Level == "error" && m.Message == "缺少必填选项: --output <content-dir>");
    }

    [Fact]
    public async Task RunAsync_SeedSuccess_WritesMarkdownAndReturnsCurrentCompletionMessage()
    {
        var seedDir = Path.Combine(_rootDir, "seed");
        Directory.CreateDirectory(seedDir);
        File.WriteAllText(Path.Combine(seedDir, "posts.json"), """
[
  { "title": "Hello", "slug": "hello", "content": "Body" }
]
""");

        var result = await ImportCommandWorkflow.RunAsync(BaseOptions("seed") with
        {
            SeedDir = "seed",
            OutputDir = "content"
        });

        Assert.Equal(0, result.ExitCode);
        Assert.NotNull(result.SeedResult);
        Assert.True(File.Exists(Path.Combine(_rootDir, "content", "posts", "hello.md")));
        Assert.Contains(result.Messages, m =>
            m.Level == "info" &&
            m.Message == $"seed import 完成: records=1 written=1 output={Path.Combine(_rootDir, "content")}");
    }

    [Fact]
    public async Task RunAsync_HtmlDemoDryRun_DoesNotWriteTheme()
    {
        var demoDir = CreateMinimalDemo();

        var result = await ImportCommandWorkflow.RunAsync(BaseOptions("html-demo") with
        {
            DemoDir = demoDir,
            ThemeName = "demo-theme",
            DryRun = true
        });

        Assert.Equal(0, result.ExitCode);
        Assert.NotNull(result.HtmlDemoResult);
        Assert.False(Directory.Exists(Path.Combine(_rootDir, "themes", "demo-theme")));
        Assert.Contains(result.Messages, m => m.Level == "info" && m.Message.Contains("未提取到共享布局", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_HtmlDemoPushNotionCannotCombineWithDryRun()
    {
        var demoDir = CreateMinimalDemo();

        var result = await ImportCommandWorkflow.RunAsync(BaseOptions("html-demo") with
        {
            DemoDir = demoDir,
            ThemeName = "demo-theme",
            DryRun = true,
            PushNotion = true
        });

        Assert.Equal(2, result.ExitCode);
        Assert.Contains(result.Messages, m =>
            m.Level == "error" &&
            m.Message == "--push-notion 不能与 --dry-run 同时使用。先生成草稿后再执行实际推送。");
    }

    [Fact]
    public async Task RunAsync_HtmlDemoNotionBuildSourceRequiresNotionContentSource()
    {
        var demoDir = CreateMinimalDemo();

        var result = await ImportCommandWorkflow.RunAsync(BaseOptions("html-demo") with
        {
            DemoDir = demoDir,
            ThemeName = "demo-theme",
            ContentSource = "json",
            BuildSource = "notion"
        });

        Assert.Equal(2, result.ExitCode);
        Assert.Contains(result.Messages, m =>
            m.Level == "error" &&
            m.Message == "--build-source notion requires --content-source notion.");
    }

    [Fact]
    public async Task RunAsync_HtmlDemoExistingThemeWithoutForceReturnsCurrentError()
    {
        var demoDir = CreateMinimalDemo();
        Directory.CreateDirectory(Path.Combine(_rootDir, "themes", "demo-theme"));

        var result = await ImportCommandWorkflow.RunAsync(BaseOptions("html-demo") with
        {
            DemoDir = demoDir,
            ThemeName = "demo-theme"
        });

        Assert.Equal(2, result.ExitCode);
        Assert.Contains(result.Messages, m =>
            m.Level == "error" &&
            m.Message == "主题已存在: demo-theme。使用 --force 覆盖。");
    }

    private ImportCommandOptions BaseOptions(string subcommand)
        => new()
        {
            Subcommand = subcommand,
            RootDir = _rootDir,
            WorkingDir = _rootDir
        };

    private string CreateMinimalDemo()
    {
        var demoDir = Path.Combine(_rootDir, "demo");
        Directory.CreateDirectory(demoDir);
        File.WriteAllText(Path.Combine(demoDir, "index.html"), """
<!doctype html>
<html>
<head><title>Demo</title></head>
<body><main><h1>Hello</h1><p>World</p></main></body>
</html>
""");
        return demoDir;
    }
}
