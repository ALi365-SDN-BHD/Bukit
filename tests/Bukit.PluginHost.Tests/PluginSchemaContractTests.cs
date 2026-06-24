using System.Text.Json;
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
        AssertRequired(root, ["version", "plugins"]);

        JsonElement pluginEntry = root
            .GetProperty("$defs")
            .GetProperty("pluginEntry");
        AssertRequired(pluginEntry, ["enabled", "source", "exposeCommands", "permissions"]);
        Assert.False(pluginEntry.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal("static", pluginEntry.GetProperty("properties").GetProperty("manifestPolicy").GetProperty("default").GetString());
        Assert.Equal("^plugins/(?!.*--)[a-z0-9](?:[a-z0-9-]{0,62}[a-z0-9])?$", pluginEntry.GetProperty("properties").GetProperty("source").GetProperty("pattern").GetString());
        Assert.Equal("^(?!.*--)[a-z0-9](?:[a-z0-9-]{0,62}[a-z0-9])?$", root.GetProperty("properties").GetProperty("plugins").GetProperty("propertyNames").GetProperty("pattern").GetString());
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
    }

    [Theory]
    [InlineData("plugins/Bukit.Plugin.Import/plugin.yaml")]
    [InlineData("plugins/Bukit.Plugin.Clone/plugin.yaml")]
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

    private static JsonDocument ReadSchema(string relativePath)
    {
        string path = Path.Combine(RepoRoot, relativePath);
        Assert.True(File.Exists(path), $"Missing schema: {relativePath}");
        return JsonDocument.Parse(File.ReadAllText(path));
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
            if (File.Exists(Path.Combine(current, "bukit.slnx")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
