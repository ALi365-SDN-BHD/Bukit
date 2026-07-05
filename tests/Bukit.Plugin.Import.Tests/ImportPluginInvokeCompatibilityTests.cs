using System.Text.Json;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Runtime;
using Bukit.Plugin.Abstractions.Security;
using Xunit;

namespace Bukit.Plugin.Import.Tests;

public sealed class ImportPluginInvokeCompatibilityTests : IDisposable
{
    private readonly string _rootDir;

    public ImportPluginInvokeCompatibilityTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-plugin-import-invoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
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
        Assert.Contains(response.Diagnostics, diagnostic => diagnostic.Message == "缺少必填参数: <demo-dir>");
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
        Assert.Contains(response.Diagnostics, diagnostic => diagnostic.Message == "缺少必填选项: --theme <名称>");
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
            diagnostic.Message == "--push-notion 不能与 --dry-run 同时使用。先生成草稿后再执行实际推送。");
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
            diagnostic.Message == "--create-missing-notion-databases 需要 --notion-parent-page-id <id>。");
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
        Assert.Contains(response.Diagnostics, diagnostic => diagnostic.Message == "缺少必填选项: --output <content-dir>");
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
        Assert.Contains(response.Messages, message => message.Message.Contains("seed import 完成: records=1 written=1", StringComparison.Ordinal));
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
        Assert.Contains(response.Messages, message => message.Message.Contains("未提取到共享布局", StringComparison.Ordinal));
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
}
