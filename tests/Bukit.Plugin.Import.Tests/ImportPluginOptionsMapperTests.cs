using System.Text.Json;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Runtime;
using Bukit.Plugin.Abstractions.Security;
using Xunit;

namespace Bukit.Plugin.Import.Tests;

public sealed class ImportPluginOptionsMapperTests : IDisposable
{
    private readonly string _rootDir;

    public ImportPluginOptionsMapperTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-plugin-import-map-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
            Directory.Delete(_rootDir, recursive: true);
    }

    [Fact]
    public void Map_HtmlDemo_PreservesCurrentImportOptionSemantics()
    {
        var request = Request(
            path: ["import", "html-demo"],
            arguments: ["demo"],
            options: new Dictionary<string, JsonElement>
            {
                ["--theme"] = Json("demo-theme"),
                ["--strict"] = Json("anything"),
                ["--no-seed"] = Json(true),
                ["--push-notion"] = Json(true)
            },
            permissions: new PluginPermissionSet(Environment: new PluginEnvironmentPermission(Read: ["NOTION_TOKEN"])));

        var options = ImportPluginOptionsMapper.Map(request);

        Assert.Equal("html-demo", options.Subcommand);
        Assert.Equal(Path.Combine(_rootDir, "demo"), options.DemoDir);
        Assert.Equal("demo-theme", options.ThemeName);
        Assert.Equal("fail", options.StrictMode);
        Assert.False(options.GenerateSeed);
        Assert.True(options.PushNotion);
        Assert.Equal("NOTION_TOKEN", options.NotionTokenEnv);
    }

    [Fact]
    public void Map_Seed_ResolvesInputAndOutputUnderProjectRoot()
    {
        var request = Request(
            path: ["import", "seed"],
            arguments: ["seed"],
            options: new Dictionary<string, JsonElement>
            {
                ["--output"] = Json("content"),
                ["--force"] = Json(true)
            });

        var options = ImportPluginOptionsMapper.Map(request);

        Assert.Equal("seed", options.Subcommand);
        Assert.Equal(Path.Combine(_rootDir, "seed"), options.SeedDir);
        Assert.Equal(Path.Combine(_rootDir, "content"), options.OutputDir);
        Assert.True(options.Force);
    }

    [Fact]
    public void Map_HtmlDemo_RejectsCustomNotionTokenEnvWithoutGrant()
    {
        var request = Request(
            path: ["import", "html-demo"],
            arguments: ["demo"],
            options: new Dictionary<string, JsonElement>
            {
                ["--theme"] = Json("demo-theme"),
                ["--push-notion"] = Json(true),
                ["--notion-token-env"] = Json("CUSTOM_TOKEN")
            },
            permissions: new PluginPermissionSet(Environment: new PluginEnvironmentPermission(Read: ["NOTION_TOKEN"])));

        var ex = Assert.Throws<ImportPluginOptionsException>(() => ImportPluginOptionsMapper.Map(request));

        Assert.Equal("plugin.import.envDenied", ex.Code);
        Assert.Equal(2, ex.ExitCode);
    }

    [Fact]
    public void Map_HtmlDemo_RejectsInputPathEscapingProjectRoot()
    {
        var request = Request(
            path: ["import", "html-demo"],
            arguments: ["../demo"],
            options: new Dictionary<string, JsonElement>
            {
                ["--theme"] = Json("demo-theme")
            });

        var ex = Assert.Throws<ImportPluginOptionsException>(() => ImportPluginOptionsMapper.Map(request));

        Assert.Equal("plugin.import.pathDenied", ex.Code);
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

    private static JsonElement Json(string value)
        => JsonSerializer.SerializeToElement(value);

    private static JsonElement Json(bool value)
        => JsonSerializer.SerializeToElement(value);
}
