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

        if (TryGetMapping(root, "site", out var siteNode) &&
            siteNode.Children.ContainsKey(new YamlScalarNode("rssMode")))
        {
            warnings.Add(new ConfigDeprecationWarning(
                "site.rssMode",
                "site.feed.formats",
                "site.rssMode is deprecated. Use site.feed.formats instead."));
        }

        if (root.Children.ContainsKey(new YamlScalarNode("outputPath")))
        {
            warnings.Add(new ConfigDeprecationWarning(
                "outputPath",
                "route.outputPath",
                "Top-level outputPath is deprecated. Use route.outputPath instead."));
        }

        if (TryGetMapping(root, "site", out var siteForCollections) &&
            TryGetMapping(siteForCollections, "collections", out var collections))
        {
            foreach (var entry in collections.Children)
            {
                if (entry.Key is YamlScalarNode keyNode &&
                    entry.Value is YamlMappingNode collectionNode &&
                    collectionNode.Children.ContainsKey(new YamlScalarNode("rss")))
                {
                    warnings.Add(new ConfigDeprecationWarning(
                        $"collections.{keyNode.Value}.rss",
                        $"collections.{keyNode.Value}.feed",
                        $"collections.{keyNode.Value}.rss is deprecated. Use collections.{keyNode.Value}.feed instead."));
                }
            }
        }

        if (TryGetMapping(root, "site", out var siteNode2) &&
            siteNode2.Children.ContainsKey(new YamlScalarNode("collection")))
        {
            warnings.Add(new ConfigDeprecationWarning(
                "site.collection",
                "site.collections",
                "site.collection is deprecated. Use site.collections (plural) instead."));
        }

        if (TryGetMapping(root, "content", out var contentNode) &&
            TryGetMapping(contentNode, "notion", out var notionNode) &&
            notionNode.Children.ContainsKey(new YamlScalarNode("rootPageId")))
        {
            warnings.Add(new ConfigDeprecationWarning(
                "content.notion.rootPageId",
                "content.notion.rootBlockId",
                "content.notion.rootPageId is deprecated. Use content.notion.rootBlockId instead."));
        }

        if (root.Children.TryGetValue(new YamlScalarNode("content"), out var contentChild) &&
            contentChild is YamlMappingNode contentMap &&
            contentMap.Children.TryGetValue(new YamlScalarNode("provider"), out var providerNode) &&
            providerNode is YamlScalarNode providerScalar &&
            "notion".Equals(providerScalar.Value, StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add(new ConfigDeprecationWarning(
                "content.provider: notion",
                "content.sources",
                "content.provider notion is deprecated. Consider using content.sources with a notion source instead."));
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
