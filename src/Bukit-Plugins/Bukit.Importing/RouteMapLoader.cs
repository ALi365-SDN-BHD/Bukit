using YamlDotNet.RepresentationModel;

namespace Bukit.Importing;

internal static class RouteMapLoader
{
    internal static RouteMapConfig? Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            var yaml = new YamlStream();
            using var reader = File.OpenText(path);
            yaml.Load(reader);

            if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode == null)
            {
                Console.Error.WriteLine($"Route map '{path}' is empty.");
                return null;
            }

            var root = yaml.Documents[0].RootNode;
            YamlSequenceNode pagesSeq;

            if (root is YamlMappingNode mapping)
            {
                if (!mapping.Children.TryGetValue("pages", out var pagesNode) ||
                    pagesNode is not YamlSequenceNode seq)
                {
                    Console.Error.WriteLine($"Route map '{path}' is missing the 'pages' sequence.");
                    return null;
                }
                pagesSeq = seq;
            }
            else if (root is YamlSequenceNode directSeq)
            {
                pagesSeq = directSeq;
            }
            else
            {
                Console.Error.WriteLine($"Route map '{path}' has unsupported structure.");
                return null;
            }

            var config = new RouteMapConfig();
            foreach (var node in pagesSeq.Children)
            {
                if (node is not YamlMappingNode item)
                    continue;

                var page = new RouteMapPage
                {
                    Source = ReadString(item, "source"),
                    Route = ReadString(item, "route"),
                    Type = ReadString(item, "type"),
                    Template = ReadString(item, "template"),
                    Slug = ReadOptionalString(item, "slug"),
                    Description = ReadOptionalString(item, "description")
                };

                if (string.IsNullOrWhiteSpace(page.Source))
                {
                    Console.Error.WriteLine("Route map entry missing required 'source' field.");
                    continue;
                }

                config.Pages.Add(page);
            }

            return config;
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            Console.Error.WriteLine($"Failed to parse route map '{path}': {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load route map '{path}': {ex.Message}");
            return null;
        }
    }

    private static string ReadString(YamlMappingNode node, string key)
    {
        if (!node.Children.TryGetValue(key, out var valueNode))
            return "";
        return ((YamlScalarNode)valueNode).Value ?? "";
    }

    private static string? ReadOptionalString(YamlMappingNode node, string key)
    {
        if (!node.Children.TryGetValue(key, out var valueNode))
            return null;
        var val = ((YamlScalarNode)valueNode).Value;
        return string.IsNullOrWhiteSpace(val) ? null : val;
    }
}
