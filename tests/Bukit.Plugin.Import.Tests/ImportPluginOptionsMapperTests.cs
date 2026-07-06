using System.Text.Json;
using Bukit.Importing;
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

    [Fact]
    public void MapInvocation_NotionPush_PreservesLabsCompatibleOptions()
    {
        var request = Request(
            path: ["notion", "push"],
            arguments: [],
            options: new Dictionary<string, JsonElement>
            {
                ["--input"] = Json("seed"),
                ["--database-id"] = Json("db-single"),
                ["--database-map"] = Json("maps/notion.yaml"),
                ["--create-missing-databases"] = Json(true),
                ["--parent-page-id"] = Json("parent"),
                ["--generated-database-map"] = Json("maps/generated.yaml"),
                ["--mode"] = Json("upsert"),
                ["--unique-field"] = Json("Slug"),
                ["--update-content"] = Json("replace"),
                ["--dry-run"] = Json(true),
                ["--report"] = Json("reports/notion.json"),
                ["--no-validate-schema"] = Json(true)
            });

        var invocation = ImportPluginOptionsMapper.MapInvocation(request);

        Assert.Equal(ImportPluginInvocationKind.NotionPush, invocation.Kind);
        var options = Assert.IsType<ImportNotionSeedPushOptions>(invocation.NotionPushOptions);
        Assert.Equal(Path.Combine(_rootDir, "seed"), options.InputDir);
        Assert.Equal("db-single", options.DatabaseId);
        Assert.Equal(Path.Combine(_rootDir, "maps", "notion.yaml"), options.DatabaseMapPath);
        Assert.True(options.CreateMissingDatabases);
        Assert.Equal("parent", options.ParentPageId);
        Assert.Equal(Path.Combine(_rootDir, "maps", "generated.yaml"), options.GeneratedDatabaseMapPath);
        Assert.Equal("upsert", options.Mode);
        Assert.Equal("Slug", options.UniqueField);
        Assert.Equal("replace", options.UpdateContent);
        Assert.True(options.DryRun);
        Assert.Equal(Path.Combine(_rootDir, "reports", "notion.json"), options.ReportPath);
        Assert.False(options.ValidateSchema);
    }

    [Fact]
    public void MapInvocation_NotionPushRejectsCustomTokenEnvWithoutGrant()
    {
        var request = Request(
            path: ["notion", "push"],
            arguments: [],
            options: new Dictionary<string, JsonElement>
            {
                ["--input"] = Json("seed"),
                ["--dry-run"] = Json(true),
                ["--token-env"] = Json("CUSTOM_TOKEN")
            },
            permissions: new PluginPermissionSet(Environment: new PluginEnvironmentPermission(Read: ["NOTION_TOKEN"])));

        var ex = Assert.Throws<ImportPluginOptionsException>(() => ImportPluginOptionsMapper.MapInvocation(request));

        Assert.Equal("plugin.import.envDenied", ex.Code);
    }

    [Fact]
    public void MapInvocation_NotionValidateSchema_PreservesLabsCompatibleOptions()
    {
        var request = Request(
            path: ["notion", "validate-schema"],
            arguments: [],
            options: new Dictionary<string, JsonElement>
            {
                ["--database-id"] = Json("db-schema"),
                ["--token-env"] = Json("CUSTOM_TOKEN"),
                ["--report"] = Json("reports/schema.json")
            },
            permissions: new PluginPermissionSet(Environment: new PluginEnvironmentPermission(Read: ["CUSTOM_TOKEN"])));

        var invocation = ImportPluginOptionsMapper.MapInvocation(request);

        Assert.Equal(ImportPluginInvocationKind.NotionValidateSchema, invocation.Kind);
        var options = Assert.IsType<ImportNotionSchemaValidationOptions>(invocation.SchemaValidationOptions);
        Assert.Equal("db-schema", options.DatabaseId);
        Assert.Equal("CUSTOM_TOKEN", options.TokenEnv);
        Assert.Equal(Path.Combine(_rootDir, "reports", "schema.json"), options.ReportPath);
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
