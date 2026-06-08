using Bukit.Shared;
using YamlDotNet.RepresentationModel;

namespace Bukit.Config;

/// <summary>
/// 1.0 config rejection scanner. Old fields are rejected with stable diagnostic codes
/// and a migration hint. No warning-only fallback.
/// </summary>
public static class ConfigRemovedFieldScanner
{
    /// <summary>
    /// Scans a site.yaml file for removed 1.0 fields. Throws ConfigException if any are found.
    /// </summary>
    public static void RejectRemovedFields(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            using var reader = File.OpenText(path);
            var yaml = new YamlStream();
            yaml.Load(reader);
            if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode is not YamlMappingNode root)
            {
                return;
            }

            RejectRemovedFields(root);
        }
        catch (ConfigException)
        {
            throw;
        }
        catch
        {
            // YAML parse errors are handled by ConfigLoader, not here.
        }
    }

    /// <summary>
    /// Scans a parsed YAML root node for removed 1.0 fields. Throws ConfigException if any are found.
    /// </summary>
    public static void RejectRemovedFields(YamlMappingNode root)
    {
        var removed = new List<ConfigRemovedField>();

        if (TryGetMapping(root, "site", out var siteNode) &&
            TryGetMapping(siteNode, "plugins", out var plugins) &&
            plugins.Children.ContainsKey(new YamlScalarNode("rss")))
        {
            removed.Add(new ConfigRemovedField("site.plugins.rss", "site.plugins.feed", DiagnosticCode.ConfigRemovedField));
        }

        if (TryGetMapping(root, "site", out var siteNode2) &&
            siteNode2.Children.ContainsKey(new YamlScalarNode("rssMode")))
        {
            removed.Add(new ConfigRemovedField("site.rssMode", "site.feed.formats", DiagnosticCode.ConfigRemovedField));
        }

        if (TryGetMapping(root, "site", out var siteNode2b) &&
            siteNode2b.Children.ContainsKey(new YamlScalarNode("searchMode")))
        {
            removed.Add(new ConfigRemovedField("site.searchMode", "site.search", DiagnosticCode.ConfigRemovedField));
        }

        if (root.Children.ContainsKey(new YamlScalarNode("outputPath")))
        {
            removed.Add(new ConfigRemovedField("outputPath", "route.url"));
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
                    removed.Add(new ConfigRemovedField(
                        $"collections.{keyNode.Value}.rss",
                        $"collections.{keyNode.Value}.feed",
                        DiagnosticCode.ConfigRemovedField));
                }
            }
        }

        if (TryGetMapping(root, "site", out var siteNode3) &&
            siteNode3.Children.ContainsKey(new YamlScalarNode("collection")))
        {
            removed.Add(new ConfigRemovedField("site.collection", "site.collections", DiagnosticCode.ConfigRemovedField));
        }

        if (TryGetMapping(root, "content", out var contentNode) &&
            TryGetMapping(contentNode, "notion", out var notionNode) &&
            notionNode.Children.ContainsKey(new YamlScalarNode("rootPageId")))
        {
            removed.Add(new ConfigRemovedField("content.notion.rootPageId", "content.notion.rootBlockId", DiagnosticCode.ConfigRemovedField));
        }

        if (TryGetMapping(root, "content", out var contentWithSources) &&
            contentWithSources.Children.TryGetValue(new YamlScalarNode("sources"), out var sourcesNode) &&
            sourcesNode is YamlSequenceNode sources)
        {
            for (var i = 0; i < sources.Children.Count; i++)
            {
                if (sources.Children[i] is YamlMappingNode sourceNode &&
                    TryGetMapping(sourceNode, "notion", out var sourceNotionNode) &&
                    sourceNotionNode.Children.ContainsKey(new YamlScalarNode("rootPageId")))
                {
                    removed.Add(new ConfigRemovedField(
                        $"content.sources[{i}].notion.rootPageId",
                        $"content.sources[{i}].notion.rootBlockId",
                        DiagnosticCode.ConfigRemovedField));
                }
            }
        }

        if (root.Children.TryGetValue(new YamlScalarNode("content"), out var contentChild) &&
            contentChild is YamlMappingNode contentMap &&
            contentMap.Children.TryGetValue(new YamlScalarNode("provider"), out _))
        {
            removed.Add(new ConfigRemovedField("content.provider", "content.sources", DiagnosticCode.ConfigProviderRemoved));
        }

        if (removed.Count > 0)
        {
            var details = string.Join("\n  ", removed.Select(r => $"{r.Path} — removed in 1.0. Migration: use '{r.Migration}' instead."));
            throw new ConfigException(
                $"Removed configuration fields detected in 1.0:\n  {details}\nBukit 1.0 is a new project contract. Remove or migrate the fields above.",
                removed.Count == 1 ? removed[0].Code : DiagnosticCode.ConfigRemovedField);
        }
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

internal sealed record ConfigRemovedField(
    string Path,
    string Migration,
    DiagnosticCode Code = DiagnosticCode.ConfigRemovedField);
