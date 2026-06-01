using Bukit.Cli.Cli.Binding;
using Bukit.Cli.Commands;
using System.Net;
using System.Text.Json;
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

    [Fact]
    public async Task Push_WithoutDryRun_PostsRecordsToNotionApi()
    {
        var seedDir = Path.Combine(_tempDir, "notion-seed");
        Directory.CreateDirectory(seedDir);
        File.WriteAllText(Path.Combine(seedDir, "pages.json"), """
[
  {
    "title": "Home",
    "slug": "home",
    "type": "Home",
    "summary": "Welcome",
    "content": "<p>Hello</p>",
    "language": "zh",
    "published": true,
    "seo_title": "Home SEO",
    "seo_description": "SEO desc"
  }
]
""");

        var requests = new List<HttpRequestMessage>();
        var handler = new RecordingHandler(req =>
        {
            requests.Add(CloneRequest(req));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"page-1"}""")
            };
        });

        var originalFactory = NotionCommand.CreateHttpClient;
        var originalToken = Environment.GetEnvironmentVariable("BUKIT_TEST_NOTION_TOKEN");
        NotionCommand.CreateHttpClient = () => new HttpClient(handler);
        Environment.SetEnvironmentVariable("BUKIT_TEST_NOTION_TOKEN", "secret_test");
        try
        {
            var reportPath = Path.Combine(_tempDir, "push-report.json");
            var result = await NotionCommand.RunAsync(MakeCommand(new Dictionary<string, string?>
            {
                ["--input"] = seedDir,
                ["--database-id"] = "db123",
                ["--token-env"] = "BUKIT_TEST_NOTION_TOKEN",
                ["--report"] = reportPath
            }, ["push"]));

            Assert.Equal(0, result);
            Assert.Single(requests);
            Assert.Equal(HttpMethod.Post, requests[0].Method);
            Assert.Equal("https://api.notion.com/v1/pages", requests[0].RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer secret_test", requests[0].Headers.Authorization!.ToString());
            Assert.Equal("2022-06-28", requests[0].Headers.GetValues("Notion-Version").Single());

            var payload = await requests[0].Content!.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(payload);
            Assert.Equal("db123", doc.RootElement.GetProperty("parent").GetProperty("database_id").GetString());
            Assert.True(doc.RootElement.GetProperty("properties").TryGetProperty("Title", out _));
            Assert.Equal("Home", doc.RootElement.GetProperty("properties").GetProperty("Title").GetProperty("title")[0].GetProperty("text").GetProperty("content").GetString());
            var report = File.ReadAllText(reportPath);
            Assert.Contains("\"created\": 1", report);
            Assert.Contains("\"notionPageId\": \"page-1\"", report);
        }
        finally
        {
            NotionCommand.CreateHttpClient = originalFactory;
            Environment.SetEnvironmentVariable("BUKIT_TEST_NOTION_TOKEN", originalToken);
        }
    }

    [Fact]
    public async Task Push_AppendFailed_MarksAppendFailed()
    {
        var seedDir = Path.Combine(_tempDir, "notion-seed");
        Directory.CreateDirectory(seedDir);
        var records = new List<ImportSeedRecord>
        {
            new("page", "Home", "home", null, "<p>Content</p>", "zh", true, null, null)
        };

        var blocksRequested = false;
        var handler = new RecordingHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/query"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"results":[{"id":"page-1"}],"has_more":false}""")
                };
            if (req.Method == HttpMethod.Patch && req.RequestUri.AbsolutePath.Contains("/pages/"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"id":"page-1"}""")
                };
            if (req.Method == HttpMethod.Patch && req.RequestUri.AbsolutePath.Contains("/blocks/"))
            {
                blocksRequested = true;
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"page-1"}""")
            };
        });

        var reportPath = Path.Combine(seedDir, "notion-push-report.json");
        using var http = new HttpClient(handler);
        var result = await NotionSeedPusher.PushAsync(http, records, new NotionPushOptions(
            DatabaseId: "db123",
            Token: "token",
            ReportPath: reportPath,
            DryRun: false,
            Mode: "upsert",
            UpdateContent: "append"));

        Assert.True(blocksRequested, "Expected PATCH /blocks/{id}/children for append mode");
        Assert.True(File.Exists(reportPath));
        Assert.Contains("append-failed", File.ReadAllText(reportPath));
    }

    [Fact]
    public async Task Push_ReplaceFailed_MarksReplaceFailed()
    {
        var seedDir = Path.Combine(_tempDir, "notion-seed");
        Directory.CreateDirectory(seedDir);
        var records = new List<ImportSeedRecord>
        {
            new("page", "Home", "home", null, "<p>Content</p>", "zh", true, null, null)
        };

        var blocksReadAttempted = false;
        var handler = new RecordingHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/blocks/") &&
                req.Method == HttpMethod.Get)
            {
                blocksReadAttempted = true;
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
            if (req.RequestUri.AbsolutePath.Contains("/query"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"results":[{"id":"page-1"}],"has_more":false}""")
                };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"page-1"}""")
            };
        });

        var reportPath = Path.Combine(seedDir, "notion-push-report.json");
        using var http = new HttpClient(handler);
        var result = await NotionSeedPusher.PushAsync(http, records, new NotionPushOptions(
            DatabaseId: "db123",
            Token: "token",
            ReportPath: reportPath,
            DryRun: false,
            Mode: "upsert",
            UpdateContent: "replace"));

        Assert.True(blocksReadAttempted, "Expected GET /blocks/{id}/children for replace mode");
        Assert.True(File.Exists(reportPath));
        Assert.Contains("replace-failed", File.ReadAllText(reportPath));
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        if (request.Content is not null)
            clone.Content = new StringContent(request.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        return clone;
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
