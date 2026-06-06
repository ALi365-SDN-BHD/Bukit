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
        TestCleanup.DeleteDirectory(_tempDir, recursive: true);
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
                ["--no-validate-schema"] = "true",
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
    public async Task Push_SingleDatabaseSchemaValidationFails_BlocksPush()
    {
        var seedDir = Path.Combine(_tempDir, "notion-seed");
        Directory.CreateDirectory(seedDir);
        File.WriteAllText(Path.Combine(seedDir, "pages.json"), """
[
  { "title": "Home", "slug": "home", "published": true }
]
""");

        var pageCreateAttempted = false;
        var handler = new RecordingHandler(req =>
        {
            if (req.Method == HttpMethod.Get && req.RequestUri!.AbsolutePath.Contains("/databases/"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"properties":{}}""")
                };
            if (req.Method == HttpMethod.Post && req.RequestUri!.AbsoluteUri == "https://api.notion.com/v1/pages")
                pageCreateAttempted = true;
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
            var result = await NotionCommand.RunAsync(MakeCommand(new Dictionary<string, string?>
            {
                ["--input"] = seedDir,
                ["--database-id"] = "db123",
                ["--token-env"] = "BUKIT_TEST_NOTION_TOKEN"
            }, ["push"]));

            Assert.Equal(2, result);
            Assert.False(pageCreateAttempted);
        }
        finally
        {
            NotionCommand.CreateHttpClient = originalFactory;
            Environment.SetEnvironmentVariable("BUKIT_TEST_NOTION_TOKEN", originalToken);
        }
    }

    [Fact]
    public async Task Push_WithDatabaseMap_RoutesCollectionsToMappedDatabases()
    {
        var seedDir = Path.Combine(_tempDir, "notion-seed");
        Directory.CreateDirectory(seedDir);
        File.WriteAllText(Path.Combine(seedDir, "pages.json"), """
[
  { "title": "Home", "slug": "home", "summary": "Welcome", "content": "<p>Hello</p>", "language": "zh", "published": true }
]
""");
        File.WriteAllText(Path.Combine(seedDir, "posts.json"), """
[
  { "title": "Post", "slug": "post", "summary": "Post summary", "content": "<p>Post body</p>", "language": "zh", "published": true }
]
""");
        var mapPath = Path.Combine(_tempDir, "notion-map.yaml");
        File.WriteAllText(mapPath, """
databases:
  pages:
    databaseId: "pages-db"
    seed: "pages.json"
  posts:
    databaseId: "posts-db"
    seed: "posts.json"
""");

        var requests = new List<HttpRequestMessage>();
        var handler = new RecordingHandler(req =>
        {
            requests.Add(CloneRequest(req));
            if (req.Method == HttpMethod.Get && req.RequestUri!.AbsolutePath.Contains("/databases/"))
                return OkDatabaseSchema();
            if (req.Method == HttpMethod.Post && req.RequestUri!.AbsolutePath.Contains("/query"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"results":[],"has_more":false}""")
                };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"page-created"}""")
            };
        });

        var originalFactory = NotionCommand.CreateHttpClient;
        var originalToken = Environment.GetEnvironmentVariable("BUKIT_TEST_NOTION_TOKEN");
        NotionCommand.CreateHttpClient = () => new HttpClient(handler);
        Environment.SetEnvironmentVariable("BUKIT_TEST_NOTION_TOKEN", "secret_test");
        try
        {
            var result = await NotionCommand.RunAsync(MakeCommand(new Dictionary<string, string?>
            {
                ["--input"] = seedDir,
                ["--database-map"] = mapPath,
                ["--token-env"] = "BUKIT_TEST_NOTION_TOKEN",
                ["--mode"] = "upsert",
                ["--report"] = Path.Combine(_tempDir, "push-report.json")
            }, ["push"]));

            Assert.Equal(0, result);
            var createPayloads = requests
                .Where(r => r.Method == HttpMethod.Post && r.RequestUri!.AbsoluteUri == "https://api.notion.com/v1/pages")
                .Select(r => r.Content!.ReadAsStringAsync().GetAwaiter().GetResult())
                .ToList();
            Assert.Contains(createPayloads, p => p.Contains("\"database_id\": \"pages-db\""));
            Assert.Contains(createPayloads, p => p.Contains("\"database_id\": \"posts-db\""));
        }
        finally
        {
            NotionCommand.CreateHttpClient = originalFactory;
            Environment.SetEnvironmentVariable("BUKIT_TEST_NOTION_TOKEN", originalToken);
        }
    }

    [Fact]
    public async Task Push_UsesDefaultDatabaseMapInInputDirectory()
    {
        var seedDir = Path.Combine(_tempDir, "notion-seed-default-map");
        Directory.CreateDirectory(seedDir);
        File.WriteAllText(Path.Combine(seedDir, "pages.json"), """
[
  { "title": "Home", "slug": "home", "summary": "Welcome", "content": "<p>Hello</p>", "language": "zh", "published": true }
]
""");
        File.WriteAllText(Path.Combine(seedDir, "notion-database-map.yaml"), """
databases:
  pages:
    title: Pages
    databaseId: "pages-db"
    seed: "pages.json"
    collection: page
    uniqueField: Slug
""");

        var requests = new List<HttpRequestMessage>();
        var handler = new RecordingHandler(req =>
        {
            requests.Add(CloneRequest(req));
            if (req.Method == HttpMethod.Get && req.RequestUri!.AbsolutePath.Contains("/databases/"))
                return OkDatabaseSchema();
            if (req.Method == HttpMethod.Post && req.RequestUri!.AbsolutePath.Contains("/query"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"results":[],"has_more":false}""")
                };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"page-created"}""")
            };
        });

        var originalFactory = NotionCommand.CreateHttpClient;
        var originalToken = Environment.GetEnvironmentVariable("BUKIT_TEST_NOTION_TOKEN");
        NotionCommand.CreateHttpClient = () => new HttpClient(handler);
        Environment.SetEnvironmentVariable("BUKIT_TEST_NOTION_TOKEN", "secret_test");
        try
        {
            var result = await NotionCommand.RunAsync(MakeCommand(new Dictionary<string, string?>
            {
                ["--input"] = seedDir,
                ["--token-env"] = "BUKIT_TEST_NOTION_TOKEN",
                ["--mode"] = "upsert",
                ["--report"] = Path.Combine(_tempDir, "push-report.json")
            }, ["push"]));

            Assert.Equal(0, result);
            var createPayloads = requests
                .Where(r => r.Method == HttpMethod.Post && r.RequestUri!.AbsoluteUri == "https://api.notion.com/v1/pages")
                .Select(r => r.Content!.ReadAsStringAsync().GetAwaiter().GetResult())
                .ToList();
            Assert.Contains(createPayloads, p => p.Contains("\"database_id\": \"pages-db\""));
        }
        finally
        {
            NotionCommand.CreateHttpClient = originalFactory;
            Environment.SetEnvironmentVariable("BUKIT_TEST_NOTION_TOKEN", originalToken);
        }
    }

    [Fact]
    public async Task Push_DefaultMultiDatabaseWithoutCreateMissingDatabases_Returns2()
    {
        var seedDir = Path.Combine(_tempDir, "notion-seed");
        Directory.CreateDirectory(seedDir);
        File.WriteAllText(Path.Combine(seedDir, "pages.json"), """
