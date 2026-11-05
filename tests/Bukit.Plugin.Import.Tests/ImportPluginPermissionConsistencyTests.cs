using System.Runtime.CompilerServices;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Bukit.Plugin.Import.Tests;

public sealed class ImportPluginPermissionConsistencyTests
{
    [Fact]
    public void ExampleManifest_RequiresExactlyTheRuntimeWritePaths()
    {
        var path = Path.Combine(RepoRoot(),
            "src", "Bukit-Plugins", "Bukit.Plugin.Import", "examples", "minimal",
            "plugins", "import", "plugin.yaml");

        var declared = ReadSequence(path, "requiredPermissions", "fileSystem", "write");

        Assert.Equal(
            ImportPluginCommandSpecs.RequiredWritePaths.OrderBy(item => item, StringComparer.Ordinal),
            declared.OrderBy(item => item, StringComparer.Ordinal));
    }

    [Fact]
    public void ExampleGrant_IsValidYamlAndCoversTheRuntimeWritePaths()
    {
        var path = Path.Combine(RepoRoot(),
            "src", "Bukit-Plugins", "Bukit.Plugin.Import", "examples", "minimal",
            ".bukit", "plugins.yaml");

        // Loading throws on invalid YAML; this file used to be broken by indentation.
        var granted = ReadSequence(path, "plugins", "import", "permissions", "fileSystem", "write");

        foreach (var required in ImportPluginCommandSpecs.RequiredWritePaths)
            Assert.Contains(required, granted);
    }

    private static List<string> ReadSequence(string path, params string[] keys)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(File.ReadAllText(path)));

        YamlNode node = stream.Documents[0].RootNode ?? throw new InvalidOperationException($"empty YAML: {path}");
        foreach (var key in keys)
        {
            var map = Assert.IsType<YamlMappingNode>(node);
            Assert.True(map.Children.TryGetValue(new YamlScalarNode(key), out var child),
                $"missing '{key}' in {path}");
            node = child!;
        }

        var sequence = Assert.IsType<YamlSequenceNode>(node);
        return sequence.Children
            .Select(child => Assert.IsType<YamlScalarNode>(child).Value ?? "")
            .ToList();
    }

    private static string RepoRoot([CallerFilePath] string source = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", ".."));
}
