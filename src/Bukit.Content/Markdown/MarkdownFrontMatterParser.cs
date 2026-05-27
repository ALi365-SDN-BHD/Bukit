using YamlDotNet.RepresentationModel;

namespace Bukit.Content.Markdown;

internal static class MarkdownFrontMatterParser
{
    internal static bool TryExtractFrontMatter(string markdown, out string frontMatterYaml, out string bodyMarkdown)
    {
        frontMatterYaml = string.Empty;
        bodyMarkdown = markdown;

        if (string.IsNullOrWhiteSpace(markdown))
        {
            return false;
        }

        var normalized = markdown.Replace("\r\n", "\n");
        if (!normalized.StartsWith("---\n", StringComparison.Ordinal) && !string.Equals(normalized.TrimStart(), "---", StringComparison.Ordinal))
        {
            return false;
        }

        var lines = normalized.Split('\n');
        if (lines.Length < 3 || lines[0].Trim() != "---")
        {
            return false;
        }

        var end = -1;
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---")
            {
                end = i;
                break;
            }
        }

        if (end <= 0)
        {
            return false;
        }

        frontMatterYaml = string.Join("\n", lines.Skip(1).Take(end - 1));
        bodyMarkdown = string.Join("\n", lines.Skip(end + 1));
        return true;
    }

    internal static IReadOnlyDictionary<string, object> ParseFrontMatter(string yaml)
    {
        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return dict;
        }

        try
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(yaml));
            if (stream.Documents.Count == 0)
            {
                return dict;
            }

            if (stream.Documents[0].RootNode is not YamlMappingNode root)
            {
                return dict;
            }

            foreach (var kv in root.Children)
            {
                if (kv.Key is not YamlScalarNode k || string.IsNullOrWhiteSpace(k.Value))
                {
                    continue;
                }

                var key = k.Value.Trim();
                dict[key] = ToObject(kv.Value);
            }

            NormalizeTaxonomy(dict, "tags");
            NormalizeTaxonomy(dict, "categories");

            return dict;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[warn] Failed to parse front matter: {ex.Message}");
            return dict;
        }
    }

    private static void NormalizeTaxonomy(Dictionary<string, object> dict, string key)
    {
        if (!dict.TryGetValue(key, out var v) || v is null)
        {
            return;
        }

        if (v is string s)
        {
            var parts = s.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            dict[key] = parts.ToList();
            return;
        }

        if (v is IEnumerable<object> seq)
        {
            dict[key] = seq.Select(x => x?.ToString() ?? string.Empty)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
        }
    }

    private static object ToObject(YamlNode node)
    {
        return node switch
        {
            YamlScalarNode s => s.Value ?? string.Empty,
            YamlSequenceNode seq => seq.Children.Select(ToObject).ToList(),
            YamlMappingNode map => map.Children
                .Where(p => p.Key is YamlScalarNode ks && !string.IsNullOrWhiteSpace(ks.Value))
                .ToDictionary(
                    p => ((YamlScalarNode)p.Key).Value!,
                    p => ToObject(p.Value),
                    StringComparer.OrdinalIgnoreCase),
            _ => node.ToString()
        };
    }
}
