using System.Net;
using System.Net.Http;
using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Labs.Cli.Commands;
using Xunit;

namespace Bukit.Labs.Cli.Tests;

[Collection("Console")]
public sealed class NotionCommandDeepTests : IDisposable
{
    private readonly string _rootDir;

    public NotionCommandDeepTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-labs-notion-deep-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        NotionCommand.CreateHttpClient = () => new HttpClient();
        TestCleanup.DeleteDirectory(_rootDir, recursive: true);
    }

    [Fact]
    public async Task RunAsync_PushSingleDatabaseDryRun_WritesReport()
    {
        var inputDir = Path.Combine(_rootDir, "seed");
        Directory.CreateDirectory(inputDir);
        File.WriteAllText(Path.Combine(inputDir, "pages.json"), """
[
  {
    "title": "About",
    "slug": "about",
    "summary": "About page",
    "content": "<p>Hello</p>"
  }
]
""");
        var reportPath = Path.Combine(_rootDir, "single-report.json");

        var result = await CommandTestSupport.CaptureAsync(() =>
            NotionCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?>
                {
                    ["--input"] = inputDir,
                    ["--database-id"] = "db-single",
                    ["--dry-run"] = "",
                    ["--report"] = reportPath
                },
                ["push"])));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("notion push dry-run 完成: records=1", result.StdOut, StringComparison.Ordinal);
        Assert.True(File.Exists(reportPath));
        Assert.Contains("\"dryRun\": true", File.ReadAllText(reportPath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_PushDryRunWithCreateMissingDatabases_WritesGeneratedMap()
    {
        var inputDir = Path.Combine(_rootDir, "multi-seed");
        Directory.CreateDirectory(inputDir);
        File.WriteAllText(Path.Combine(inputDir, "posts.json"), """
[
  {
    "title": "Hello",
    "slug": "hello"
  }
]
""");
        File.WriteAllText(Path.Combine(inputDir, "navigation.yaml"), """
- name: Main nav
  slug: main-nav
""");
        var reportPath = Path.Combine(_rootDir, "multi-report.json");
        var mapPath = Path.Combine(_rootDir, "generated-map.yaml");

        var result = await CommandTestSupport.CaptureAsync(() =>
            NotionCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?>
                {
                    ["--input"] = inputDir,
                    ["--dry-run"] = "",
                    ["--create-missing-databases"] = "",
                    ["--parent-page-id"] = "parent-page",
                    ["--generated-database-map"] = mapPath,
                    ["--report"] = reportPath
                },
                ["push"])));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("databases=2 records=2", result.StdOut, StringComparison.Ordinal);
        Assert.True(File.Exists(reportPath));
        Assert.True(File.Exists(mapPath));

        var generatedMap = File.ReadAllText(mapPath);
        Assert.Contains("posts:", generatedMap, StringComparison.Ordinal);
        Assert.Contains("navigation:", generatedMap, StringComparison.Ordinal);
        Assert.Contains("uniqueField: Slug", generatedMap, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ValidateSchema_WithStubbedHttpClient_WritesPassingReport()
    {
        const string responseJson = """
{
  "properties": {
    "Title": { "type": "title" },
    "Slug": { "type": "rich_text" },
    "Type": { "type": "select" },
    "Summary": { "type": "rich_text" },
    "Content": { "type": "rich_text" },
    "Language": { "type": "select" },
    "Published": { "type": "checkbox" },
    "SeoTitle": { "type": "rich_text" },
    "SeoDescription": { "type": "rich_text" }
  }
}
""";

        NotionCommand.CreateHttpClient = () => new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson)
            }));

        using var token = new CommandTestSupport.EnvironmentVariableScope("NOTION_TOKEN", "secret");
        var reportPath = Path.Combine(_rootDir, "schema-report.json");

        var result = await CommandTestSupport.CaptureAsync(() =>
            NotionCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?>
                {
                    ["--database-id"] = "db-schema",
                    ["--report"] = reportPath
                },
                ["validate-schema"])));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("schema validation: PASSED", result.StdOut, StringComparison.Ordinal);
        Assert.True(File.Exists(reportPath));
        Assert.Contains("\"success\": true", File.ReadAllText(reportPath), StringComparison.Ordinal);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
