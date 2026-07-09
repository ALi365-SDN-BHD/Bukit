using System.Text.Json;
using System.Text.RegularExpressions;
using Bukit.Plugin.Abstractions.Config;
using Xunit;

namespace Bukit.PluginHost.Tests;

public sealed class PluginSchemaContractTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void PluginConfigSchema_DefinesStrictProjectPluginConfig()
    {
        using JsonDocument document = ReadSchema("docs/schemas/bukit-plugin-config.v1.schema.json");
        JsonElement root = document.RootElement;

        Assert.Equal("https://json-schema.org/draft/2020-12/schema", root.GetProperty("$schema").GetString());
        Assert.Equal("https://bukit.dev/schemas/bukit-plugin-config.v1.json", root.GetProperty("$id").GetString());
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
        AssertRequired(root, ["version"]);

        JsonElement pluginEntry = root
            .GetProperty("$defs")
            .GetProperty("pluginEntry");
        AssertRequired(pluginEntry, ["enabled", "source", "exposeCommands", "permissions"]);
        Assert.False(pluginEntry.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal("static", pluginEntry.GetProperty("properties").GetProperty("manifestPolicy").GetProperty("default").GetString());
        Assert.Equal("^plugins/(?!.*--)[a-z0-9](?:[a-z0-9-]{0,62}[a-z0-9])?$", pluginEntry.GetProperty("properties").GetProperty("source").GetProperty("pattern").GetString());
        Assert.Equal("^(?!.*--)[a-z0-9](?:[a-z0-9-]{0,62}[a-z0-9])?$", root.GetProperty("properties").GetProperty("plugins").GetProperty("propertyNames").GetProperty("pattern").GetString());
        AssertPermissionPathRefs(root);
    }

    [Fact]
    public void PluginManifestSchema_DefinesStrictProcessPluginManifest()
    {
        using JsonDocument document = ReadSchema("docs/schemas/bukit-plugin-manifest.v1.schema.json");
        JsonElement root = document.RootElement;

        Assert.Equal("https://json-schema.org/draft/2020-12/schema", root.GetProperty("$schema").GetString());
        Assert.Equal("https://bukit.dev/schemas/bukit-plugin-manifest.v1.json", root.GetProperty("$id").GetString());
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
        AssertRequired(root, ["id", "name", "version", "protocol", "kind", "platforms"]);

        JsonElement properties = root.GetProperty("properties");
        Assert.Equal("^(?!.*--)[a-z0-9](?:[a-z0-9-]{0,62}[a-z0-9])?$", properties.GetProperty("id").GetProperty("pattern").GetString());
        Assert.Equal("bukit-plugin-v1", properties.GetProperty("protocol").GetProperty("const").GetString());
        Assert.Equal("process", properties.GetProperty("kind").GetProperty("const").GetString());
        Assert.Equal("self-contained", properties.GetProperty("distribution").GetProperty("default").GetString());
        AssertPermissionPathRefs(root);
    }

    [Theory]
    [InlineData(".")]
    [InlineData("./content")]
    [InlineData("content/posts")]
    [InlineData("themes\\starter")]
    [InlineData(".bukit/reports/plugin-output/echo")]
    [InlineData(".bukit/reports/plugin-output/echo/result.json")]
    [InlineData(".bukit/tmp/echo/work.json")]
    public void PluginConfigSchemaPermissionPathPattern_AllowsSafeProjectRelativePaths(string path)
    {
        string pattern = ReadPermissionPathPattern("docs/schemas/bukit-plugin-config.v1.schema.json");

        Assert.Matches(new Regex(pattern, RegexOptions.CultureInvariant), path);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../secret")]
    [InlineData("content/../secret")]
    [InlineData("/tmp")]
    [InlineData("C:/tmp")]
    [InlineData("C:\\tmp")]
    [InlineData(".bukit")]
    [InlineData(".bukit/plugins")]
    [InlineData(".bukit/plugins.yaml")]
    [InlineData(".bukit/reports/plugin-output")]
    [InlineData(".bukit/reports/plugin-output/Bad")]
    [InlineData(".bukit/tmp")]
    [InlineData(".bukit/tmp/bad--id")]
    public void PluginConfigSchemaPermissionPathPattern_RejectsUnsafePaths(string path)
    {
        string pattern = ReadPermissionPathPattern("docs/schemas/bukit-plugin-config.v1.schema.json");

        Assert.DoesNotMatch(new Regex(pattern, RegexOptions.CultureInvariant), path);
    }

    [Theory]
    [InlineData("src/Bukit-Plugins/Bukit.Plugin.Import/plugin.yaml")]
    [InlineData("src/Bukit-Plugins/Bukit.Plugin.Clone/plugin.yaml")]
    [InlineData("src/Bukit-Plugins/Bukit.Plugin.WechatSync/plugin.yaml")]
    public void OfficialPluginPackageManifestPolicy_WhenPresent_MustBeStatic(string relativePath)
    {
        string path = Path.Combine(RepoRoot, relativePath);
        if (!File.Exists(path))
        {
            return;
        }

        string manifest = File.ReadAllText(path);
        Assert.DoesNotContain("manifestPolicy: runtime-only", manifest, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("src/Bukit-Plugins/Bukit.Plugin.Import", "import")]
    [InlineData("src/Bukit-Plugins/Bukit.Plugin.Clone", "clone")]
    [InlineData("src/Bukit-Plugins/Bukit.Plugin.WechatSync", "wechat-sync")]
    public async Task OfficialPluginPackageExampleConfig_WhenPackageExists_MustLoad(string packagePath, string pluginId)
    {
        string fullPackagePath = Path.Combine(RepoRoot, packagePath);
        if (!Directory.Exists(fullPackagePath))
        {
            return;
        }

        string exampleRoot = Path.Combine(fullPackagePath, "examples", "minimal");
        string configPath = Path.Combine(exampleRoot, ".bukit", "plugins.yaml");
        Assert.True(File.Exists(configPath), $"Missing official plugin example config: {configPath}");

        var loader = new PluginConfigLoader();
        PluginHostConfig config = await loader.LoadAsync(exampleRoot, CancellationToken.None);

        PluginConfigEntry entry = Assert.Single(config.Plugins).Value;
        Assert.True(entry.Enabled);
        Assert.True(entry.PermissionsExplicit);
        Assert.Equal($"plugins/{pluginId}", entry.Source);
        Assert.Contains(pluginId, entry.ExposeCommands);
        Assert.NotEqual("runtime-only", entry.ManifestPolicy);
    }

    [Theory]
    [InlineData("src/Bukit-Plugins/Bukit.Plugin.Import", "import")]
    [InlineData("src/Bukit-Plugins/Bukit.Plugin.Clone", "clone")]
    [InlineData("src/Bukit-Plugins/Bukit.Plugin.WechatSync", "wechat-sync")]
    public async Task OfficialPluginPackageExampleManifest_WhenPackageExists_MustLoad(string packagePath, string pluginId)
    {
        string fullPackagePath = Path.Combine(RepoRoot, packagePath);
        if (!Directory.Exists(fullPackagePath))
        {
            return;
        }

        string pluginRoot = Path.Combine(fullPackagePath, "examples", "minimal", "plugins", pluginId);
        string manifestPath = Path.Combine(pluginRoot, "plugin.yaml");
        Assert.True(File.Exists(manifestPath), $"Missing official plugin example manifest: {manifestPath}");

        var loader = new PluginManifestLoader();
        var manifest = await loader.LoadAsync(pluginRoot, CancellationToken.None);

        Assert.Equal(pluginId, manifest.Id);
        Assert.Equal("bukit-plugin-v1", manifest.Protocol);
        Assert.Equal("process", manifest.Kind);
        Assert.Equal("self-contained", manifest.Distribution);
        Assert.Contains(manifest.Commands, command => command.Name == pluginId);
    }

    [Theory]
    [InlineData("src/Bukit-Plugins/Bukit.Plugin.Import")]
    [InlineData("src/Bukit-Plugins/Bukit.Plugin.Clone")]
    [InlineData("src/Bukit-Plugins/Bukit.Plugin.WechatSync")]
    public void OfficialPluginPackageExampleConfig_WhenPackageExists_MustNotDeclareForbiddenRuntimeFields(string packagePath)
    {
        string fullPackagePath = Path.Combine(RepoRoot, packagePath);
        if (!Directory.Exists(fullPackagePath))
        {
            return;
        }

        string configPath = Path.Combine(fullPackagePath, "examples", "minimal", ".bukit", "plugins.yaml");
        Assert.True(File.Exists(configPath), $"Missing official plugin example config: {configPath}");

        string config = File.ReadAllText(configPath);
        Assert.DoesNotContain("manifestPolicy: runtime-only", config, StringComparison.Ordinal);
        Assert.DoesNotContain("entry:", config, StringComparison.Ordinal);
        Assert.DoesNotContain(".bukit/plugins", config, StringComparison.Ordinal);
        Assert.DoesNotContain("site.externalPlugins", config, StringComparison.Ordinal);
    }

    private static JsonDocument ReadSchema(string relativePath)
    {
        string path = Path.Combine(RepoRoot, relativePath);
        Assert.True(File.Exists(path), $"Missing schema: {relativePath}");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string ReadPermissionPathPattern(string relativePath)
    {
        using JsonDocument document = ReadSchema(relativePath);
        return document.RootElement
            .GetProperty("$defs")
            .GetProperty("permissionPath")
            .GetProperty("pattern")
            .GetString() ?? string.Empty;
    }

    private static void AssertPermissionPathRefs(JsonElement root)
    {
        JsonElement permissions = root.GetProperty("$defs").GetProperty("permissions");
        JsonElement fileSystem = permissions.GetProperty("properties").GetProperty("fileSystem");
        JsonElement properties = fileSystem.GetProperty("properties");

        Assert.Equal("#/$defs/permissionPath", properties.GetProperty("read").GetProperty("items").GetProperty("$ref").GetString());
        Assert.Equal("#/$defs/permissionPath", properties.GetProperty("write").GetProperty("items").GetProperty("$ref").GetString());
    }

    private static void AssertRequired(JsonElement element, IReadOnlyList<string> expected)
    {
        string[] required = element.GetProperty("required")
            .EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty)
            .ToArray();
        Assert.Equal(expected, required);
    }

    private static string FindRepoRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "bukit-core.slnx")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
