using YamlDotNet.RepresentationModel;

namespace Bukit.Config;

public static class ConfigDeprecationScanner
{
    public static IReadOnlyList<ConfigDeprecationWarning> ScanFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return Array.Empty<ConfigDeprecationWarning>();
        }

        try
        {
            using var reader = File.OpenText(path);
            var yaml = new YamlStream();
            yaml.Load(reader);
            if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode is not YamlMappingNode root)
            {
                return Array.Empty<ConfigDeprecationWarning>();
            }

            return Scan(root);
        }
        catch
        {
            return Array.Empty<ConfigDeprecationWarning>();
        }
    }

    public static IReadOnlyList<ConfigDeprecationWarning> Scan(YamlMappingNode root)
    {
        var warnings = new List<ConfigDeprecationWarning>();
        if (TryGetMapping(root, "site", out var site) &&
            TryGetMapping(site, "plugins", out var plugins) &&
            plugins.Children.ContainsKey(new YamlScalarNode("rss")))
        {
            warnings.Add(new ConfigDeprecationWarning(
                "site.plugins.rss",
                "site.plugins.feed",
                "site.plugins.rss is deprecated. Use site.plugins.feed instead."));
        }

        return warnings;
    }

    private static bool TryGetMapping(YamlMappingNode node, string key, out YamlMappingNode mapping)
    {
        if (node.Children.TryGetValue(new YamlScalarNode(key), out var child) &&
            child is YamlMappingNode result)
        {
            mapping = result;
            return true;
        }

        mapping = null!;
        return false;
    }
}

public sealed record ConfigDeprecationWarning(string Path, string Replacement, string Message);
