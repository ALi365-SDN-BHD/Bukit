using Bukit.Cli.Cli.Binding;
using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class NotionCommandTests : IDisposable
{
    private readonly string _tempDir;

    public NotionCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-notion-cmd-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private static CliBoundCommand MakeCommand(Dictionary<string, string?> options, string[] args)
        => new(options, args);

    [Fact]
    public async Task Push_MissingInput_Returns2()
    {
        var result = await NotionCommand.RunAsync(MakeCommand(new Dictionary<string, string?>
        {
            ["--database-id"] = "db123"
        }, ["push"]));

        Assert.Equal(2, result);
    }

    [Fact]
    public async Task Push_DryRun_WritesPlanReport()
    {
        var seedDir = Path.Combine(_tempDir, "notion-seed");
        Directory.CreateDirectory(seedDir);
        File.WriteAllText(Path.Combine(seedDir, "pages.json"), """
[
  {
    "title": "Home",
    "slug": "",
    "type": "Home",
    "summary": "Welcome",
    "content": "<p>Hello</p>",
    "language": "zh",
    "published": true
  }
]
""");
        File.WriteAllText(Path.Combine(seedDir, "posts.json"), """
[
  {
    "title": "Post",
    "slug": "post",
    "summary": "Post summary",
    "content": "<p>Post body</p>",
    "language": "zh",
    "published": true
  }
]
""");

        var reportPath = Path.Combine(_tempDir, "push-plan.json");
        var result = await NotionCommand.RunAsync(MakeCommand(new Dictionary<string, string?>
        {
            ["--input"] = seedDir,
            ["--database-id"] = "db123",
            ["--dry-run"] = "true",
            ["--report"] = reportPath
        }, ["push"]));

        Assert.Equal(0, result);
        Assert.True(File.Exists(reportPath));
        var report = File.ReadAllText(reportPath);
        Assert.Contains("\"dryRun\": true", report);
        Assert.Contains("\"databaseId\": \"db123\"", report);
        Assert.Contains("\"recordCount\": 2", report);
        Assert.Contains("\"title\": \"Home\"", report);
        Assert.Contains("\"collection\": \"post\"", report);
    }

    [Fact]
    public async Task Push_WithoutDryRun_RequiresToken()
    {
        var seedDir = Path.Combine(_tempDir, "notion-seed");
        Directory.CreateDirectory(seedDir);
        File.WriteAllText(Path.Combine(seedDir, "pages.json"), "[]");

        var result = await NotionCommand.RunAsync(MakeCommand(new Dictionary<string, string?>
        {
            ["--input"] = seedDir,
            ["--database-id"] = "db123",
            ["--token-env"] = "BUKIT_TEST_NOTION_TOKEN_MISSING"
        }, ["push"]));

        Assert.Equal(2, result);
    }
}
