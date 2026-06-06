using Bukit.Cli.Cli.Binding;
using Bukit.Cli.Commands;
using System.Net;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class ImportCommandTests : IDisposable
{
    private readonly string _tempDir;

    public ImportCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-import-cmd-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_tempDir, recursive: true);
    }

    private static CliBoundCommand MakeCommand(Dictionary<string, string?> options, string[] args)
    {
        return new CliBoundCommand(options, args);
    }

    private Dictionary<string, string?> BaseOptions()
    {
        var configPath = Path.Combine(_tempDir, "site.yaml");
        return new Dictionary<string, string?>
        {
            ["--config"] = configPath
        };
    }

    private void CreateDemoHtml(string fileName, string title)
    {
        File.WriteAllText(Path.Combine(_tempDir, fileName),
            $"<html><head><title>{title}</title></head><body><header><nav>Nav</nav></header><main><h1>{title}</h1></main><footer>Footer</footer></body></html>");
    }

    [Fact]
    public async Task NoSubcommand_Returns2()
    {
        var cmd = MakeCommand(new Dictionary<string, string?>(), []);
        var result = await ImportCommand.RunAsync(cmd);
        Assert.Equal(2, result);
    }

    [Fact]
    public async Task MissingDemoDir_Returns2()
    {
        var opts = BaseOptions();
        opts["--theme"] = "test";
        var cmd = MakeCommand(opts, ["html-demo"]);

        var result = await ImportCommand.RunAsync(cmd);
        Assert.Equal(2, result);
    }

    [Fact]
    public async Task DemoDirNotFound_Returns2()
    {
        var opts = BaseOptions();
        opts["--theme"] = "test";
        var cmd = MakeCommand(opts, ["html-demo", Path.Combine(_tempDir, "nonexistent")]);

        var result = await ImportCommand.RunAsync(cmd);
        Assert.Equal(2, result);
    }

    [Fact]
    public async Task MissingTheme_Returns2()
    {
        CreateDemoHtml("index.html", "Test");

        var opts = BaseOptions();
        var cmd = MakeCommand(opts, ["html-demo", _tempDir]);

        var result = await ImportCommand.RunAsync(cmd);
        Assert.Equal(2, result);
    }

    [Theory]
    [InlineData("../evil")]
    [InlineData("/root")]
    [InlineData("")]
    public async Task InvalidThemeName_Returns2(string themeName)
    {
        CreateDemoHtml("index.html", "Test");

        var opts = BaseOptions();
        opts["--theme"] = themeName;
        var cmd = MakeCommand(opts, ["html-demo", _tempDir]);

        var result = await ImportCommand.RunAsync(cmd);
        Assert.Equal(2, result);
    }

    [Fact]
    public async Task ExistingTheme_WithoutForce_Returns2()
    {
        CreateDemoHtml("index.html", "Test");
        var themeDir = Path.Combine(_tempDir, "themes", "existing");
        Directory.CreateDirectory(themeDir);

        var opts = BaseOptions();
        opts["--theme"] = "existing";
        var cmd = MakeCommand(opts, ["html-demo", _tempDir]);

        var result = await ImportCommand.RunAsync(cmd);
        Assert.Equal(2, result);
    }

    [Fact]
    public async Task ExistingTheme_WithForce_Overwrites()
    {
        CreateDemoHtml("index.html", "Test");
        var themeDir = Path.Combine(_tempDir, "themes", "force-test");
        Directory.CreateDirectory(themeDir);
        File.WriteAllText(Path.Combine(themeDir, "old.txt"), "old");

        var opts = BaseOptions();
        opts["--theme"] = "force-test";
        opts["--force"] = "true";
        var cmd = MakeCommand(opts, ["html-demo", _tempDir]);

        var result = await ImportCommand.RunAsync(cmd);

        Assert.Equal(0, result);
        Assert.False(File.Exists(Path.Combine(themeDir, "old.txt")));
    }

    [Fact]
    public async Task SingleHtmlFile_GeneratesCompleteStructure()
    {
        CreateDemoHtml("index.html", "Test Site");

        var opts = BaseOptions();
        opts["--theme"] = "single-test";
        var cmd = MakeCommand(opts, ["html-demo", _tempDir]);

        var result = await ImportCommand.RunAsync(cmd);

        Assert.Equal(0, result);
        var themeDir = Path.Combine(_tempDir, "themes", "single-test");
        Assert.True(File.Exists(Path.Combine(themeDir, "layouts", "layouts", "base.html")));
        Assert.True(File.Exists(Path.Combine(themeDir, "layouts", "pages", "index.html")));
        Assert.True(File.Exists(Path.Combine(themeDir, "layouts", "partials", "header.html")));
        Assert.True(File.Exists(Path.Combine(themeDir, "layouts", "bukit.templates.yaml")));
    }

    [Fact]
    public async Task Import_TemplateSyncStatusMatchesActualManifest()
    {
        CreateDemoHtml("index.html", "Test Site");

        var opts = BaseOptions();
        opts["--theme"] = "sync-status-test";

        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            var result = await ImportCommand.RunAsync(MakeCommand(opts, ["html-demo", _tempDir]));
            Assert.Equal(0, result);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = writer.ToString();
        Assert.True(File.Exists(Path.Combine(_tempDir, "themes", "sync-status-test", "layouts", "bukit.templates.yaml")));
        Assert.Contains("bukit.templates.yaml: 已创建", output);
        Assert.DoesNotContain("bukit.templates.yaml: 已跳过", output);
        Assert.Equal(1, CountOccurrences(output, "bukit.templates.yaml: 已创建"));
    }

    [Fact]
    public async Task Import_WithPushNotion_PostsGeneratedSeed()
    {
        CreateDemoHtml("index.html", "Home");

        var requests = new List<HttpRequestMessage>();
        var handler = new RecordingHandler(req =>
        {
            requests.Add(CloneRequest(req));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"page-1"}""")
            };
        });

        var opts = BaseOptions();
        opts["--theme"] = "push-notion-test";
        opts["--push-notion"] = "true";
        opts["--notion-database-id"] = "db123";
        opts["--notion-token-env"] = "BUKIT_TEST_IMPORT_NOTION_TOKEN";
        opts["--no-validate-notion-schema"] = "true";

        var originalFactory = NotionCommand.CreateHttpClient;
        var originalToken = Environment.GetEnvironmentVariable("BUKIT_TEST_IMPORT_NOTION_TOKEN");
        NotionCommand.CreateHttpClient = () => new HttpClient(handler);
        Environment.SetEnvironmentVariable("BUKIT_TEST_IMPORT_NOTION_TOKEN", "secret_import");
        try
        {
            var result = await ImportCommand.RunAsync(MakeCommand(opts, ["html-demo", _tempDir]));

            Assert.Equal(0, result);
            Assert.Single(requests);
            Assert.Equal("Bearer secret_import", requests[0].Headers.Authorization!.ToString());
            Assert.True(File.Exists(Path.Combine(_tempDir, "sites", "push-notion-test", "notion-seed", "notion-push-report.json")));
        }
        finally
        {
            NotionCommand.CreateHttpClient = originalFactory;
            Environment.SetEnvironmentVariable("BUKIT_TEST_IMPORT_NOTION_TOKEN", originalToken);
        }
    }

    [Fact]
    public async Task Import_WithPushNotionDatabaseMap_DoesNotRequireSingleDatabaseId()
    {
        CreateDemoHtml("index.html", "Home");
        var mapPath = Path.Combine(_tempDir, "notion-map.yaml");
        File.WriteAllText(mapPath, """
databases:
  pages:
    databaseId: "pages-db"
    seed: "pages.json"
""");

        var requests = new List<HttpRequestMessage>();
        var handler = new RecordingHandler(req =>
        {
            requests.Add(CloneRequest(req));
            if (req.RequestUri!.AbsolutePath.Contains("/databases/") && req.Method == HttpMethod.Get)
                return OkDatabaseSchema();
            if (req.RequestUri.AbsolutePath.Contains("/query"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"results":[],"has_more":false}""")
                };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"page-1"}""")
            };
        });

        var opts = BaseOptions();
        opts["--theme"] = "push-notion-map-test";
        opts["--push-notion"] = "true";
        opts["--notion-database-map"] = mapPath;
        opts["--notion-token-env"] = "BUKIT_TEST_IMPORT_NOTION_MAP_TOKEN";

        var originalFactory = NotionCommand.CreateHttpClient;
        var originalToken = Environment.GetEnvironmentVariable("BUKIT_TEST_IMPORT_NOTION_MAP_TOKEN");
        NotionCommand.CreateHttpClient = () => new HttpClient(handler);
        Environment.SetEnvironmentVariable("BUKIT_TEST_IMPORT_NOTION_MAP_TOKEN", "secret_import_map");
        try
        {
            var result = await ImportCommand.RunAsync(MakeCommand(opts, ["html-demo", _tempDir]));

            Assert.Equal(0, result);
            Assert.Contains(requests, r => r.RequestUri!.AbsoluteUri == "https://api.notion.com/v1/pages");
        }
        finally
        {
            NotionCommand.CreateHttpClient = originalFactory;
            Environment.SetEnvironmentVariable("BUKIT_TEST_IMPORT_NOTION_MAP_TOKEN", originalToken);
        }
    }

    [Fact]
    public async Task Import_WithPushNotion_UsesGeneratedDefaultDatabaseMapWhenPresent()
    {
        CreateDemoHtml("index.html", "Home");
        var siteSeedDir = Path.Combine(_tempDir, "sites", "push-notion-default-map-test", "notion-seed");
        Directory.CreateDirectory(siteSeedDir);
        File.WriteAllText(Path.Combine(siteSeedDir, "notion-database-map.yaml"), """
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
            if (req.RequestUri!.AbsolutePath.Contains("/databases/") && req.Method == HttpMethod.Get)
                return OkDatabaseSchema();
            if (req.RequestUri.AbsolutePath.Contains("/query"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"results":[],"has_more":false}""")
                };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"page-1"}""")
            };
        });

        var opts = BaseOptions();
        opts["--theme"] = "push-notion-default-map-test";
        opts["--push-notion"] = "true";
        opts["--notion-token-env"] = "BUKIT_TEST_IMPORT_NOTION_DEFAULT_MAP_TOKEN";

        var originalFactory = NotionCommand.CreateHttpClient;
        var originalToken = Environment.GetEnvironmentVariable("BUKIT_TEST_IMPORT_NOTION_DEFAULT_MAP_TOKEN");
        NotionCommand.CreateHttpClient = () => new HttpClient(handler);
        Environment.SetEnvironmentVariable("BUKIT_TEST_IMPORT_NOTION_DEFAULT_MAP_TOKEN", "secret_default_map");
        try
        {
            var result = await ImportCommand.RunAsync(MakeCommand(opts, ["html-demo", _tempDir]));

            Assert.Equal(0, result);
            Assert.DoesNotContain(requests, r => r.RequestUri!.AbsoluteUri == "https://api.notion.com/v1/databases");
            var payloads = requests
                .Where(r => r.RequestUri!.AbsoluteUri == "https://api.notion.com/v1/pages")
                .Select(r => r.Content!.ReadAsStringAsync().GetAwaiter().GetResult())
                .ToList();
            Assert.Contains(payloads, p => p.Contains("\"database_id\": \"pages-db\""));
        }
        finally
        {
            NotionCommand.CreateHttpClient = originalFactory;
            Environment.SetEnvironmentVariable("BUKIT_TEST_IMPORT_NOTION_DEFAULT_MAP_TOKEN", originalToken);
        }
    }

    [Fact]
    public async Task Import_WithPushNotion_DefaultDatabaseMapMissingDatabaseIds_Returns2()
    {
        CreateDemoHtml("index.html", "Home");

        var requests = new List<HttpRequestMessage>();
        var handler = new RecordingHandler(req =>
        {
            requests.Add(CloneRequest(req));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"page-1"}""")
            };
        });

        var opts = BaseOptions();
        opts["--theme"] = "push-notion-empty-default-map-test";
        opts["--push-notion"] = "true";
        opts["--notion-token-env"] = "BUKIT_TEST_IMPORT_NOTION_EMPTY_MAP_TOKEN";

        var originalFactory = NotionCommand.CreateHttpClient;
        var originalToken = Environment.GetEnvironmentVariable("BUKIT_TEST_IMPORT_NOTION_EMPTY_MAP_TOKEN");
        var originalError = Console.Error;
        using var error = new StringWriter();
        NotionCommand.CreateHttpClient = () => new HttpClient(handler);
        Environment.SetEnvironmentVariable("BUKIT_TEST_IMPORT_NOTION_EMPTY_MAP_TOKEN", "secret_empty_map");
        Console.SetError(error);
        try
        {
            var result = await ImportCommand.RunAsync(MakeCommand(opts, ["html-demo", _tempDir]));

            Assert.Equal(2, result);
            Assert.Empty(requests);
            Assert.Contains("databaseId values are empty", error.ToString());
            Assert.Contains("--create-missing-notion-databases --notion-parent-page-id <id>", error.ToString());
        }
        finally
        {
            Console.SetError(originalError);
            NotionCommand.CreateHttpClient = originalFactory;
            Environment.SetEnvironmentVariable("BUKIT_TEST_IMPORT_NOTION_EMPTY_MAP_TOKEN", originalToken);
        }
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
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

    [Fact]
    public async Task MultipleHtmlFiles_CorrectlySplits()
    {
        CreateDemoHtml("index.html", "Home");
        CreateDemoHtml("about.html", "About");

        var opts = BaseOptions();
        opts["--theme"] = "multi-test";
        var cmd = MakeCommand(opts, ["html-demo", _tempDir]);

        var result = await ImportCommand.RunAsync(cmd);

        Assert.Equal(0, result);
        var pagesDir = Path.Combine(_tempDir, "themes", "multi-test", "layouts", "pages");
        Assert.True(File.Exists(Path.Combine(pagesDir, "index.html")));
        Assert.True(File.Exists(Path.Combine(pagesDir, "list.html")));
    }

    [Fact]
    public async Task AssetsCopied()
    {
        CreateDemoHtml("index.html", "Test");
        Directory.CreateDirectory(Path.Combine(_tempDir, "img"));
        File.WriteAllText(Path.Combine(_tempDir, "img", "logo.png"), "fake");

        var html = File.ReadAllText(Path.Combine(_tempDir, "index.html"));
        html = html.Replace("</main>", "<img src=\"img/logo.png\" /></main>");
        File.WriteAllText(Path.Combine(_tempDir, "index.html"), html);

        var opts = BaseOptions();
        opts["--theme"] = "asset-test";
        var cmd = MakeCommand(opts, ["html-demo", _tempDir]);

        var result = await ImportCommand.RunAsync(cmd);

        Assert.Equal(0, result);
        Assert.True(
            File.Exists(Path.Combine(_tempDir, "themes", "asset-test", "assets", "img", "logo.png")) ||
            File.Exists(Path.Combine(_tempDir, "themes", "asset-test", "static", "img", "logo.png")));
    }

    [Fact]
    public async Task SiteYamlCreated()
    {
        CreateDemoHtml("index.html", "Test");

        var opts = BaseOptions();
        opts["--theme"] = "yaml-test";
        var cmd = MakeCommand(opts, ["html-demo", _tempDir]);

        var result = await ImportCommand.RunAsync(cmd);

        Assert.Equal(0, result);
        Assert.True(File.Exists(Path.Combine(_tempDir, "sites", "yaml-test", "site.yaml")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "sites", "yaml-test", "content", "index.md")));
    }

    [Fact]
    public async Task PathTraversal_Rejected()
    {
        CreateDemoHtml("index.html", "Test");
        var html = File.ReadAllText(Path.Combine(_tempDir, "index.html"));
        html = html.Replace("</main>", "<img src=\"../etc/passwd\" /></main>");
        File.WriteAllText(Path.Combine(_tempDir, "index.html"), html);

        var opts = BaseOptions();
        opts["--theme"] = "traversal-test";
        var cmd = MakeCommand(opts, ["html-demo", _tempDir]);

        var result = await ImportCommand.RunAsync(cmd);

        Assert.Equal(0, result);
        Assert.False(File.Exists(Path.Combine(_tempDir, "themes", "traversal-test", "assets", "..", "etc", "passwd")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "passwd")));
    }

    [Fact]
    public async Task SensitiveFiles_Excluded()
    {
        CreateDemoHtml("index.html", "Test");
        File.WriteAllText(Path.Combine(_tempDir, ".env"), "SECRET=xxx");

        var opts = BaseOptions();
        opts["--theme"] = "sensitive-test";
        var cmd = MakeCommand(opts, ["html-demo", _tempDir]);

        var result = await ImportCommand.RunAsync(cmd);

        Assert.Equal(2, result);
    }

    [Fact]
    public async Task ImportThenBuild_DefaultOutput_BuildsSuccessfully()
    {
        var demoDir = Path.Combine(_tempDir, "demo");
        Directory.CreateDirectory(Path.Combine(demoDir, "assets", "css"));
        File.WriteAllText(Path.Combine(demoDir, "assets", "css", "style.css"), "body{font-family:sans-serif}");
        File.WriteAllText(Path.Combine(demoDir, "index.html"),
            "<html><head><title>Home</title><link rel=\"stylesheet\" href=\"assets/css/style.css\"></head><body><header><nav>Nav</nav></header><main><h1>Home</h1><p>Welcome.</p></main><footer>Footer</footer></body></html>");
        File.WriteAllText(Path.Combine(demoDir, "about.html"),
            "<html><head><title>About</title></head><body><header><nav>Nav</nav></header><main><h1>About</h1><p>About body.</p></main><footer>Footer</footer></body></html>");

        var opts = BaseOptions();
        opts["--theme"] = "build-test";
        opts["--force"] = "true";
        var importResult = await ImportCommand.RunAsync(MakeCommand(opts, ["html-demo", demoDir]));

        Assert.Equal(0, importResult);

        var siteConfig = Path.Combine(_tempDir, "sites", "build-test", "site.yaml");
        var buildResult = await BuildCommand.RunAsync(MakeCommand(new Dictionary<string, string?>
        {
            ["--config"] = siteConfig,
            ["--output"] = "dist-build-test",
            ["--no-clean"] = "true"
        }, []));

        Assert.Equal(0, buildResult);
        Assert.True(File.Exists(Path.Combine(_tempDir, "dist-build-test", "index.html")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "dist-build-test", "about", "index.html")));
    }

    [Fact]
    public async Task Verify_UsesGeneratedSiteConfigAndBuilds()
    {
        var demoDir = Path.Combine(_tempDir, "verify-demo");
        Directory.CreateDirectory(demoDir);
        File.WriteAllText(Path.Combine(demoDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1><p>Welcome.</p></main></body></html>");
        File.WriteAllText(Path.Combine(demoDir, "about.html"),
            "<html><head><title>About</title></head><body><main><h1>About</h1><p>About us.</p></main></body></html>");

        var opts = BaseOptions();
        opts["--theme"] = "verify-test";
        opts["--verify"] = "true";
        var result = await ImportCommand.RunAsync(MakeCommand(opts, ["html-demo", demoDir]));

        Assert.Equal(0, result);
        Assert.True(File.Exists(Path.Combine(_tempDir, "sites", "verify-test", "site.yaml")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "dist", "index.html")));
    }

    [Fact]
    public async Task ContentSourceNotion_DefaultsToBuildableMarkdownWithNotionSeed()
    {
        var demoDir = Path.Combine(_tempDir, "notion-default-demo");
        Directory.CreateDirectory(demoDir);
        File.WriteAllText(Path.Combine(demoDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1><p>Welcome.</p></main></body></html>");
        File.WriteAllText(Path.Combine(demoDir, "about.html"),
            "<html><head><title>About</title></head><body><main><h1>About</h1><p>About us.</p></main></body></html>");

        var opts = BaseOptions();
        opts["--theme"] = "notion-default-test";
        opts["--force"] = "true";
        opts["--verify"] = "true";
        opts["--content-source"] = "notion";

        var result = await ImportCommand.RunAsync(MakeCommand(opts, ["html-demo", demoDir]));

        Assert.Equal(0, result);
        Assert.True(File.Exists(Path.Combine(_tempDir, "sites", "notion-default-test", "content", "index.md")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "sites", "notion-default-test", "notion-seed", "pages.json")));
        var siteYaml = File.ReadAllText(Path.Combine(_tempDir, "sites", "notion-default-test", "site.yaml"));
        Assert.Contains("provider: markdown", siteYaml);
        Assert.DoesNotContain("provider: notion", siteYaml);
        Assert.True(File.Exists(Path.Combine(_tempDir, "dist", "index.html")));
    }

    [Fact]
    public async Task BuildSourceNotion_UsesNotionProviderAndSkipsMarkdownDraft()
    {
        var demoDir = Path.Combine(_tempDir, "notion-build-source-demo");
        Directory.CreateDirectory(demoDir);
        File.WriteAllText(Path.Combine(demoDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1><p>Welcome.</p></main></body></html>");

        var opts = BaseOptions();
        opts["--theme"] = "notion-build-source-test";
        opts["--force"] = "true";
        opts["--content-source"] = "notion";
        opts["--build-source"] = "notion";

        var result = await ImportCommand.RunAsync(MakeCommand(opts, ["html-demo", demoDir]));

        Assert.Equal(0, result);
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "sites", "notion-build-source-test", "content")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "sites", "notion-build-source-test", "notion-seed", "pages.json")));
        var siteYaml = File.ReadAllText(Path.Combine(_tempDir, "sites", "notion-build-source-test", "site.yaml"));
        Assert.Contains("sources:", siteYaml);
        Assert.Contains("name: pages", siteYaml);
        Assert.Contains("collection: page", siteYaml);
        Assert.Contains("databaseId: ${NOTION_PAGES_DATABASE_ID}", siteYaml);
        Assert.Contains("name: navigation", siteYaml);
        Assert.Contains("mode: data", siteYaml);
        Assert.Contains("collection: navigation", siteYaml);
        Assert.DoesNotContain("provider: markdown", siteYaml);
    }

    [Fact]
    public async Task BuildSourceInvalid_Returns2()
    {
        var demoDir = Path.Combine(_tempDir, "invalid-build-source-demo");
        Directory.CreateDirectory(demoDir);
        File.WriteAllText(Path.Combine(demoDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");

        var opts = BaseOptions();
        opts["--theme"] = "invalid-build-source-test";
        opts["--build-source"] = "json";

        var result = await ImportCommand.RunAsync(MakeCommand(opts, ["html-demo", demoDir]));

        Assert.Equal(2, result);
    }

    [Fact]
    public async Task BuildSourceNotion_WithJsonContentSource_Returns2()
    {
        var demoDir = Path.Combine(_tempDir, "invalid-build-content-source-demo");
        Directory.CreateDirectory(demoDir);
        File.WriteAllText(Path.Combine(demoDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");

        var opts = BaseOptions();
        opts["--theme"] = "invalid-build-content-source-test";
        opts["--content-source"] = "json";
        opts["--build-source"] = "notion";

        var result = await ImportCommand.RunAsync(MakeCommand(opts, ["html-demo", demoDir]));

        Assert.Equal(2, result);
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "sites", "invalid-build-content-source-test")));
    }

    [Fact]
    public async Task ContentSourceMarkdown_Returns2()
    {
        var demoDir = Path.Combine(_tempDir, "invalid-content-source-demo");
        Directory.CreateDirectory(demoDir);
        File.WriteAllText(Path.Combine(demoDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");

        var opts = BaseOptions();
        opts["--theme"] = "invalid-content-source-test";
        opts["--content-source"] = "markdown";

        var result = await ImportCommand.RunAsync(MakeCommand(opts, ["html-demo", demoDir]));

        Assert.Equal(2, result);
    }

    [Fact]
    public async Task Verify_ListPages_DoNotConflictWithCollectionListRoutes()
    {
        var demoDir = Path.Combine(_tempDir, "list-demo");
        Directory.CreateDirectory(demoDir);
        File.WriteAllText(Path.Combine(demoDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(demoDir, "about.html"),
            "<html><head><title>About</title></head><body><main><h1>About</h1></main></body></html>");
        File.WriteAllText(Path.Combine(demoDir, "insights.html"),
            "<html><head><title>Insights</title></head><body><main><h1>Insights</h1></main></body></html>");
        File.WriteAllText(Path.Combine(demoDir, "companies.html"),
            "<html><head><title>Companies</title></head><body><main><h1>Companies</h1></main></body></html>");
        File.WriteAllText(Path.Combine(demoDir, "article-detail.html"),
            "<html><head><title>Article</title></head><body><main><h1>Article</h1><p>Body.</p></main></body></html>");
        File.WriteAllText(Path.Combine(demoDir, "company-detail.html"),
            "<html><head><title>Company</title></head><body><main><h1>Company</h1><p>Body.</p></main></body></html>");

        var opts = BaseOptions();
        opts["--theme"] = "list-route-test";
        opts["--verify"] = "true";
        var result = await ImportCommand.RunAsync(MakeCommand(opts, ["html-demo", demoDir]));

        Assert.Equal(0, result);
        Assert.False(File.Exists(Path.Combine(_tempDir, "sites", "list-route-test", "content", "insights.md")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "sites", "list-route-test", "content", "companies.md")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "dist", "insights", "index.html")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "dist", "companies", "index.html")));
    }

    [Fact]
    public async Task ImportThenDoctor_DoesNotWarnForSeoFieldAccessOrBaseUrlAssets()
    {
        var demoDir = Path.Combine(_tempDir, "doctor-demo");
        Directory.CreateDirectory(Path.Combine(demoDir, "assets", "css"));
        File.WriteAllText(Path.Combine(demoDir, "assets", "css", "style.css"), "body{}");
        File.WriteAllText(Path.Combine(demoDir, "index.html"),
            "<html><head><title>Home</title><link rel=\"stylesheet\" href=\"assets/css/style.css\"></head><body><main><h1>Home</h1><p>Welcome.</p></main></body></html>");
        File.WriteAllText(Path.Combine(demoDir, "about.html"),
            "<html><head><title>About</title></head><body><main><h1>About</h1><p>About us.</p></main></body></html>");

        var opts = BaseOptions();
        opts["--theme"] = "doctor-import-test";
        opts["--force"] = "true";
        var importResult = await ImportCommand.RunAsync(MakeCommand(opts, ["html-demo", demoDir]));

        Assert.Equal(0, importResult);

        var siteConfig = Path.Combine(_tempDir, "sites", "doctor-import-test", "site.yaml");
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            var doctorResult = await DoctorCommand.RunAsync(MakeCommand(new Dictionary<string, string?>
            {
                ["--config"] = siteConfig
            }, []));

            Assert.Equal(0, doctorResult);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = writer.ToString();
        Assert.Contains("No unknown template variables detected", output);
        Assert.DoesNotContain("page.seo_title", output);
        Assert.DoesNotContain("page.seo_description", output);
        Assert.DoesNotContain("hardcoded URL", output);
        Assert.DoesNotContain("href=\"/assets/css/style.css\"", output);
    }

    [Fact]
    public async Task ImportVerify_DoesNotEmitResolvedImportWarnings()
    {
        var demoDir = Path.Combine(_tempDir, "verify-warning-demo");
        Directory.CreateDirectory(Path.Combine(demoDir, "assets", "css"));
        File.WriteAllText(Path.Combine(demoDir, "assets", "css", "style.css"), "body{}");
        File.WriteAllText(Path.Combine(demoDir, "index.html"),
            """
            <html>
              <head><title>Home</title><link rel="stylesheet" href="assets/css/style.css"></head>
              <body>
                <main>
                  <h1>Home</h1>
                  <p>Welcome.</p>
                  <a href="about.html">About</a>
                  <div class="pagination"><a href="page-1.html">← 上一页</a><span>第 2 / 3 页</span><a href="page-3.html">下一页 →</a></div>
                </main>
              </body>
            </html>
            """);
        File.WriteAllText(Path.Combine(demoDir, "about.html"),
            "<html><head><title>About</title></head><body><main><h1>About</h1><p>About.</p></main></body></html>");

        var opts = BaseOptions();
        opts["--theme"] = "verify-warning-test";
        opts["--force"] = "true";
        opts["--verify"] = "true";

        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            var result = await ImportCommand.RunAsync(MakeCommand(opts, ["html-demo", demoDir]));
            Assert.Equal(0, result);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = writer.ToString();
        Assert.DoesNotContain("Static HTML files in static dir are skipped", output);
        Assert.DoesNotContain("seo.site_url_missing", output);
        Assert.DoesNotContain("hardcoded text issue", output);
        Assert.DoesNotContain("上一页", output);
        Assert.DoesNotContain("下一页", output);
    }

    [Fact]
    public async Task SeedJson_WritesMarkdownContent()
    {
        var seedDir = Path.Combine(_tempDir, "seed-json");
        Directory.CreateDirectory(seedDir);
        File.WriteAllText(Path.Combine(seedDir, "pages.json"), """
[
  {
    "title": "About",
    "slug": "about",
    "type": "page",
    "summary": "About summary",
    "content": "<p>About body.</p>",
    "language": "zh",
    "published": true
  }
]
""");
        File.WriteAllText(Path.Combine(seedDir, "posts.json"), """
[
  {
    "title": "News",
    "slug": "news",
    "summary": "News summary",
    "content": "<p>News body.</p>",
    "language": "zh",
    "published": true
  }
]
""");

        var outputDir = Path.Combine(_tempDir, "content-json");
        var result = await ImportCommand.RunAsync(MakeCommand(new Dictionary<string, string?>
        {
            ["--output"] = outputDir
        }, ["seed", seedDir]));

        Assert.Equal(0, result);
        var page = File.ReadAllText(Path.Combine(outputDir, "pages", "about.md"));
        var post = File.ReadAllText(Path.Combine(outputDir, "posts", "news.md"));
        Assert.Contains("title: \"About\"", page);
        Assert.Contains("<p>About body.</p>", page);
        Assert.Contains("type: \"post\"", post);
    }

    [Fact]
    public async Task SeedYaml_WritesMarkdownContent()
    {
        var seedDir = Path.Combine(_tempDir, "seed-yaml");
        Directory.CreateDirectory(seedDir);
        File.WriteAllText(Path.Combine(seedDir, "services.yaml"), """
-
  title: "Consulting"
  slug: "consulting"
  summary: "Service summary"
  content: "<p>Service body.</p>"
  language: "zh"
  published: true
""");

        var outputDir = Path.Combine(_tempDir, "content-yaml");
        var result = await ImportCommand.RunAsync(MakeCommand(new Dictionary<string, string?>
        {
            ["--output"] = outputDir
        }, ["seed", seedDir]));

        Assert.Equal(0, result);
        var service = File.ReadAllText(Path.Combine(outputDir, "services", "consulting.md"));
        Assert.Contains("title: \"Consulting\"", service);
        Assert.Contains("type: \"service\"", service);
        Assert.Contains("<p>Service body.</p>", service);
    }

    [Fact]
    public async Task Import_WithPushNotion_QueryFailed_DoesNotCreate()
    {
        CreateDemoHtml("index.html", "Home");
        File.WriteAllText(Path.Combine(_tempDir, "about.html"),
            "<html><head><title>About</title></head><body><main><h1>About</h1></main></body></html>");

        var requests = new List<HttpRequestMessage>();
        var handler = new RecordingHandler(req =>
        {
            requests.Add(CloneRequest(req));
            if (req.RequestUri!.AbsolutePath.Contains("/query"))
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("""{"message":"Invalid filter"}""")
                };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"page-1"}""")
            };
        });

        var opts = BaseOptions();
        opts["--theme"] = "query-failed-test";
        opts["--push-notion"] = "true";
        opts["--notion-database-id"] = "db123";
        opts["--notion-token-env"] = "BUKIT_TEST_QF_TOKEN";
        opts["--no-validate-notion-schema"] = "true";

        var originalFactory = NotionCommand.CreateHttpClient;
        var originalToken = Environment.GetEnvironmentVariable("BUKIT_TEST_QF_TOKEN");
        NotionCommand.CreateHttpClient = () => new HttpClient(handler);
        Environment.SetEnvironmentVariable("BUKIT_TEST_QF_TOKEN", "secret_qf");
        try
        {
            await ImportCommand.RunAsync(MakeCommand(opts, ["html-demo", _tempDir]));

            var reportPath = Path.Combine(_tempDir, "sites", "query-failed-test", "notion-seed", "notion-push-report.json");
            Assert.True(File.Exists(reportPath), $"Report not found at {reportPath}");
            var report = File.ReadAllText(reportPath);
            Assert.Contains("query-failed", report);
        }
        finally
        {
            NotionCommand.CreateHttpClient = originalFactory;
            Environment.SetEnvironmentVariable("BUKIT_TEST_QF_TOKEN", originalToken);
        }
    }

    [Fact]
    public async Task Import_WithPushNotion_SchemaValidationFails_BlocksPush()
    {
        CreateDemoHtml("index.html", "Home");

        var requests = new List<HttpRequestMessage>();
        var handler = new RecordingHandler(req =>
        {
            requests.Add(CloneRequest(req));
            if (req.RequestUri!.AbsolutePath.Contains("/databases/") && req.Method == HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"properties":{}}""")
                };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"page-1"}""")
            };
        });

        var opts = BaseOptions();
        opts["--theme"] = "schema-fail-test";
        opts["--push-notion"] = "true";
        opts["--notion-database-id"] = "db123";
        opts["--notion-token-env"] = "BUKIT_TEST_SCHEMA_TOKEN";

        var originalFactory = NotionCommand.CreateHttpClient;
        var originalToken = Environment.GetEnvironmentVariable("BUKIT_TEST_SCHEMA_TOKEN");
        NotionCommand.CreateHttpClient = () => new HttpClient(handler);
        Environment.SetEnvironmentVariable("BUKIT_TEST_SCHEMA_TOKEN", "secret_sc");
        try
        {
            var result = await ImportCommand.RunAsync(MakeCommand(opts, ["html-demo", _tempDir]));
            Assert.Equal(2, result);

            var pushRequest = requests.FirstOrDefault(r =>
                r.RequestUri!.AbsolutePath.Contains("/pages") && r.Method == HttpMethod.Post);
            Assert.Null(pushRequest);
            Assert.Equal(1, requests.Count(r =>
                r.RequestUri!.AbsolutePath.Contains("/databases/") && r.Method == HttpMethod.Get));
        }
        finally
        {
            NotionCommand.CreateHttpClient = originalFactory;
            Environment.SetEnvironmentVariable("BUKIT_TEST_SCHEMA_TOKEN", originalToken);
        }
    }

}
