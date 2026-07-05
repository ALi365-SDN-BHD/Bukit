using YamlDotNet.RepresentationModel;

namespace Bukit.Importing;

public static class ImportThemeSelectionService
{
    public static Task<int> SetThemeAsync(
        string name,
        string fullConfigPath,
        string rootDir,
        string? brand,
        string? primaryColor,
        string? accentColor)
    {
        var themesDir = Path.Combine(rootDir, "themes");
        var themeRoot = Path.Combine(themesDir, name);
        if (!Directory.Exists(themeRoot))
        {
            Console.Error.WriteLine($"Theme not found: {name}");
            return Task.FromResult(2);
        }

        if (!File.Exists(fullConfigPath))
        {
            Console.Error.WriteLine($"Config not found: {fullConfigPath}");
            return Task.FromResult(2);
        }

        var yaml = File.ReadAllText(fullConfigPath);
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            root = new YamlMappingNode();
            stream.Documents.Clear();
            stream.Documents.Add(new YamlDocument(root));
        }

        var themeNode = GetOrCreateMapping(root, "theme");
        themeNode.Children[new YamlScalarNode("name")] = new YamlScalarNode(name);
        var hasParams =
            !string.IsNullOrWhiteSpace(brand) ||
            !string.IsNullOrWhiteSpace(primaryColor) ||
            !string.IsNullOrWhiteSpace(accentColor);
        var paramsNode = hasParams ? GetOrCreateMapping(themeNode, "params") : null;
        if (!string.IsNullOrWhiteSpace(brand))
        {
            paramsNode!.Children[new YamlScalarNode("brand")] = new YamlScalarNode(brand);
            paramsNode.Children[new YamlScalarNode("footer_text")] = new YamlScalarNode(brand);
        }

        if (!string.IsNullOrWhiteSpace(primaryColor))
        {
            paramsNode!.Children[new YamlScalarNode("primary_color")] = new YamlScalarNode(primaryColor);
        }

        if (!string.IsNullOrWhiteSpace(accentColor))
        {
            paramsNode!.Children[new YamlScalarNode("accent_color")] = new YamlScalarNode(accentColor);
        }

        using var writer = new StringWriter();
        stream.Save(writer, assignAnchors: false);
        File.WriteAllText(fullConfigPath, writer.ToString());

        Console.WriteLine($"Theme set: {name}");
        return Task.FromResult(0);
    }

    private static YamlMappingNode GetOrCreateMapping(YamlMappingNode parent, string key)
    {
        var k = new YamlScalarNode(key);
        if (parent.Children.TryGetValue(k, out var existing) && existing is YamlMappingNode map)
        {
            return map;
        }

        var created = new YamlMappingNode();
        parent.Children[k] = created;
        return created;
    }
}
