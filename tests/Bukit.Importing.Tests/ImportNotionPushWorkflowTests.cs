using System.Net;
using System.Text.Json;
using Xunit;

namespace Bukit.Importing.Tests;

[Collection("ImportingConsole")]
public sealed class ImportNotionPushWorkflowTests : IDisposable
{
    private readonly string _rootDir;

    public ImportNotionPushWorkflowTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-importing-notion-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        ImportNotionPushWorkflow.CreateHttpClient = () => new HttpClient();
        if (Directory.Exists(_rootDir))
            Directory.Delete(_rootDir, recursive: true);
    }

    [Fact]
    public async Task PushGeneratedSeedAsync_RejectsDryRun()
    {
        var result = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportNotionPushWorkflow.PushGeneratedSeedAsync(new ImportGeneratedNotionPushOptions
            {
                ImportResult = BuildImportResult(),
                RootDir = _rootDir,
                ThemeName = "demo",
                DryRun = true
            }));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("--push-notion 不能与 --dry-run 同时使用。先生成草稿后再执行实际推送。", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PushGeneratedSeedAsync_DefaultNotionMapWithMissingDatabaseIdReturnsTwoBeforeTokenCheck()
    {
        var siteDir = Path.Combine(_rootDir, "sites", "demo");
        var seedDir = Path.Combine(siteDir, "notion-seed");
        Directory.CreateDirectory(seedDir);
        File.WriteAllText(Path.Combine(seedDir, "posts.json"), """
[
  { "title": "Hello", "slug": "hello" }
]
""");
        File.WriteAllText(Path.Combine(seedDir, "notion-database-map.yaml"), """
databases:
  posts:
    title: Posts
    seed: posts.json
    collection: post
    databaseId:
    uniqueField: Slug
""");

        using var token = new ImportingCommandTestSupport.EnvironmentVariableScope("BUKIT_IMPORT_TEST_NOTION_TOKEN", null);
        var result = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportNotionPushWorkflow.PushGeneratedSeedAsync(new ImportGeneratedNotionPushOptions
            {
                ImportResult = BuildImportResult(),
                RootDir = _rootDir,
                ThemeName = "demo",
                TokenEnv = "BUKIT_IMPORT_TEST_NOTION_TOKEN"
            }));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Notion database map exists but one or more databaseId values are empty.", result.StdErr, StringComparison.Ordinal);
        Assert.DoesNotContain("BUKIT_IMPORT_TEST_NOTION_TOKEN is required", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PushGeneratedSeedAsync_MissingTokenReturnsTwoForActualPush()
    {
        var siteDir = Path.Combine(_rootDir, "sites", "demo");
        var seedDir = Path.Combine(siteDir, "notion-seed");
        Directory.CreateDirectory(seedDir);
        File.WriteAllText(Path.Combine(seedDir, "posts.json"), """
[
  { "title": "Hello", "slug": "hello" }
]
""");

        using var token = new ImportingCommandTestSupport.EnvironmentVariableScope("BUKIT_IMPORT_TEST_NOTION_TOKEN", null);
        var result = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportNotionPushWorkflow.PushGeneratedSeedAsync(new ImportGeneratedNotionPushOptions
            {
                ImportResult = BuildImportResult(),
                RootDir = _rootDir,
                ThemeName = "demo",
                DatabaseId = "db-single",
                TokenEnv = "BUKIT_IMPORT_TEST_NOTION_TOKEN"
            }));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("BUKIT_IMPORT_TEST_NOTION_TOKEN is required for notion push.", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PushSeedDirectoryAsync_DryRunWithCreateMissingDatabasesWritesGeneratedMapAndReport()
    {
        var inputDir = Path.Combine(_rootDir, "seed");
        Directory.CreateDirectory(inputDir);
        File.WriteAllText(Path.Combine(inputDir, "posts.json"), """
[
  { "title": "Hello", "slug": "hello" }
]
""");
        File.WriteAllText(Path.Combine(inputDir, "navigation.yaml"), """
- name: Main nav
  slug: main-nav
""");
        var reportPath = Path.Combine(_rootDir, "multi-report.json");
        var mapPath = Path.Combine(_rootDir, "generated-map.yaml");

        var result = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportNotionPushWorkflow.PushSeedDirectoryAsync(new ImportNotionSeedPushOptions
            {
                InputDir = inputDir,
                DryRun = true,
                CreateMissingDatabases = true,
                ParentPageId = "parent-page",
                GeneratedDatabaseMapPath = mapPath,
                ReportPath = reportPath
            }));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("notion push dry-run 完成: databases=2 records=2", result.StdOut, StringComparison.Ordinal);
        Assert.True(File.Exists(reportPath));
        Assert.True(File.Exists(mapPath));

        var generatedMap = File.ReadAllText(mapPath);
        Assert.Contains("posts:", generatedMap, StringComparison.Ordinal);
        Assert.Contains("navigation:", generatedMap, StringComparison.Ordinal);
        Assert.Contains("uniqueField: Slug", generatedMap, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PushSeedDirectoryAsync_SingleDatabaseDryRunWritesReport()
    {
        var inputDir = Path.Combine(_rootDir, "single-seed");
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

        var result = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportNotionPushWorkflow.PushSeedDirectoryAsync(new ImportNotionSeedPushOptions
            {
                InputDir = inputDir,
                DatabaseId = "db-single",
                DryRun = true,
                ReportPath = reportPath
            }));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("notion push dry-run 完成: records=1", result.StdOut, StringComparison.Ordinal);
        Assert.True(File.Exists(reportPath));
        Assert.Contains("\"dryRun\": true", File.ReadAllText(reportPath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PushGeneratedSeedAsync_ResolvesRelativeReportAndGeneratedMapAgainstSiteDirectory()
    {
        var siteDir = Path.Combine(_rootDir, "sites", "demo");
        var seedDir = Path.Combine(siteDir, "notion-seed");
        Directory.CreateDirectory(seedDir);
        File.WriteAllText(Path.Combine(seedDir, "posts.json"), """
[
  { "title": "Hello", "slug": "hello", "content": "<p>Hello</p>" }
]
""");

        ImportNotionPushWorkflow.CreateHttpClient = () => new HttpClient(new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? "";
            if (request.Method == HttpMethod.Post && path.EndsWith("/databases", StringComparison.Ordinal))
            {
                return Json("""{ "id": "db-created" }""");
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/query", StringComparison.Ordinal))
            {
                return Json("""{ "results": [] }""");
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/pages", StringComparison.Ordinal))
            {
                return Json("""{ "id": "page-created" }""");
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent(path)
            };
        }));

        using var token = new ImportingCommandTestSupport.EnvironmentVariableScope("BUKIT_IMPORT_TEST_NOTION_TOKEN", "secret");
        var result = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportNotionPushWorkflow.PushGeneratedSeedAsync(new ImportGeneratedNotionPushOptions
            {
                ImportResult = BuildImportResult(),
                RootDir = _rootDir,
                ThemeName = "demo",
                CreateMissingDatabases = true,
                ParentPageId = "parent-page",
                GeneratedDatabaseMap = "generated-map.yaml",
                ReportPath = "reports/notion-report.json",
                TokenEnv = "BUKIT_IMPORT_TEST_NOTION_TOKEN",
                ValidateSchema = false
            }));

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(siteDir, "generated-map.yaml")));
        Assert.True(File.Exists(Path.Combine(siteDir, "reports", "notion-report.json")));
        Assert.Contains("databaseId: db-created", File.ReadAllText(Path.Combine(siteDir, "generated-map.yaml")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PushGeneratedSeedAsync_CreateMissingDatabasesAddsExtraSeedFieldsToSchema()
    {
        var siteDir = Path.Combine(_rootDir, "sites", "demo");
        var seedDir = Path.Combine(siteDir, "notion-seed");
        Directory.CreateDirectory(seedDir);
        File.WriteAllText(Path.Combine(seedDir, "posts.json"), """
[
  {
    "title": "Hello",
    "slug": "hello",
    "content": "<p>Hello</p>",
    "category": "Market",
    "tags_text": "one, two",
    "featured": true,
    "priority": 7,
    "url": "https://example.com"
  }
]
""");

        var createDatabasePayloads = new List<string>();
        ImportNotionPushWorkflow.CreateHttpClient = () => new HttpClient(new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? "";
            if (request.Method == HttpMethod.Post && path.EndsWith("/databases", StringComparison.Ordinal))
            {
                createDatabasePayloads.Add(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
                return Json("""{ "id": "db-created" }""");
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/query", StringComparison.Ordinal))
            {
                return Json("""{ "results": [] }""");
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/pages", StringComparison.Ordinal))
            {
                return Json("""{ "id": "page-created" }""");
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent(path)
            };
        }));

        using var token = new ImportingCommandTestSupport.EnvironmentVariableScope("BUKIT_IMPORT_TEST_NOTION_TOKEN", "secret");
        var result = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportNotionPushWorkflow.PushGeneratedSeedAsync(new ImportGeneratedNotionPushOptions
            {
                ImportResult = BuildImportResult(),
                RootDir = _rootDir,
                ThemeName = "demo",
                CreateMissingDatabases = true,
                ParentPageId = "parent-page",
                GeneratedDatabaseMap = "generated-map.yaml",
                ReportPath = "reports/notion-report.json",
                TokenEnv = "BUKIT_IMPORT_TEST_NOTION_TOKEN",
                ValidateSchema = false
            }));

        Assert.Equal(0, result.ExitCode);
        var payload = Assert.Single(createDatabasePayloads);
        AssertPropertyType(payload, "Category", "rich_text");
        AssertPropertyType(payload, "TagsText", "rich_text");
        AssertPropertyType(payload, "Featured", "checkbox");
        AssertPropertyType(payload, "Priority", "number");
        AssertPropertyType(payload, "Url", "url");
    }

    [Fact]
    public async Task PushSeedDirectoryAsync_TypedSchemaCreatesAndWritesMatchingNotionTypes()
    {
        var inputDir = Path.Combine(_rootDir, "typed-seed");
        Directory.CreateDirectory(inputDir);
        File.WriteAllText(Path.Combine(inputDir, "posts.json"), """
[
  {
    "title": "Typed",
    "slug": "typed",
    "category": "Market",
    "tags": ["china", "market"],
    "website": "https://example.com",
    "publish_at": "2026-07-11",
    "priority": 3,
    "featured": true
  }
]
""");
        var mapPath = Path.Combine(inputDir, "notion-database-map.yaml");
        File.WriteAllText(mapPath, """
databases:
  posts:
    title: Posts
    seed: posts.json
    collection: post
    databaseId:
    uniqueField: Slug
    schema:
      Category: select
      Tags: multi_select
      Website: url
      PublishAt: date
      Priority: number
      Featured: checkbox
""");

        string? databasePayload = null;
        string? pagePayload = null;
        ImportNotionPushWorkflow.CreateHttpClient = () => new HttpClient(new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? "";
            if (request.Method == HttpMethod.Post && path.EndsWith("/databases", StringComparison.Ordinal))
            {
                databasePayload = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return Json("""{ "id": "db-created" }""");
            }
            if (request.Method == HttpMethod.Post && path.EndsWith("/pages", StringComparison.Ordinal))
            {
                pagePayload = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return Json("""{ "id": "page-created" }""");
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }));

        using var token = new ImportingCommandTestSupport.EnvironmentVariableScope("BUKIT_IMPORT_TEST_NOTION_TOKEN", "secret");
        var result = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportNotionPushWorkflow.PushSeedDirectoryAsync(new ImportNotionSeedPushOptions
            {
                InputDir = inputDir,
                DatabaseMapPath = mapPath,
                CreateMissingDatabases = true,
                ParentPageId = "parent-page",
                TokenEnv = "BUKIT_IMPORT_TEST_NOTION_TOKEN",
                ValidateSchema = false,
                ReportPath = Path.Combine(_rootDir, "typed-report.json")
            }));

        Assert.Equal(0, result.ExitCode);
        Assert.NotNull(databasePayload);
        Assert.NotNull(pagePayload);
        foreach (var (field, type) in new[]
        {
            ("Category", "select"), ("Tags", "multi_select"), ("Website", "url"),
            ("PublishAt", "date"), ("Priority", "number"), ("Featured", "checkbox")
        })
        {
            AssertPropertyType(databasePayload!, field, type);
            AssertPropertyType(pagePayload!, field, type);
        }
    }

    [Theory]
    [InlineData("Unknown: relation", "Unsupported Notion schema type 'relation'")]
    [InlineData("Category: [select]", "Schema field 'Category' must declare a scalar type")]
    public async Task PushSeedDirectoryAsync_InvalidTypedSchemaFailsBeforeApiCall(string schemaLine, string expectedError)
    {
        var inputDir = Path.Combine(_rootDir, "invalid-schema-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(inputDir);
        File.WriteAllText(Path.Combine(inputDir, "posts.json"), """[{ "title": "Post", "slug": "post" }]""");
        var mapPath = Path.Combine(inputDir, "map.yaml");
        File.WriteAllText(mapPath, $"""
databases:
  posts:
    seed: posts.json
    collection: post
    databaseId: db-posts
    schema:
      {schemaLine}
""");
        var requestCount = 0;
        ImportNotionPushWorkflow.CreateHttpClient = () => new HttpClient(new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            return Json("{}");
        }));

        using var token = new ImportingCommandTestSupport.EnvironmentVariableScope("BUKIT_IMPORT_TEST_NOTION_TOKEN", "secret");
        var result = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportNotionPushWorkflow.PushSeedDirectoryAsync(new ImportNotionSeedPushOptions
            {
                InputDir = inputDir,
                DatabaseMapPath = mapPath,
                TokenEnv = "BUKIT_IMPORT_TEST_NOTION_TOKEN",
                ValidateSchema = false,
                ReportPath = Path.Combine(_rootDir, "invalid-report.json")
            }));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains(expectedError, result.StdErr, StringComparison.Ordinal);
        Assert.Equal(0, requestCount);
    }

    [Fact]
    public async Task PushSeedDirectoryAsync_DuplicateTypedSchemaFieldFailsBeforeApiCall()
    {
        var inputDir = Path.Combine(_rootDir, "duplicate-schema");
        Directory.CreateDirectory(inputDir);
        File.WriteAllText(Path.Combine(inputDir, "posts.json"), """[{ "title": "Post", "slug": "post" }]""");
        var mapPath = Path.Combine(inputDir, "map.yaml");
        File.WriteAllText(mapPath, """
databases:
  posts:
    seed: posts.json
    collection: post
    databaseId: db-posts
    schema:
      Category: select
      Category: rich_text
""");
        var requestCount = 0;
        ImportNotionPushWorkflow.CreateHttpClient = () => new HttpClient(new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            return Json("{}");
        }));

        using var token = new ImportingCommandTestSupport.EnvironmentVariableScope("BUKIT_IMPORT_TEST_NOTION_TOKEN", "secret");
        var result = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportNotionPushWorkflow.PushSeedDirectoryAsync(new ImportNotionSeedPushOptions
            {
                InputDir = inputDir,
                DatabaseMapPath = mapPath,
                TokenEnv = "BUKIT_IMPORT_TEST_NOTION_TOKEN",
                ValidateSchema = false,
                ReportPath = Path.Combine(_rootDir, "duplicate-report.json")
            }));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains(mapPath, result.StdErr, StringComparison.Ordinal);
        Assert.Contains("Duplicate schema field 'Category'", result.StdErr, StringComparison.Ordinal);
        Assert.Equal(0, requestCount);
    }

    [Theory]
    [InlineData("publish_at: date", "Schema key 'publish_at' must use canonical Notion property name 'PublishAt'")]
    [InlineData("Title: rich_text", "Schema key 'Title' conflicts with fixed core property 'Title'")]
    [InlineData("'---': rich_text", "Schema key '---' has an empty canonical Notion property name")]
    public async Task PushSeedDirectoryAsync_InvalidSchemaKeyFailsWithMapPathBeforeApiCall(
        string schemaLine,
        string expectedError)
    {
        var inputDir = Path.Combine(_rootDir, "invalid-key-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(inputDir);
        File.WriteAllText(Path.Combine(inputDir, "posts.json"), """[{ "title": "Post", "slug": "post" }]""");
        var mapPath = Path.Combine(inputDir, "map.yaml");
        File.WriteAllText(mapPath, $"""
databases:
  posts:
    seed: posts.json
    collection: post
    databaseId: db-posts
    schema:
      {schemaLine}
""");
        var requestCount = 0;
        ImportNotionPushWorkflow.CreateHttpClient = () => new HttpClient(new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            return Json("{}");
        }));

        using var token = new ImportingCommandTestSupport.EnvironmentVariableScope("BUKIT_IMPORT_TEST_NOTION_TOKEN", "secret");
        var result = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportNotionPushWorkflow.PushSeedDirectoryAsync(new ImportNotionSeedPushOptions
            {
                InputDir = inputDir,
                DatabaseMapPath = mapPath,
                TokenEnv = "BUKIT_IMPORT_TEST_NOTION_TOKEN",
                ValidateSchema = false,
                ReportPath = Path.Combine(_rootDir, "invalid-key-report.json")
            }));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains(mapPath, result.StdErr, StringComparison.Ordinal);
        Assert.Contains("databases.posts.schema", result.StdErr, StringComparison.Ordinal);
        Assert.Contains(expectedError, result.StdErr, StringComparison.Ordinal);
        Assert.Equal(0, requestCount);
    }

    [Fact]
    public async Task PushSeedDirectoryAsync_NormalizedDuplicateSchemaKeysFailBeforeApiCall()
    {
        var inputDir = Path.Combine(_rootDir, "normalized-duplicate");
        Directory.CreateDirectory(inputDir);
        File.WriteAllText(Path.Combine(inputDir, "posts.json"), """[{ "title": "Post", "slug": "post" }]""");
        var mapPath = Path.Combine(inputDir, "map.yaml");
        File.WriteAllText(mapPath, """
databases:
  posts:
    seed: posts.json
    collection: post
    databaseId: db-posts
    schema:
      publish_at: date
      PublishAt: date
""");
        var requestCount = 0;
        ImportNotionPushWorkflow.CreateHttpClient = () => new HttpClient(new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            return Json("{}");
        }));

        using var token = new ImportingCommandTestSupport.EnvironmentVariableScope("BUKIT_IMPORT_TEST_NOTION_TOKEN", "secret");
        var result = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportNotionPushWorkflow.PushSeedDirectoryAsync(new ImportNotionSeedPushOptions
            {
                InputDir = inputDir,
                DatabaseMapPath = mapPath,
                TokenEnv = "BUKIT_IMPORT_TEST_NOTION_TOKEN",
                ValidateSchema = false,
                ReportPath = Path.Combine(_rootDir, "normalized-duplicate-report.json")
            }));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("normalize to duplicate Notion property 'PublishAt'", result.StdErr, StringComparison.Ordinal);
        Assert.Equal(0, requestCount);
    }

    [Theory]
    [InlineData("date", "07/11/2026")]
    [InlineData("url", "ftp://example.com/file")]
    [InlineData("number", "3")]
    [InlineData("checkbox", "true")]
    [InlineData("multi_select", "market")]
    public async Task PushSeedDirectoryAsync_TypedValueKindMismatchFailsBeforeApiCall(string type, string jsonValue)
    {
        var inputDir = Path.Combine(_rootDir, "mismatch-" + type);
        Directory.CreateDirectory(inputDir);
        File.WriteAllText(Path.Combine(inputDir, "posts.json"), $$"""[{ "title": "Post", "slug": "post", "value": "{{jsonValue}}" }]""");
        var mapPath = Path.Combine(inputDir, "map.yaml");
        File.WriteAllText(mapPath, $$"""
databases:
  posts:
    seed: posts.json
    collection: post
    databaseId: db-posts
    schema:
      Value: {{type}}
""");
        var requestCount = 0;
        ImportNotionPushWorkflow.CreateHttpClient = () => new HttpClient(new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            return Json("{}");
        }));

        using var token = new ImportingCommandTestSupport.EnvironmentVariableScope("BUKIT_IMPORT_TEST_NOTION_TOKEN", "secret");
        var result = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportNotionPushWorkflow.PushSeedDirectoryAsync(new ImportNotionSeedPushOptions
            {
                InputDir = inputDir,
                DatabaseMapPath = mapPath,
                TokenEnv = "BUKIT_IMPORT_TEST_NOTION_TOKEN",
                ValidateSchema = false,
                ReportPath = Path.Combine(_rootDir, "mismatch-report.json")
            }));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains($"field 'Value', record 'post': expected {type}", result.StdErr, StringComparison.Ordinal);
        Assert.Equal(0, requestCount);
    }

    [Fact]
    public async Task PushSeedDirectoryAsync_IncompatibleTypedValueReportsDatabaseFieldAndRecordBeforeApiCall()
    {
        var inputDir = Path.Combine(_rootDir, "invalid-value");
        Directory.CreateDirectory(inputDir);
        File.WriteAllText(Path.Combine(inputDir, "posts.json"), """[{ "title": "Post", "slug": "post", "category": ["Market"] }]""");
        var mapPath = Path.Combine(inputDir, "map.yaml");
        File.WriteAllText(mapPath, """
databases:
  posts:
    seed: posts.json
    collection: post
    databaseId: db-posts
    schema:
      Category: select
""");
        var requestCount = 0;
        ImportNotionPushWorkflow.CreateHttpClient = () => new HttpClient(new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            return Json("{}");
        }));

        using var token = new ImportingCommandTestSupport.EnvironmentVariableScope("BUKIT_IMPORT_TEST_NOTION_TOKEN", "secret");
        var result = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportNotionPushWorkflow.PushSeedDirectoryAsync(new ImportNotionSeedPushOptions
            {
                InputDir = inputDir,
                DatabaseMapPath = mapPath,
                TokenEnv = "BUKIT_IMPORT_TEST_NOTION_TOKEN",
                ValidateSchema = false,
                ReportPath = Path.Combine(_rootDir, "invalid-value-report.json")
            }));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("database 'posts'", result.StdErr, StringComparison.Ordinal);
        Assert.Contains("field 'Category'", result.StdErr, StringComparison.Ordinal);
        Assert.Contains("record 'post'", result.StdErr, StringComparison.Ordinal);
        Assert.Equal(0, requestCount);
    }

    [Fact]
    public async Task PushSeedDirectoryAsync_WithoutSchemaPreservesLegacyPagePropertyInference()
    {
        var inputDir = Path.Combine(_rootDir, "legacy-inference");
        Directory.CreateDirectory(inputDir);
        File.WriteAllText(Path.Combine(inputDir, "posts.json"), """
[
  {
    "title": "Legacy",
    "slug": "legacy",
    "featured": true,
    "priority": 7,
    "url": "https://example.com",
    "category": "Market"
  }
]
""");
        var mapPath = Path.Combine(inputDir, "map.yaml");
        File.WriteAllText(mapPath, """
databases:
  posts:
    seed: posts.json
    collection: post
    databaseId: db-posts
""");
        string? pagePayload = null;
        ImportNotionPushWorkflow.CreateHttpClient = () => new HttpClient(new StubHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath.EndsWith("/pages", StringComparison.Ordinal) == true)
            {
                pagePayload = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return Json("""{ "id": "page-created" }""");
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }));

        using var token = new ImportingCommandTestSupport.EnvironmentVariableScope("BUKIT_IMPORT_TEST_NOTION_TOKEN", "secret");
        var result = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportNotionPushWorkflow.PushSeedDirectoryAsync(new ImportNotionSeedPushOptions
            {
                InputDir = inputDir,
                DatabaseMapPath = mapPath,
                TokenEnv = "BUKIT_IMPORT_TEST_NOTION_TOKEN",
                ValidateSchema = false,
                ReportPath = Path.Combine(_rootDir, "legacy-report.json")
            }));

        Assert.Equal(0, result.ExitCode);
        Assert.NotNull(pagePayload);
        AssertPropertyType(pagePayload!, "Featured", "checkbox");
        AssertPropertyType(pagePayload!, "Priority", "number");
        AssertPropertyType(pagePayload!, "Url", "url");
        AssertPropertyType(pagePayload!, "Category", "rich_text");
    }

    [Fact]
    public async Task PushSeedDirectoryAsync_TypedYamlScalarSequenceWritesMultiSelect()
    {
        var inputDir = Path.Combine(_rootDir, "yaml-multi-select");
        Directory.CreateDirectory(inputDir);
        File.WriteAllText(Path.Combine(inputDir, "posts.yaml"), """
- title: YAML Tags
  slug: yaml-tags
  tags:
    - china
    - market
""");
        var mapPath = Path.Combine(inputDir, "map.yaml");
        File.WriteAllText(mapPath, """
databases:
  posts:
    seed: posts.yaml
    collection: post
    databaseId: db-posts
    schema:
      Tags: multi_select
""");
        string? pagePayload = null;
        ImportNotionPushWorkflow.CreateHttpClient = () => new HttpClient(new StubHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath.EndsWith("/pages", StringComparison.Ordinal) == true)
            {
                pagePayload = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return Json("""{ "id": "page-created" }""");
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }));

        using var token = new ImportingCommandTestSupport.EnvironmentVariableScope("BUKIT_IMPORT_TEST_NOTION_TOKEN", "secret");
        var result = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportNotionPushWorkflow.PushSeedDirectoryAsync(new ImportNotionSeedPushOptions
            {
                InputDir = inputDir,
                DatabaseMapPath = mapPath,
                TokenEnv = "BUKIT_IMPORT_TEST_NOTION_TOKEN",
                ValidateSchema = false,
                ReportPath = Path.Combine(_rootDir, "yaml-tags-report.json")
            }));

        Assert.Equal(0, result.ExitCode);
        using var payload = JsonDocument.Parse(pagePayload!);
        var names = payload.RootElement.GetProperty("properties").GetProperty("Tags")
            .GetProperty("multi_select").EnumerateArray()
            .Select(item => item.GetProperty("name").GetString()!).ToArray();
        Assert.Equal(["china", "market"], names);
    }

    [Fact]
    public async Task ValidateSchemaAsync_MissingDatabaseIdReturnsTwo()
    {
        using var token = new ImportingCommandTestSupport.EnvironmentVariableScope("BUKIT_IMPORT_TEST_NOTION_TOKEN", "secret");

        var result = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportNotionPushWorkflow.ValidateSchemaAsync(new ImportNotionSchemaValidationOptions
            {
                TokenEnv = "BUKIT_IMPORT_TEST_NOTION_TOKEN"
            }));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("缺少必填选项: --database-id <id>", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateSchemaAsync_MissingTokenReturnsTwo()
    {
        using var token = new ImportingCommandTestSupport.EnvironmentVariableScope("BUKIT_IMPORT_TEST_NOTION_TOKEN", null);

        var result = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportNotionPushWorkflow.ValidateSchemaAsync(new ImportNotionSchemaValidationOptions
            {
                DatabaseId = "db-schema",
                TokenEnv = "BUKIT_IMPORT_TEST_NOTION_TOKEN"
            }));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("BUKIT_IMPORT_TEST_NOTION_TOKEN is required for notion validate-schema.", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateSchemaAsync_WithStubbedHttpClientWritesPassingReport()
    {
        ImportNotionPushWorkflow.CreateHttpClient = () => new HttpClient(new StubHttpMessageHandler(_ =>
            Json("""
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
""")));

        using var token = new ImportingCommandTestSupport.EnvironmentVariableScope("BUKIT_IMPORT_TEST_NOTION_TOKEN", "secret");
        var reportPath = Path.Combine(_rootDir, "schema-report.json");

        var result = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportNotionPushWorkflow.ValidateSchemaAsync(new ImportNotionSchemaValidationOptions
            {
                DatabaseId = "db-schema",
                TokenEnv = "BUKIT_IMPORT_TEST_NOTION_TOKEN",
                ReportPath = reportPath
            }));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("schema validation: PASSED", result.StdOut, StringComparison.Ordinal);
        Assert.True(File.Exists(reportPath));
        Assert.Contains("\"success\": true", File.ReadAllText(reportPath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateSchemaAsync_FieldMissingReturnsOneAndWritesReport()
    {
        ImportNotionPushWorkflow.CreateHttpClient = () => new HttpClient(new StubHttpMessageHandler(_ =>
            Json("""
{
  "properties": {
    "Title": { "type": "title" }
  }
}
""")));

        using var token = new ImportingCommandTestSupport.EnvironmentVariableScope("BUKIT_IMPORT_TEST_NOTION_TOKEN", "secret");
        var reportPath = Path.Combine(_rootDir, "schema-missing-report.json");

        var result = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportNotionPushWorkflow.ValidateSchemaAsync(new ImportNotionSchemaValidationOptions
            {
                DatabaseId = "db-schema",
                TokenEnv = "BUKIT_IMPORT_TEST_NOTION_TOKEN",
                ReportPath = reportPath
            }));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("schema validation: FAILED", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("Field 'Slug' (type: rich_text) is missing", result.StdErr, StringComparison.Ordinal);
        Assert.True(File.Exists(reportPath));
        Assert.Contains("\"success\": false", File.ReadAllText(reportPath), StringComparison.Ordinal);
    }

    private ImportResult BuildImportResult()
        => new()
        {
            ThemePath = Path.Combine(_rootDir, "themes", "demo")
        };

    private static void AssertPropertyType(string payload, string propertyName, string expectedType)
    {
        using var doc = JsonDocument.Parse(payload);
        var property = doc.RootElement
            .GetProperty("properties")
            .GetProperty(propertyName);
        Assert.True(
            property.TryGetProperty(expectedType, out _),
            $"{propertyName} should be created as Notion property type {expectedType}.");
    }

    private static HttpResponseMessage Json(string body)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body)
        };

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