[
  { "title": "Home", "slug": "home", "published": true }
]
""");

        var result = await NotionCommand.RunAsync(MakeCommand(new Dictionary<string, string?>
        {
            ["--input"] = seedDir
        }, ["push"]));

        Assert.Equal(2, result);
    }

    [Fact]
    public async Task Push_CreateMissingDatabasesWithoutMap_CreatesPerCollectionAndWritesGeneratedMap()
    {
        var seedDir = Path.Combine(_tempDir, "notion-seed");
        Directory.CreateDirectory(seedDir);
        File.WriteAllText(Path.Combine(seedDir, "pages.json"), """
[
  { "title": "Home", "slug": "home", "summary": "Welcome", "language": "zh", "published": true }
]
""");
        File.WriteAllText(Path.Combine(seedDir, "posts.json"), """
[
  { "title": "Post", "slug": "post", "summary": "Post summary", "language": "zh", "published": true }
]
""");

        var createdDatabaseIds = new Queue<string>(["created-pages-db", "created-posts-db"]);
        var handler = new RecordingHandler(req =>
        {
            if (req.Method == HttpMethod.Post && req.RequestUri!.AbsoluteUri == "https://api.notion.com/v1/databases")
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($$"""{"id":"{{createdDatabaseIds.Dequeue()}}"}""")
                };
            if (req.Method == HttpMethod.Get && req.RequestUri!.AbsolutePath.Contains("/databases/"))
                return OkDatabaseSchema();
            if (req.Method == HttpMethod.Post && req.RequestUri!.AbsolutePath.Contains("/query"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"results":[],"has_more":false}""")
                };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"page-created"}""")
            };
        });

        var originalFactory = NotionCommand.CreateHttpClient;
        var originalToken = Environment.GetEnvironmentVariable("BUKIT_TEST_NOTION_TOKEN");
        NotionCommand.CreateHttpClient = () => new HttpClient(handler);
        Environment.SetEnvironmentVariable("BUKIT_TEST_NOTION_TOKEN", "secret_test");
        try
        {
            var generatedMapPath = Path.Combine(_tempDir, "generated-map.yaml");
            var result = await NotionCommand.RunAsync(MakeCommand(new Dictionary<string, string?>
            {
                ["--input"] = seedDir,
                ["--create-missing-databases"] = "true",
                ["--parent-page-id"] = "parent-page",
                ["--generated-database-map"] = generatedMapPath,
                ["--token-env"] = "BUKIT_TEST_NOTION_TOKEN",
                ["--mode"] = "upsert"
            }, ["push"]));

            Assert.Equal(0, result);
            var generatedMap = File.ReadAllText(generatedMapPath);
            Assert.Contains("databaseId: created-pages-db", generatedMap);
            Assert.Contains("databaseId: created-posts-db", generatedMap);
        }
        finally
        {
            NotionCommand.CreateHttpClient = originalFactory;
            Environment.SetEnvironmentVariable("BUKIT_TEST_NOTION_TOKEN", originalToken);
        }
    }

    [Fact]
    public async Task Push_CreateMissingDatabaseSchemaValidationFails_BlocksPush()
    {
        var seedDir = Path.Combine(_tempDir, "notion-seed");
        Directory.CreateDirectory(seedDir);
        File.WriteAllText(Path.Combine(seedDir, "pages.json"), """
