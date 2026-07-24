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
        Assert.Contains(result.Messages, m => m.Level == "error" && m.Message == "Unknown import subcommand: mystery");
        Assert.Contains(result.Messages, m => m.Level == "error" && m.Message == "Available: html-demo, seed");
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
        Assert.Contains(result.Messages, m => m.Level == "error" && m.Message == "Missing required option: --output <content-dir>");
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
            m.Message == $"seed import complete: records=1 written=1 output={Path.Combine(_rootDir, "content")}");
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
        Assert.Contains(result.Messages, m => m.Level == "info" && m.Message.Contains("No shared layout", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "import.content.author_missing" &&
            diagnostic.Severity == "warning" &&
            diagnostic.Path == Path.Combine(_rootDir, "sites", "demo-theme", "content"));
        Assert.Contains(result.HtmlDemoResult.Diagnostics, diagnostic =>
            diagnostic.Code == "import.content.entities_missing" &&
            diagnostic.Severity == ImportDiagnosticSeverity.Warning);
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
            m.Message == "--push-notion cannot be used with --dry-run. Generate first, then push.");
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
            m.Message == "Theme already exists: demo-theme. Use --force to overwrite.");
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
