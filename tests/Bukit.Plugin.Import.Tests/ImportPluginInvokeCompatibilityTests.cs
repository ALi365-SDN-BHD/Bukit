using System.Net;
using System.Text.Json;
using Bukit.Importing;
using Bukit.Notion.Transport;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Runtime;
using Bukit.Plugin.Abstractions.Security;
using Xunit;

namespace Bukit.Plugin.Import.Tests;

public sealed class ImportPluginInvokeCompatibilityTests : IDisposable
{
    private readonly string _rootDir;

    private static Func<HttpMessageHandler?> TestHttpMessageHandlerFactory
    {
        set => ImportNotionPushWorkflow.CreateNotionClient = options =>
        {
            var handler = value();
            return handler is null
                ? null
                : new NotionClient(
                    options,
                    handler,
                    static (_, _) => Task.CompletedTask,
                    static () => DateTimeOffset.UtcNow);
        };
    }

    public ImportPluginInvokeCompatibilityTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-plugin-import-invoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        TestHttpMessageHandlerFactory = static () => null;
        if (Directory.Exists(_rootDir))
            Directory.Delete(_rootDir, recursive: true);
    }

    [Fact]
    public async Task Invoke_HtmlDemoWithoutArgument_ReturnsTwo()
    {
        var response = await ImportPluginInvoker.InvokeAsync(Request(
            path: ["import", "html-demo"],
            arguments: [],
            options: new Dictionary<string, JsonElement>
            {
                ["--theme"] = Json("demo-theme")
            }));

        Assert.Equal(2, response.ExitCode);
        Assert.Contains(response.Diagnostics, diagnostic => diagnostic.Message == "Missing required argument: <demo-dir>");
    }

    [Fact]
    public async Task Invoke_HtmlDemoWithoutTheme_ReturnsTwo()
    {
        var demoDir = Path.Combine(_rootDir, "demo");
        Directory.CreateDirectory(demoDir);

        var response = await ImportPluginInvoker.InvokeAsync(Request(
            path: ["import", "html-demo"],
            arguments: ["demo"],
            options: new Dictionary<string, JsonElement>()));

        Assert.Equal(2, response.ExitCode);
        Assert.Contains(response.Diagnostics, diagnostic => diagnostic.Message == "Missing required option: --theme <name>");
    }

    [Fact]
    public async Task Invoke_HtmlDemoPushNotionCannotCombineWithDryRun()
    {
        Directory.CreateDirectory(Path.Combine(_rootDir, "demo"));

        var response = await ImportPluginInvoker.InvokeAsync(Request(
            path: ["import", "html-demo"],
            arguments: ["demo"],
            options: new Dictionary<string, JsonElement>
            {
                ["--theme"] = Json("demo-theme"),
                ["--push-notion"] = Json(true),
                ["--dry-run"] = Json(true)
            },
            permissions: PermissionsWithNotionToken()));

        Assert.Equal(2, response.ExitCode);
        Assert.Contains(response.Diagnostics, diagnostic =>
            diagnostic.Message == "--push-notion cannot be used with --dry-run. Generate first, then push.");
    }

    [Fact]
    public async Task Invoke_HtmlDemoCreateMissingNotionDatabasesRequiresParentPage()
    {
        Directory.CreateDirectory(Path.Combine(_rootDir, "demo"));

        var response = await ImportPluginInvoker.InvokeAsync(Request(
            path: ["import", "html-demo"],
            arguments: ["demo"],
            options: new Dictionary<string, JsonElement>
            {
                ["--theme"] = Json("demo-theme"),
                ["--push-notion"] = Json(true),
                ["--create-missing-notion-databases"] = Json(true)
            },
            permissions: PermissionsWithNotionToken()));

        Assert.Equal(2, response.ExitCode);
        Assert.Contains(response.Diagnostics, diagnostic =>
            diagnostic.Message == "--create-missing-notion-databases requires --notion-parent-page-id <id>.");
    }

    [Fact]
    public async Task Invoke_SeedWithoutOutput_ReturnsTwo()
    {
        Directory.CreateDirectory(Path.Combine(_rootDir, "seed"));

        var response = await ImportPluginInvoker.InvokeAsync(Request(
            path: ["import", "seed"],
            arguments: ["seed"],
            options: new Dictionary<string, JsonElement>()));

        Assert.Equal(2, response.ExitCode);
        Assert.Contains(response.Diagnostics, diagnostic => diagnostic.Message == "Missing required option: --output <content-dir>");
    }

    [Fact]
    public async Task Invoke_SeedSuccess_WritesMarkdownAndRelativeArtifacts()
    {
        var seedDir = Path.Combine(_rootDir, "seed");
        Directory.CreateDirectory(seedDir);
        File.WriteAllText(Path.Combine(seedDir, "posts.json"), """
[
  { "title": "Hello", "slug": "hello", "content": "Body" }
]
""");

        var response = await ImportPluginInvoker.InvokeAsync(Request(
            path: ["import", "seed"],
            arguments: ["seed"],
            options: new Dictionary<string, JsonElement>
            {
                ["--output"] = Json("content")
            }));

        Assert.True(response.Success);
        Assert.Equal(0, response.ExitCode);
        Assert.True(File.Exists(Path.Combine(_rootDir, "content", "posts", "hello.md")));
        Assert.Contains(response.Messages, message => message.Message.Contains("seed import complete: records=1 written=1", StringComparison.Ordinal));
        Assert.All(response.Artifacts, artifact => Assert.False(Path.IsPathRooted(artifact.Path)));
    }

    [Fact]
    public async Task Invoke_HtmlDemoDryRun_ReturnsSuccessAndDoesNotWriteTheme()
    {
        CreateMinimalDemo();

        var response = await ImportPluginInvoker.InvokeAsync(Request(
            path: ["import", "html-demo"],
            arguments: ["demo"],
            options: new Dictionary<string, JsonElement>
            {
                ["--theme"] = Json("demo-theme"),
                ["--dry-run"] = Json(true)
            }));

        Assert.True(response.Success);
        Assert.Equal(0, response.ExitCode);
        Assert.False(Directory.Exists(Path.Combine(_rootDir, "themes", "demo-theme")));
        Assert.Contains(response.Messages, message => message.Message.Contains("No shared layout", StringComparison.Ordinal));
        Assert.Contains(response.Diagnostics, diagnostic =>
            diagnostic.Code == "import.content.author_missing" &&
            diagnostic.Severity == "warning" &&
            diagnostic.Path == "sites/demo-theme/content");
    }

    [Fact]
    public async Task Invoke_HtmlDemoSitePathProjectRoot_ReturnsSafeArtifactPaths()
    {
        CreateMinimalDemo();

        var response = await ImportPluginInvoker.InvokeAsync(Request(
            path: ["import", "html-demo"],
            arguments: ["demo"],
            options: new Dictionary<string, JsonElement>
            {
                ["--theme"] = Json("demo-theme"),
                ["--site-path"] = Json(".")
            }));

        Assert.True(response.Success);
        Assert.Equal(0, response.ExitCode);
        Assert.DoesNotContain(response.Artifacts, artifact => artifact.Path is "." or "");
        Assert.Contains(response.Artifacts, artifact => artifact.Type == "site" && artifact.Path == "site.yaml");
    }

    [Fact]
    public async Task Invoke_HtmlDemoCustomNotionTokenEnvWithoutGrantReturnsTwo()
    {
        var response = await ImportPluginInvoker.InvokeAsync(Request(
            path: ["import", "html-demo"],
            arguments: ["demo"],
            options: new Dictionary<string, JsonElement>
            {
                ["--theme"] = Json("demo-theme"),
                ["--push-notion"] = Json(true),
                ["--notion-token-env"] = Json("CUSTOM_TOKEN")
            },
            permissions: PermissionsWithNotionToken()));

        Assert.Equal(2, response.ExitCode);
        Assert.Contains(response.Diagnostics, diagnostic => diagnostic.Code == "plugin.import.envDenied");
    }

    [Fact]
    public async Task Invoke_NotionPushDryRunWritesReportArtifact()
    {
        var seedDir = Path.Combine(_rootDir, "seed");
        Directory.CreateDirectory(seedDir);
        File.WriteAllText(Path.Combine(seedDir, "pages.json"), """
[
  { "title": "About", "slug": "about", "content": "<p>Hello</p>" }
]
""");

        var response = await ImportPluginInvoker.InvokeAsync(Request(
            path: ["notion", "push"],
            arguments: [],
            options: new Dictionary<string, JsonElement>
            {
                ["--input"] = Json("seed"),
                ["--database-id"] = Json("db-single"),
                ["--dry-run"] = Json(true),
                ["--report"] = Json("reports/notion-plan.json")
            }));

        Assert.True(response.Success);
        Assert.Equal(0, response.ExitCode);
        Assert.True(File.Exists(Path.Combine(_rootDir, "reports", "notion-plan.json")));
        Assert.Contains(response.Messages, message => message.Message.Contains("notion push dry-run complete: records=1", StringComparison.Ordinal));
        var artifact = Assert.Single(response.Artifacts, artifact => artifact.Type == "report");
        Assert.Equal("reports/notion-plan.json", artifact.Path);
    }

    [Fact]
    public async Task Invoke_NotionPushCustomTokenEnvWithoutGrantReturnsTwo()
    {
        var response = await ImportPluginInvoker.InvokeAsync(Request(
            path: ["notion", "push"],
            arguments: [],
            options: new Dictionary<string, JsonElement>
            {
                ["--input"] = Json("seed"),
                ["--dry-run"] = Json(true),
                ["--token-env"] = Json("CUSTOM_TOKEN")
            },
            permissions: PermissionsWithNotionToken()));

        Assert.Equal(2, response.ExitCode);
        Assert.Contains(response.Diagnostics, diagnostic => diagnostic.Code == "plugin.import.envDenied");
    }

    [Fact]
    public async Task Invoke_NotionValidateSchemaSuccessWritesReportArtifact()
    {
        TestHttpMessageHandlerFactory = () => new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
{
  "properties": {
    "Title": { "type": "title" },
    "Slug": { "type": "rich_text" },
    "Type": { "type": "select" },
    "Summary": { "type": "rich_text" },
    "Language": { "type": "select" },
    "Published": { "type": "checkbox" },
    "SeoTitle": { "type": "rich_text" },
    "SeoDescription": { "type": "rich_text" }
  }
}
""")
            });
        using var token = new EnvironmentVariableScope("NOTION_TOKEN", "secret");

        var response = await ImportPluginInvoker.InvokeAsync(Request(
            path: ["notion", "validate-schema"],
            arguments: [],
            options: new Dictionary<string, JsonElement>
            {
                ["--database-id"] = Json("db-schema"),
                ["--report"] = Json("reports/schema.json")
            },
            permissions: PermissionsWithNotionToken()));

        Assert.True(response.Success);
        Assert.Equal(0, response.ExitCode);
        Assert.Contains(response.Messages, message => message.Message.Contains("schema validation: PASSED", StringComparison.Ordinal));
        var artifact = Assert.Single(response.Artifacts, artifact => artifact.Type == "report");
        Assert.Equal("reports/schema.json", artifact.Path);
    }

    [Fact]
    public async Task Invoke_NotionValidateSchemaMissingDatabaseIdReturnsTwo()
    {
        var response = await ImportPluginInvoker.InvokeAsync(Request(
            path: ["notion", "validate-schema"],
            arguments: [],
            options: new Dictionary<string, JsonElement>(),
            permissions: PermissionsWithNotionToken()));

        Assert.Equal(2, response.ExitCode);
        Assert.Contains(response.Diagnostics, diagnostic =>
            diagnostic.Message == "Missing required option: --database-id <id>");
    }

    private PluginInvokeRequest Request(
        IReadOnlyList<string> path,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, JsonElement> options,
        PluginPermissionSet? permissions = null)
        => new(
            Type: "invoke",
            Protocol: "bukit-plugin-v1",
            RequestId: "req",
            Host: new PluginHostInfo("Bukit", "1.0.0", "test"),
            Command: new PluginInvokeCommand(path.Last(), Path: path, Arguments: arguments, Options: options),
            Context: new PluginInvokeContext(_rootDir, _rootDir),
            Permissions: permissions ?? new PluginPermissionSet());

    private static PluginPermissionSet PermissionsWithNotionToken()
        => new(Environment: new PluginEnvironmentPermission(Read: ["NOTION_TOKEN"]));

    private void CreateMinimalDemo()
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
    }

    private static JsonElement Json(string value)
        => JsonSerializer.SerializeToElement(value);

    private static JsonElement Json(bool value)
        => JsonSerializer.SerializeToElement(value);

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _previous;

        public EnvironmentVariableScope(string name, string? value)
        {
            _name = name;
            _previous = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
            => Environment.SetEnvironmentVariable(_name, _previous);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