[
  { "title": "Home", "slug": "home", "summary": "Welcome", "language": "zh", "published": true }
]
""");

        var pageCreateAttempted = false;
        var handler = new RecordingHandler(req =>
        {
            if (req.Method == HttpMethod.Post && req.RequestUri!.AbsoluteUri == "https://api.notion.com/v1/databases")
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"id":"created-pages-db"}""")
                };
            if (req.Method == HttpMethod.Get && req.RequestUri!.AbsolutePath.Contains("/databases/"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"properties":{}}""")
                };
            if (req.Method == HttpMethod.Post && req.RequestUri!.AbsoluteUri == "https://api.notion.com/v1/pages")
                pageCreateAttempted = true;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"page-created"}""")
            };
        });

        var originalFactory = NotionCommand.CreateHttpClient;
        var originalToken = Environment.GetEnvironmentVariable("BUKIT_TEST_NOTION_TOKEN");
        NotionCommand.CreateHttpClient = () => new HttpClient(handler);
        Environment.SetEnvironmentVariable("BUKIT_TEST_NOTION_TOKEN", "secret_test");
        try
        {
            var result = await NotionCommand.RunAsync(MakeCommand(new Dictionary<string, string?>
            {
                ["--input"] = seedDir,
                ["--create-missing-databases"] = "true",
                ["--parent-page-id"] = "parent-page",
                ["--token-env"] = "BUKIT_TEST_NOTION_TOKEN",
                ["--mode"] = "upsert"
            }, ["push"]));

            Assert.Equal(1, result);
            Assert.False(pageCreateAttempted);
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
    public async Task Push_NavigationRecord_WritesLinkAndOrderProperties()
    {
        var seedDir = Path.Combine(_tempDir, "notion-seed");
        Directory.CreateDirectory(seedDir);
        var requests = new List<HttpRequestMessage>();
        var records = new List<ImportSeedRecord>
        {
            new("navigation", "Home", "home", null, null, "zh", true, null, null,
                new Dictionary<string, object?>
                {
                    ["link"] = "/",
                    ["order"] = 10
                })
        };

        var handler = new RecordingHandler(req =>
        {
            requests.Add(CloneRequest(req));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"page-1"}""")
            };
        });

        using var http = new HttpClient(handler);
        var result = await NotionSeedPusher.PushAsync(http, records, new NotionPushOptions(
            DatabaseId: "db-nav",
            Token: "token",
            ReportPath: Path.Combine(seedDir, "notion-push-report.json"),
            DryRun: false));

        Assert.Equal(1, result.Created);
        var payload = await requests.Single(r => r.RequestUri!.AbsoluteUri == "https://api.notion.com/v1/pages")
            .Content!.ReadAsStringAsync();
        Assert.Contains("\"Type\"", payload);
        Assert.Contains("\"name\": \"navigation\"", payload);
        Assert.Contains("\"Link\"", payload);
        Assert.Contains("\"url\": \"/\"", payload);
        Assert.Contains("\"Order\"", payload);
        Assert.Contains("\"number\": 10", payload);
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

    [Fact]
    public async Task Push_ReplaceDeleteFailed_MarksReplaceFailed()
    {
        var seedDir = Path.Combine(_tempDir, "notion-seed");
        Directory.CreateDirectory(seedDir);
        var records = new List<ImportSeedRecord>
        {
            new("page", "Home", "home", null, "<p>Content</p>", "zh", true, null, null)
        };

        var deleteAttempted = false;
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
            if (req.Method == HttpMethod.Get && req.RequestUri.AbsolutePath.Contains("/blocks/"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"results":[{"id":"block-1"}],"has_more":false}""")
                };
            if (req.Method == HttpMethod.Delete && req.RequestUri.AbsolutePath.Contains("/blocks/"))
            {
                deleteAttempted = true;
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
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
            UpdateContent: "replace"));

        Assert.True(deleteAttempted, "Expected DELETE /blocks/{id} for replace mode");
        var report = File.ReadAllText(reportPath);
        Assert.Contains("replace-failed", report);
        Assert.Contains("Failed to delete one or more existing blocks.", report);
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

    private static HttpResponseMessage OkDatabaseSchema()
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent("""
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
""")
        };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
