using System.Text;
using YamlDotNet.RepresentationModel;

namespace Bukit.Labs.Cli.Commands;

internal static class CloneYamlWriter
{
    internal static bool EnsureSourcesConfig(string rootDir, string themeName, string? brand, CloneTokens tokens, List<string> warnings)
    {
        var path = Path.Combine(rootDir, "site.yaml");
        if (!File.Exists(path))
        {
            warnings.Add("site.yaml not found; skipped content source configuration.");
            return false;
        }

        try
        {
            var stream = new YamlStream();
            using (var reader = File.OpenText(path))
                stream.Load(reader);

            if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
            {
                warnings.Add("site.yaml root is not a mapping; skipped content source configuration.");
                return false;
            }

            var content = GetOrCreateMapping(root, "content");
            content.Children[new YamlScalarNode("provider")] = new YamlScalarNode("sources");
            var sources = GetOrCreateSequence(content, "sources");
            EnsureMarkdownSource(sources, "content", "content", "content", "page", "page");
            EnsureMarkdownSource(sources, "modules", "data", "data", "module", collection: null);

            var theme = GetOrCreateMapping(root, "theme");
            theme.Children[new YamlScalarNode("name")] = new YamlScalarNode(themeName);
            var parameters = GetOrCreateMapping(theme, "params");
            if (!string.IsNullOrWhiteSpace(brand))
            {
                parameters.Children[new YamlScalarNode("brand")] = new YamlScalarNode(brand);
                parameters.Children[new YamlScalarNode("footer_text")] = new YamlScalarNode(brand);
            }
            if (!string.IsNullOrWhiteSpace(tokens.Primary))
                parameters.Children[new YamlScalarNode("primary_color")] = new YamlScalarNode(tokens.Primary);
            if (!string.IsNullOrWhiteSpace(tokens.Accent))
                parameters.Children[new YamlScalarNode("accent_color")] = new YamlScalarNode(tokens.Accent);

            using var writer = new StringWriter();
            stream.Save(writer, assignAnchors: false);
            File.WriteAllText(path, writer.ToString());
            return true;
        }
        catch (Exception ex)
        {
            warnings.Add($"Failed to update site.yaml: {ex.Message}");
            return false;
        }
    }

    private static void EnsureMarkdownSource(
        YamlSequenceNode sources,
        string name,
        string mode,
        string dir,
        string defaultType,
        string? collection)
    {
        foreach (var child in sources.Children.OfType<YamlMappingNode>())
        {
            if (GetScalar(child, "name")?.Equals(name, StringComparison.OrdinalIgnoreCase) == true)
            {
                child.Children[new YamlScalarNode("type")] = new YamlScalarNode("markdown");
                child.Children[new YamlScalarNode("mode")] = new YamlScalarNode(mode);
                if (!string.IsNullOrWhiteSpace(collection))
                    child.Children[new YamlScalarNode("collection")] = new YamlScalarNode(collection);
                var markdown = GetOrCreateMapping(child, "markdown");
                markdown.Children[new YamlScalarNode("dir")] = new YamlScalarNode(dir);
                markdown.Children[new YamlScalarNode("defaultType")] = new YamlScalarNode(defaultType);
                return;
            }
        }

        var newNode = new YamlMappingNode
        {
            { "type", "markdown" },
            { "name", name },
            { "mode", mode },
        };
        if (!string.IsNullOrWhiteSpace(collection))
            newNode.Children[new YamlScalarNode("collection")] = new YamlScalarNode(collection);
        newNode.Children[new YamlScalarNode("markdown")] = new YamlMappingNode { { "dir", dir }, { "defaultType", defaultType } };
        sources.Add(newNode);
    }

    internal static YamlMappingNode GetOrCreateMapping(YamlMappingNode parent, string key)
    {
        var k = new YamlScalarNode(key);
        if (parent.Children.TryGetValue(k, out var existing) && existing is YamlMappingNode map)
            return map;

        var created = new YamlMappingNode();
        parent.Children[k] = created;
        return created;
    }

    internal static YamlSequenceNode GetOrCreateSequence(YamlMappingNode parent, string key)
    {
        var k = new YamlScalarNode(key);
        if (parent.Children.TryGetValue(k, out var existing) && existing is YamlSequenceNode seq)
            return seq;

        var created = new YamlSequenceNode();
        parent.Children[k] = created;
        return created;
    }

    internal static string? GetScalar(YamlMappingNode map, string key)
        => map.Children.TryGetValue(new YamlScalarNode(key), out var node) && node is YamlScalarNode scalar ? scalar.Value : null;

    internal static string YamlScalar(string value)
        => "'" + value.Replace("'", "''") + "'";

    internal static void AppendBlockScalar(StringBuilder sb, string key, string value)
    {
        sb.AppendLine($"{key}: |-");
        if (string.IsNullOrEmpty(value))
        {
            sb.AppendLine("  ");
            return;
        }

        foreach (var line in value.ReplaceLineEndings("\n").Split('\n'))
            sb.AppendLine("  " + line);
    }
}
